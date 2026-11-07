using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>
/// Construye la vista visible a partir del resultado del escaneo: filtra, busca y ordena.
/// Trabaja sobre clones para que el resultado original del escaneo permanezca intacto
/// y se pueda restaurar al cambiar cualquier control.
/// </summary>
public static class TreeTransformer
{
    private static readonly Dictionary<TypeFilter, string[]> Map = new()
    {
        [TypeFilter.Images] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico", ".heic", ".raw", ".psd" },
        [TypeFilter.Video] = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg" },
        [TypeFilter.Audio] = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus" },
        [TypeFilter.Documents] = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".csv", ".md", ".epub" },
        [TypeFilter.Archives] = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab" },
        [TypeFilter.Code] = new[] { ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".cpp", ".h", ".c", ".go", ".rs", ".php", ".html", ".css", ".json", ".xml", ".yml", ".sql" },
        [TypeFilter.DiskImages] = new[] { ".vhd", ".vhdx", ".vmdk", ".vdi", ".qcow2", ".img" },
        [TypeFilter.Executables] = new[] { ".exe", ".dll", ".msi", ".bat", ".cmd", ".com", ".sys" }
    };

    public static FolderNode Build(FolderNode source, long minSizeBytes, TypeFilter typeFilter, string? search, SortMode sortMode, bool ascending)
    {
        var clone = Clone(source, minSizeBytes, typeFilter, search);

        // La fecha mas reciente de cada carpeta se resuelve en una sola pasada de
        // abajo arriba. Resolverla al ordenar, carpeta por carpeta, es cuadratico:
        // cada carpeta comparaba su fecha contra todo su subarbol una y otra vez, y
        // elegir «Ultima modificacion» en un arbol grande se colgaba.
        var latest = new Dictionary<FolderNode, DateTime>();
        CollectLatest(clone, latest);

        SortRecursively(clone, sortMode, ascending, latest);
        return clone;
    }

    private static FolderNode Clone(FolderNode source, long minSizeBytes, TypeFilter typeFilter, string? search)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var needle = search?.Trim() ?? string.Empty;

        var node = new FolderNode(source.Name, source.FullPath, source.IsRoot);

        foreach (var file in source.Files)
        {
            if (file.Length < minSizeBytes) continue;
            if (!MatchesType(file.Extension, typeFilter)) continue;
            if (hasSearch && !file.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;

            node.Files.Add(file);
            node.Size += file.Length;
            node.DirectSize += file.Length;
            node.FileCount++;

            // Se recalcula sobre los archivos que sobreviven al filtro: copiar el valor
            // del origen daria una fecha de un archivo que el filtro acaba de descartar.
            if (file.LastWriteTime > node.LatestWriteTime) node.LatestWriteTime = file.LastWriteTime;
        }

        foreach (var child in source.Children)
        {
            var clonedChild = Clone(child, minSizeBytes, typeFilter, search);

            // Una carpeta sobrevive si cumple el filtro de tipo/tamano o si contiene coincidencias.
            bool survivesDirectly = clonedChild.Size > 0;
            bool survivesBySearch = hasSearch
                && (child.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) || clonedChild.FileCount > 0 || clonedChild.Children.Count > 0);

            if (!survivesDirectly && !survivesBySearch) continue;

            node.Children.Add(clonedChild);
            node.Size += clonedChild.Size;
            node.FolderCount += clonedChild.FolderCount + 1;
        }

        return node;
    }

    private static bool MatchesType(string extension, TypeFilter filter)
    {
        if (filter == TypeFilter.All) return true;
        if (!Map.TryGetValue(filter, out var list)) return true;
        return list.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static void SortRecursively(FolderNode node, SortMode sortMode, bool ascending, Dictionary<FolderNode, DateTime> latest)
    {
        var sortedChildren = sortMode switch
        {
            SortMode.Name => node.Children.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            SortMode.Files => node.Children.OrderBy(c => c.FileCount).ToList(),
            SortMode.Date => node.Children.OrderByDescending(c => latest[c]).ToList(),
            _ => node.Children.OrderByDescending(c => c.Size).ToList()
        };

        if (ascending && sortMode != SortMode.Size && sortMode != SortMode.Date)
        {
            sortedChildren.Reverse();
        }

        node.Children.Clear();
        node.Children.AddRange(sortedChildren);

        var sortedFiles = sortMode switch
        {
            SortMode.Name => node.Files.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            SortMode.Date => node.Files.OrderByDescending(f => f.LastWriteTime).ToList(),
            _ => node.Files.OrderByDescending(f => f.Length).ToList()
        };

        if (ascending && sortMode == SortMode.Name)
        {
            sortedFiles.Reverse();
        }

        node.Files.Clear();
        node.Files.AddRange(sortedFiles);

        foreach (var child in node.Children)
        {
            SortRecursively(child, sortMode, ascending, latest);
        }

        node.NotifyDisplayChanged();
        node.RebuildItems(sortMode);
    }

    /// <summary>Fecha mas reciente de cada carpeta y de sus descendientes, en una pasada.</summary>
    private static void CollectLatest(FolderNode node, Dictionary<FolderNode, DateTime> latest)
    {
        var value = DateTime.MinValue;

        foreach (var file in node.Files)
        {
            if (file.LastWriteTime > value) value = file.LastWriteTime;
        }

        foreach (var child in node.Children)
        {
            CollectLatest(child, latest);

            if (latest[child] > value) value = latest[child];
        }

        latest[node] = value;
    }
}