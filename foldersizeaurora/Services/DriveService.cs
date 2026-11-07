using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>Enumerate unidades locales y su ocupacion actual.</summary>
public sealed class DriveService
{
    public IReadOnlyList<DriveItem> GetDrives()
    {
        var list = new List<DriveItem>();

        try
        {
            foreach (var info in DriveInfo.GetDrives())
            {
                var item = DriveItem.Create(info);
                if (item is not null) list.Add(item);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Enumeracion de unidades no disponible en este momento.
        }

        return list
            .OrderByDescending(d => d.Kind == DriveKind.Fixed)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Espacio libre de la unidad que contiene la ruta. -1 si no se puede determinar.</summary>
    public long GetFreeSpaceFor(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return -1;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return -1;
        }
    }

    public long GetTotalSpaceFor(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return -1;
            return new DriveInfo(root).TotalSize;
        }
        catch
        {
            return -1;
        }
    }
}