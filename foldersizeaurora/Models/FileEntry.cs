using System;

namespace FolderSizeAurora.Models;

/// <summary>Archivo individual dentro del arbol.</summary>
public sealed class FileEntry
{
    public FileEntry(string name, string fullPath, long length, string extension, DateTime lastWriteTime)
    {
        Name = name;
        FullPath = fullPath;
        Length = length;
        Extension = string.IsNullOrEmpty(extension) ? "(sin extension)" : extension.ToLowerInvariant();
        LastWriteTime = lastWriteTime;
    }

    public string Name { get; }
    public string FullPath { get; }
    public long Length { get; }
    public string Extension { get; }
    public DateTime LastWriteTime { get; }

    public string DisplaySize => Services.Formatter.Bytes(Length);
    public string DisplayDate => LastWriteTime == default ? "-" : LastWriteTime.ToString("dd/MM/yyyy HH:mm");
}