using System;

namespace FolderSizeAurora.Models;

/// <summary>
/// Fila plana del listado. Aplanar el arbol permite usar un ListView con GridView real:
/// cabeceras alineadas por construccion, orden por clic, virtualizacion y indentacion
/// controlada, sin el desfase entre la cabecera y las filas.
/// </summary>
public sealed class TreeRow
{
    public const int IndentWidth = 14;

    public TreeRow(FolderNode? folder, FileEntry? file, int depth, bool isExpanded)
    {
        Folder = folder;
        File = file;
        Depth = depth;
        IsExpanded = isExpanded;
    }

    public FolderNode? Folder { get; }
    public FileEntry? File { get; }
    public int Depth { get; }
    public bool IsExpanded { get; }

    public bool IsFolder => Folder is not null;

    public string Name => Folder?.Name ?? File?.Name ?? string.Empty;
    public string FullPath => Folder?.FullPath ?? File?.FullPath ?? string.Empty;

    public long Size => Folder?.Size ?? File?.Length ?? 0;
    public double Percentage => Folder?.Percentage ?? 0;
    public double BarFraction => Math.Clamp(Size / Math.Max(1, TotalBytes), 0, 1);

    public long TotalBytes { get; init; }

    public int ChildFolders => Folder?.Children.Count ?? 0;
    public int OwnFiles => Folder?.Files.Count ?? 1;
    public int TotalFiles => Folder?.FileCount ?? 1;
    public int TotalFolders => Folder?.FolderCount ?? 0;

    public bool HasChildren => ChildFolders > 0;

    /// <summary>
    /// Usa la fecha ya acumulada en el nodo. Recalcularla aqui con Files.Max(...)
    /// convertia cada repintado de la fila en un recorrido completo de la carpeta.
    /// </summary>
    public DateTime? Modified
    {
        get
        {
            if (File is not null) return File.LastWriteTime;
            if (Folder is null || Folder.LatestWriteTime == default) return null;
            return Folder.LatestWriteTime;
        }
    }

    public string DisplaySize => Services.Formatter.Bytes(Size);
    public string DisplayPercent => Percentage > 0 ? Percentage.ToString("0.00") : string.Empty;
    public string DisplayFiles => Folder is null ? string.Empty : Services.Formatter.Count(Folder.FileCount);
    public string DisplayFolders => Folder is null ? string.Empty : Services.Formatter.Count(Folder.FolderCount);
    public string DisplayModified => Modified is null ? "-" : Modified.Value.ToString("dd/MM/yy");
    public string DisplayKind => Folder is null ? "Archivo" : "Carpeta";

    /// <summary>Sangria acumulada en píxeles.</summary>
    public double Indent => Depth * IndentWidth;

    /// <summary>Contenido de la columna de nombre, para decidir si se recorta.</summary>
    public string Extension => File?.Extension ?? string.Empty;
}