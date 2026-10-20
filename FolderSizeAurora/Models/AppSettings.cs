using System;
using System.Collections.Generic;

namespace FolderSizeAurora.Models;

public enum SortMode
{
    Size,
    Name,
    Files,
    Date
}

public enum TypeFilter
{
    All,
    Images,
    Video,
    Audio,
    Documents,
    Archives,
    Code,
    DiskImages,
    Executables
}

/// <summary>Ajustes de la aplicacion, serializados a JSON en %APPDATA%.</summary>
public sealed class AppSettings
{
    public bool DarkTheme { get; set; }
    public bool CompactRows { get; set; }
    public bool KioskMode { get; set; }
    public string? LastPath { get; set; }
    public SortMode SortMode { get; set; } = SortMode.Size;
    public bool SortAscending { get; set; }
    public TypeFilter TypeFilter { get; set; } = TypeFilter.All;
    public long MinSizeBytes { get; set; }
    public bool IncludeSystemHidden { get; set; }

    // Monitoreo
    public bool MonitoringEnabled { get; set; }
    public int MonitorIntervalMinutes { get; set; } = 5;

    // Canales de notificacion
    public bool NotifyInApp { get; set; } = true;
    public bool NotifySound { get; set; } = true;

    public bool NotifyEmail { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string EmailFrom { get; set; } = string.Empty;
    public string EmailTo { get; set; } = string.Empty;

    public bool NotifyWebhook { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;

    public List<MonitorRule> MonitorRules { get; set; } = new();
}

/// <summary>Regla serializable (independiente del MonitorTarget en memoria).</summary>
public sealed class MonitorRule
{
    public string Path { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public MonitorKind Kind { get; set; } = MonitorKind.Folder;
    public long LimitBytes { get; set; }
    public MonitorLimitMode Mode { get; set; } = MonitorLimitMode.Bytes;
}