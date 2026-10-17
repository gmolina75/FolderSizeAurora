using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>Guarda y carga los ajustes en %APPDATA%\FolderSizeAurora\settings.json.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SettingsPath { get; }

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FolderSizeAurora");
        SettingsPath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    // Saneos defensivos: valores fuera de rango romperian la UI.
                    settings.MonitorIntervalMinutes = Math.Clamp(settings.MonitorIntervalMinutes, 1, 1440);
                    settings.MinSizeBytes = Math.Max(0, settings.MinSizeBytes);
                    settings.MonitorRules ??= new();
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Ajustes corruptos: se usan los valores por defecto.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin permisos: la app sigue funcionando con los valores en memoria.
        }
    }
}