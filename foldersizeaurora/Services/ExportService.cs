using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>Exporta el arbol escaneado a CSV, JSON o informe de texto.</summary>
public sealed class ExportService
{
    private readonly FolderNode _root;
    private readonly long _scannedTotalBytes;

    public ExportService(FolderNode root, long scannedTotalBytes)
    {
        _root = root;
        _scannedTotalBytes = scannedTotalBytes <= 0 ? root.Size : scannedTotalBytes;
    }

    public void ExportCsv(string path)
    {
        // BOM UTF-8 para que Excel respete los acentos.
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));

        writer.WriteLine("Tipo;Ruta;Nombre;Tamano (bytes);Tamano;Archivos propios;Carpetas hijas;Porcentaje;Ultima modificacion");

        foreach (var node in _root.DescendantsAndSelf())
        {
            writer.WriteLine(string.Join(';',
                "Carpeta",
                Escape(node.FullPath),
                Escape(node.Name),
                node.Size.ToString(CultureInfo.InvariantCulture),
                node.DisplaySize,
                node.Files.Count.ToString(CultureInfo.InvariantCulture),
                node.Children.Count.ToString(CultureInfo.InvariantCulture),
                node.Percentage.ToString("0.00", CultureInfo.InvariantCulture),
                LatestDate(node).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)));
        }

        foreach (var file in EnumerateFiles())
        {
            writer.WriteLine(string.Join(';',
                "Archivo",
                Escape(file.FullPath),
                Escape(file.Name),
                file.Length.ToString(CultureInfo.InvariantCulture),
                file.DisplaySize,
                "1",
                "0",
                (_scannedTotalBytes > 0 ? file.Length * 100d / _scannedTotalBytes : 0).ToString("0.00", CultureInfo.InvariantCulture),
                file.LastWriteTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)));
        }
    }

    public void ExportJson(string path)
    {
        var root = new
        {
            generado_en = DateTime.Now.ToString("O"),
            raiz = _root.FullPath,
            total_bytes = _scannedTotalBytes,
            total_legible = Formatter.Bytes(_scannedTotalBytes),
            carpetas = _root.DescendantsAndSelf().Select(n => new
            {
                ruta = n.FullPath,
                nombre = n.Name,
                bytes = n.Size,
                archivos_propios = n.Files.Count,
                carpetas_hijas = n.Children.Count,
                porcentaje = Math.Round(n.Percentage, 4)
            }),
            archivos = EnumerateFiles().Select(f => new
            {
                ruta = f.FullPath,
                nombre = f.Name,
                bytes = f.Length,
                extension = f.Extension,
                modificado = f.LastWriteTime.ToString("O")
            })
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(root, options), new UTF8Encoding(true));
    }

    public void ExportReport(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FOLDER SIZE AURORA - INFORME");
        sb.AppendLine(new string('=', 46));
        sb.AppendLine($"Fecha              : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Carpeta analizada  : {_root.FullPath}");
        sb.AppendLine($"Tamano total       : {Formatter.Bytes(_scannedTotalBytes)} ({_scannedTotalBytes:N0} bytes)");
        sb.AppendLine($"Carpetas           : {_root.DescendantsAndSelf().Count()}");
        sb.AppendLine($"Archivos           : {EnumerateFiles().Count()}");
        sb.AppendLine();

        sb.AppendLine("TOP 15 CARPETAS");
        sb.AppendLine(new string('-', 46));
        int index = 1;
        foreach (var node in _root.DescendantsAndSelf().Where(n => n != _root).OrderByDescending(n => n.Size).Take(15))
        {
            sb.AppendLine($"{index,2}. {node.DisplaySize,12}  {node.Percentage,6:0.00} %  {node.Name}");
            sb.AppendLine($"    {node.FullPath}");
            index++;
        }
        sb.AppendLine();

        sb.AppendLine("TOP 15 ARCHIVOS");
        sb.AppendLine(new string('-', 46));
        index = 1;
        foreach (var file in EnumerateFiles().OrderByDescending(f => f.Length).Take(15))
        {
            var pct = _scannedTotalBytes > 0 ? file.Length * 100d / _scannedTotalBytes : 0;
            sb.AppendLine($"{index,2}. {file.DisplaySize,12}  {pct,6:0.00} %  {file.Name}");
            sb.AppendLine($"    {file.FullPath}");
            index++;
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private IEnumerable<FileEntry> EnumerateFiles() => _root.DescendantsAndSelf().SelectMany(n => n.Files);

    private static DateTime LatestDate(FolderNode node)
    {
        var latest = node.Files.Count > 0 ? node.Files.Max(f => f.LastWriteTime) : DateTime.MinValue;
        foreach (var child in node.Children)
        {
            var childDate = LatestDate(child);
            if (childDate > latest) latest = childDate;
        }
        return latest;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(';') || value.Contains('"')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }
}