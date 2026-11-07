using System;
using System.Globalization;

namespace FolderSizeAurora.Services;

/// <summary>Formateo de tamanos, conteos y duraciones al estilo de las herramientas de disco.</summary>
public static class Formatter
{
    private static readonly string[] Units = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB" };

    /// <summary>Formato jerarquico (base 1024) con precision adaptativa.</summary>
    public static string Bytes(long value)
    {
        if (value < 0) return "-" + Bytes(-value);

        double size = value;
        int unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024d;
            unit++;
        }

        return unit switch
        {
            0 => string.Format(CultureInfo.InvariantCulture, "{0:0} {1}", size, Units[unit]),
            1 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", size, Units[unit]),
            _ => string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, Units[unit])
        };
    }

    /// <summary>Parsea entradas como "1.5 GB", "500MB", "2048". Devuelve false si no es valido.</summary>
    public static bool TryParseSize(string? text, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var cleaned = text.Trim().Replace(",", ".").Replace(" ", string.Empty).ToUpperInvariant();

        int unitIndex = 0;
        var multipliers = new[] { 1L, 1024L, 1024L * 1024, 1024L * 1024 * 1024, 1024L * 1024 * 1024 * 1024, 1024L * 1024 * 1024 * 1024 * 1024 };

        // Units[0] = "bytes", Units[1] = "KB", etc.
        for (int i = 1; i < Units.Length; i++)
        {
            var unit = Units[i];
            if (cleaned.EndsWith(unit, StringComparison.Ordinal))
            {
                cleaned = cleaned[..^unit.Length];
                unitIndex = i;
                break;
            }
        }

        if (unitIndex >= multipliers.Length) unitIndex = multipliers.Length - 1;

        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return false;

        double result = number * multipliers[unitIndex];
        if (result < 0 || result > long.MaxValue) return false;

        bytes = (long)Math.Round(result);
        return true;
    }

    public static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Duration(TimeSpan value) => value.TotalSeconds < 1
        ? $"{value.TotalMilliseconds:0} ms"
        : value.TotalSeconds < 60
            ? $"{value.TotalSeconds:0.0} s"
            : value.TotalMinutes < 60
                ? $"{(int)value.TotalMinutes} min {value.Seconds} s"
                : $"{(int)value.TotalHours} h {value.Minutes} min";
}