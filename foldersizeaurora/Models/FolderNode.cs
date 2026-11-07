using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FolderSizeAurora.Models;

/// <summary>Carpeta del arbol, con totales agregados de forma incremental.</summary>
public sealed class FolderNode : INotifyPropertyChanged
{
    private double _percentage;
    private bool _isExpanded;

    public FolderNode(string name, string fullPath, bool isRoot = false)
    {
        Name = name;
        FullPath = fullPath;
        IsRoot = isRoot;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsRoot { get; }

    /// <summary>Tamano total en bytes: suma de archivos propios + subcarpetas.</summary>
    public long Size { get; set; }

    /// <summary>Bytes de archivos que residen directamente en esta carpeta.</summary>
    public long DirectSize { get; set; }

    public int FileCount { get; set; }
    public int FolderCount { get; set; }

    public List<FileEntry> Files { get; } = new();
    public List<FolderNode> Children { get; } = new();

    /// <summary>
    /// Fecha mas reciente de los archivos PROPIOS de la carpeta, actualizada mientras
    /// se escanea. Antes se calculaba con Files.Max(...) cada vez que un binding leia
    /// la columna de modificacion: eso es recorrer todos los archivos de la carpeta en
    /// cada repintado de la fila. Con una unidad grande, expandir congelaba la ventana.
    /// </summary>
    public DateTime LatestWriteTime { get; set; }

    private IReadOnlyList<object>? _items;

    /// <summary>
    /// Lista unificada de subcarpetas y archivos que consume el TreeView.
    /// Se reconstruye al ordenar, para intercalar carpetas y archivos por tamano.
    /// </summary>
    public IReadOnlyList<object> Items => _items ??= BuildItems();

    public void RebuildItems(SortMode mode)
    {
        var merged = new List<object>(Children.Count + Files.Count);
        merged.AddRange(Children);
        merged.AddRange(Files);

        merged = mode switch
        {
            SortMode.Name => merged.OrderBy(ItemKey, StringComparer.CurrentCultureIgnoreCase).ToList(),
            SortMode.Files => merged.OrderBy(ItemSize).ToList(),
            SortMode.Date => merged.OrderByDescending(ItemDate).ToList(),
            _ => merged.OrderByDescending(ItemSize).ToList()
        };

        if (mode != SortMode.Date && !DescendingDefault(mode)) merged.Reverse();

        _items = merged;
        OnPropertyChanged(nameof(Items));
    }

    private bool DescendingDefault(SortMode mode) => mode == SortMode.Size;

    private static long ItemSize(object item) => item switch
    {
        FolderNode f => f.Size,
        FileEntry file => file.Length,
        _ => 0
    };

    private static DateTime ItemDate(object item) => item switch
    {
        FileEntry file => file.LastWriteTime,
        FolderNode node => node.LatestWriteTime,
        _ => DateTime.MinValue
    };

    private static string ItemKey(object item) => item switch
    {
        FolderNode f => f.Name,
        FileEntry file => file.Name,
        _ => string.Empty
    };

    private IReadOnlyList<object> BuildItems()
    {
        var merged = new List<object>(Children.Count + Files.Count);
        merged.AddRange(Children);
        merged.AddRange(Files);
        return merged;
    }

    /// <summary>Fraccion 0-1 del total escaneado, para las barras.</summary>
    public double Fraction => Math.Clamp(Percentage / 100d, 0, 1);

    /// <summary>Porcentaje respecto al total escaneado (0-100).</summary>
    public double Percentage
    {
        get => _percentage;
        set
        {
            if (Math.Abs(_percentage - value) < 0.0001) return;
            _percentage = value;
            OnPropertyChanged();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public string DisplaySize => Services.Formatter.Bytes(Size);
    public string DisplayDirectSize => Services.Formatter.Bytes(DirectSize);
    public string DisplayCounts => $"{Services.Formatter.Count(FileCount)} archivos / {Services.Formatter.Count(FolderCount)} carpetas";
    public string DisplayPercentage => Percentage <= 0 ? string.Empty : $"{Percentage:0.00} %";

    public void RefreshDisplay() => OnPropertyChanged(null);

    public void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(DisplaySize));
        OnPropertyChanged(nameof(DisplayDirectSize));
        OnPropertyChanged(nameof(DisplayCounts));
        OnPropertyChanged(nameof(DisplayPercentage));
    }

    /// <summary>Recorre el arbol en profundidad, sin materializar la lista.</summary>
    public IEnumerable<FolderNode> DescendantsAndSelf()
    {
        var stack = new Stack<FolderNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (int i = node.Children.Count - 1; i >= 0; i--)
                stack.Push(node.Children[i]);
        }
    }

    public void RecalculateCounts()
    {
        FileCount = Files.Count;
        FolderCount = 0;
        foreach (var child in Children)
        {
            child.RecalculateCounts();
            FolderCount += child.FolderCount + 1;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}