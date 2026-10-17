using System.Collections.Generic;

namespace FolderSizeAurora.Models;

/// <summary>Elemento de grafico (barras horizontales): etiqueta, peso y color.</summary>
public sealed class ChartItem
{
    public ChartItem(string label, long bytes, double fraction, int colorIndex, string? tooltip = null)
    {
        Label = label;
        Bytes = bytes;
        Fraction = fraction;
        ColorIndex = colorIndex;
        Tooltip = tooltip ?? string.Empty;
    }

    public string Label { get; }
    public long Bytes { get; }
    public double Fraction { get; }
    public int ColorIndex { get; }
    public string Tooltip { get; }

    public string DisplaySize => Services.Formatter.Bytes(Bytes);
    public string DisplayPercent => $"{Fraction * 100:0.0} %";
}

/// <summary>Resultado completo de un escaneo.</summary>
public sealed class ScanResult
{
    public ScanResult(
        FolderNode root,
        long totalBytes,
        int totalFiles,
        int totalFolders,
        long skippedItems,
        TimeSpan elapsed,
        IReadOnlyList<ChartItem> topFolders,
        IReadOnlyList<ChartItem> topFiles,
        IReadOnlyList<ChartItem> byExtension)
    {
        Root = root;
        TotalBytes = totalBytes;
        TotalFiles = totalFiles;
        TotalFolders = totalFolders;
        SkippedItems = skippedItems;
        Elapsed = elapsed;
        TopFolders = topFolders;
        TopFiles = topFiles;
        ByExtension = byExtension;
    }

    public FolderNode Root { get; }
    public long TotalBytes { get; }
    public int TotalFiles { get; }
    public int TotalFolders { get; }
    public long SkippedItems { get; }
    public TimeSpan Elapsed { get; }
    public IReadOnlyList<ChartItem> TopFolders { get; }
    public IReadOnlyList<ChartItem> TopFiles { get; }
    public IReadOnlyList<ChartItem> ByExtension { get; }

    public string DisplayTotal => Services.Formatter.Bytes(TotalBytes);
    public string DisplayElapsed => Elapsed.TotalSeconds < 1
        ? $"{Elapsed.TotalMilliseconds:0} ms"
        : Elapsed.TotalSeconds < 60
            ? $"{Elapsed.TotalSeconds:0.0} s"
            : $"{(int)Elapsed.TotalMinutes} min {Elapsed.Seconds} s";
}

/// <summary>Progreso de escaneo para la interfaz.</summary>
public readonly record struct ScanProgress(string CurrentPath, long BytesSoFar, int FilesSoFar, int FoldersSoFar);