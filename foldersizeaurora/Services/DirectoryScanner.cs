using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>
/// Escanea un arbol de directorios acumulando tamanos sin cargar todo en memoria como un unico buffer.
/// Recorrido iterativo (pila) para evitar desbordamiento de pila en arboles muy profundos.
/// </summary>
public sealed class DirectoryScanner
{
    private const int TopChartItems = 10;

    /// <param name="includeSystemHidden">Incluye archivos ocultos y de sistema.</param>
    public async Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSystemHidden,
        IProgress<ScanSnapshot>? progress,
        CancellationToken cancellationToken)
    {
        var rootInfo = new DirectoryInfo(rootPath);
        if (!rootInfo.Exists)
            throw new DirectoryNotFoundException($"No existe la carpeta: {rootPath}");

        var root = new FolderNode(rootInfo.Name, rootInfo.FullName, isRoot: true);
        var stopwatch = Stopwatch.StartNew();

        var extensionTotals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        long totalBytes = 0;
        long totalFiles = 0;
        long totalFolders = 0;
        long skipped = 0;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false, // recursion propia para poder saltarse junctions/symlinks
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = includeSystemHidden ? 0 : (FileAttributes.Hidden | FileAttributes.System),
            MatchCasing = MatchCasing.CaseInsensitive
        };

        var stack = new Stack<(string Path, FolderNode Node)>();
        stack.Push((root.FullPath, root));
        var currentPath = root.FullPath;
        long lastReportMs = 0;

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (dirPath, node) = stack.Pop();
            currentPath = dirPath;

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(dirPath, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entry);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        skipped++;
                        continue;
                    }

                    // No seguir junctions, symlinks ni puntos de repeticion: evita ciclos.
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        skipped++;
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        var childName = Path.GetFileName(entry.TrimEnd(Path.DirectorySeparatorChar));
                        var child = new FolderNode(childName, entry);
                        node.Children.Add(child);
                        totalFolders++;
                        stack.Push((entry, child));
                    }
                    else
                    {
                        long length;
                        DateTime modified;
                        try
                        {
                            var fi = new FileInfo(entry);
                            length = fi.Length;
                            modified = fi.LastWriteTime;
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            skipped++;
                            continue;
                        }

                        var name = Path.GetFileName(entry);
                        var extension = Path.GetExtension(entry);
                        var file = new FileEntry(name, entry, length, extension, modified);

                        node.Files.Add(file);
                        node.DirectSize += length;
                        node.Size += length;
                        node.FileCount++;

                        // Se acumula aqui porque el recorrido ya visita cada archivo:
                        // despues solo hay que leerlo.
                        if (modified > node.LatestWriteTime) node.LatestWriteTime = modified;

                        totalBytes += length;
                        totalFiles++;

                        extensionTotals.TryGetValue(file.Extension, out var acc);
                        extensionTotals[file.Extension] = acc + length;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++;
            }

            // Reporte de progreso limitado a ~4 veces por segundo para incluir snapshot de vista.
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            if (progress is not null && elapsedMs - lastReportMs >= 250)
            {
                lastReportMs = elapsedMs;
                // Clonar superficialmente los hijos directos del root para no causar concurrencia
                var partialChildren = root.Children.ToArray();
                progress.Report(new ScanSnapshot(
                    currentPath,
                    totalBytes,
                    (int)Math.Min(totalFiles, int.MaxValue),
                    (int)Math.Min(totalFolders, int.MaxValue),
                    partialChildren,
                    skipped));
                await Task.Yield();
            }
        }

        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        // Porcentajes respecto al total real.
        AssignPercentages(root, totalBytes);

        var topFolders = BuildTopFolders(root, totalBytes);
        var topFiles = BuildTopFiles(root, totalBytes);
        var byExtension = BuildExtensionChart(extensionTotals, totalBytes);

        return new ScanResult(
            root,
            totalBytes,
            (int)Math.Min(totalFiles, int.MaxValue),
            (int)Math.Min(totalFolders, int.MaxValue),
            skipped,
            stopwatch.Elapsed,
            topFolders,
            topFiles,
            byExtension);
    }

    private static void AssignPercentages(FolderNode node, long totalBytes)
    {
        if (totalBytes <= 0) return;

        foreach (var descendant in node.DescendantsAndSelf())
        {
            descendant.Percentage = descendant.Size * 100d / totalBytes;
            descendant.RefreshDisplay();
        }
    }

    private static IReadOnlyList<ChartItem> BuildTopFolders(FolderNode root, long totalBytes)
    {
        if (totalBytes <= 0) return Array.Empty<ChartItem>();

        // Las carpetas mas pesadas de todo el arbol.
        var candidates = root.DescendantsAndSelf()
            .Where(n => n != root)
            .OrderByDescending(n => n.Size)
            .Take(TopChartItems)
            .ToList();

        return candidates
            .Select((n, i) => new ChartItem(PathLabel(n.FullPath), n.Size, (double)n.Size / totalBytes, i % 6, n.FullPath))
            .ToList();
    }

    private static IReadOnlyList<ChartItem> BuildTopFiles(FolderNode root, long totalBytes)
    {
        if (totalBytes <= 0) return Array.Empty<ChartItem>();

        // No se ordenan todos los archivos. En una unidad grande son cientos de miles
        // y materializarlos en una lista para descartar casi todos no es un detalle:
        // es lo que congela la aplicacion al terminar el escaneo. Aqui solo se
        // conserva la lista de los diez mayores, y cada archivo nuevo solo displace
        // al peor de los diez si de verdad es mayor.
        var best = new List<FileEntry>(TopChartItems + 1);

        foreach (var folder in root.DescendantsAndSelf())
        {
            foreach (var file in folder.Files)
            {
                if (best.Count < TopChartItems)
                {
                    best.Add(file);

                    if (best.Count == TopChartItems)
                    {
                        best.Sort(static (a, b) => b.Length.CompareTo(a.Length));
                    }

                    continue;
                }

                if (file.Length <= best[^1].Length) continue;

                best[^1] = file;

                for (var i = best.Count - 1; i > 0 && best[i].Length > best[i - 1].Length; i--)
                {
                    (best[i], best[i - 1]) = (best[i - 1], best[i]);
                }
            }
        }

        best.Sort(static (a, b) => b.Length.CompareTo(a.Length));

        return best
            .Select((f, i) => new ChartItem(PathLabel(f.FullPath), f.Length, (double)f.Length / totalBytes, i % 6, f.FullPath))
            .ToList();
    }

    /// <summary>
    /// Etiqueta de grafico: los ultimos tres tramos de la ruta. Con el nombre suelto,
    /// dos "win-x64" o dos "PresentationFramework.dll" salen indistinguibles y el
    /// grafico parece duplicado sin serlo.
    /// </summary>
    private static string PathLabel(string fullPath)
    {
        var trimmed = fullPath.TrimEnd('\\');
        if (trimmed.Length == 0) return fullPath;

        var parts = trimmed.Split('\\');
        if (parts.Length <= 3) return trimmed;

        return string.Join('\\', parts[^3], parts[^2], parts[^1]);
    }

    private static IReadOnlyList<ChartItem> BuildExtensionChart(Dictionary<string, long> totals, long totalBytes)
    {
        if (totalBytes <= 0) return Array.Empty<ChartItem>();

        return totals
            .OrderByDescending(kv => kv.Value)
            .Take(TopChartItems)
            .Select((kv, i) => new ChartItem(kv.Key, kv.Value, (double)kv.Value / totalBytes, i % 6))
            .ToList();
    }
}