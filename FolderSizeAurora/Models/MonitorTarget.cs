using System;
using FolderSizeAurora.Services;

namespace FolderSizeAurora.Models;

public enum MonitorKind
{
    Folder,
    Drive
}

public enum MonitorLimitMode
{
    /// <summary>El limite se expresa en bytes.</summary>
    Bytes,

    /// <summary>El limite se expresa como porcentaje de uso.</summary>
    Percent
}

/// <summary>Regla de monitoreo: una carpeta o unidad con un limite de espacio.</summary>
public sealed class MonitorTarget
{
    public MonitorTarget(string path, MonitorKind kind, string label, long limitBytes, MonitorLimitMode mode)
    {
        Path = path;
        Kind = kind;
        Label = label;
        LimitBytes = limitBytes;
        Mode = mode;
    }

    public string Path { get; }
    public MonitorKind Kind { get; }
    public string Label { get; }

    /// <summary>Limite absoluto usado cuando <see cref="Mode"/> es Bytes.</summary>
    public long LimitBytes { get; }

    public MonitorLimitMode Mode { get; }

    /// <summary>Hasta cuándo silenciar alertas para esta regla (UTC). null = sin silenciar.</summary>
    public DateTime? SnoozeUntilUtc { get; set; }

    public string DisplayLimit => Mode == MonitorLimitMode.Bytes
        ? Formatter.Bytes(LimitBytes)
        : $"{LimitBytes:0} %";

    public string DisplayKind => Kind == MonitorKind.Drive ? "Unidad" : "Carpeta";

    public string ToId() => $"{Kind}|{Path}";

    public override string ToString() => $"{Label} ({DisplayKind})";
}

/// <summary>Alerta generada por el monitor de limites.</summary>
public sealed class AlertItem
{
    public AlertItem(DateTime timestamp, string path, string message, long currentBytes, long limitBytes, bool isError = false)
    {
        Timestamp = timestamp;
        Path = path;
        Message = message;
        CurrentBytes = currentBytes;
        LimitBytes = limitBytes;
        IsError = isError;
    }

    public DateTime Timestamp { get; }
    public string Path { get; }
    public string Message { get; }
    public long CurrentBytes { get; }
    public long LimitBytes { get; }
    public bool IsError { get; }
    public bool IsRead { get; set; }

    public string Time => Timestamp.ToString("HH:mm:ss");
    public string Date => Timestamp.ToString("dd/MM/yyyy HH:mm:ss");
    public string CurrentDisplay => IsError ? "-" : Formatter.Bytes(CurrentBytes);
    public string LimitDisplay => IsError ? "-" : Formatter.Bytes(LimitBytes);
}