using System;
using System.IO;
using FolderSizeAurora.Services;

namespace FolderSizeAurora.Models;

public enum DriveKind
{
    Fixed,
    Removable,
    Network,
    Optical
}

public sealed class DriveItem
{
    public DriveItem(string name, string root, DriveKind kind, long totalBytes, long freeBytes, string formatLabel)
    {
        Name = name;
        Root = root;
        Kind = kind;
        TotalBytes = totalBytes;
        FreeBytes = freeBytes;
        FormatLabel = formatLabel;
    }

    public string Name { get; }
    public string Root { get; }
    public DriveKind Kind { get; }
    public long TotalBytes { get; }
    public long FreeBytes { get; }
    public string FormatLabel { get; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedFraction => TotalBytes > 0 ? (double)UsedBytes / TotalBytes : 0;
    public double UsedPercent => UsedFraction * 100;
    public double FreePercent => 100 - UsedPercent;

    public string DisplayUsed => Formatter.Bytes(UsedBytes);
    public string DisplayFree => Formatter.Bytes(FreeBytes);
    public string DisplayTotal => Formatter.Bytes(TotalBytes);
    public string DisplayPercent => $"{UsedPercent:0} % usado";
    public string DisplayVolume => string.IsNullOrWhiteSpace(FormatLabel) ? Name : $"{Name} ({FormatLabel})";

    /// <summary>Etiqueta ya compuesta para evitar StringFormat en XAML.</summary>
    public string DisplayFreeLabel => $"Libre {DisplayFree} de {DisplayTotal}";

    public static DriveItem? Create(DriveInfo info)
    {
        try
        {
            // Fuerza la lectura de datos; puede lanzar en unidades sin medio.
            _ = info.TotalSize;
            _ = info.AvailableFreeSpace;

            var kind = info.DriveType switch
            {
                DriveType.Fixed => DriveKind.Fixed,
                DriveType.Removable => DriveKind.Removable,
                DriveType.Network => DriveKind.Network,
                DriveType.CDRom => DriveKind.Optical,
                _ => DriveKind.Removable
            };

            return new DriveItem(
                info.Name,
                info.RootDirectory.FullName,
                kind,
                info.TotalSize,
                info.AvailableFreeSpace,
                info.DriveFormat);
        }
        catch
        {
            // Unidad no disponible (sin medio, permisos, etc.).
            return null;
        }
    }
}