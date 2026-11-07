using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>Estado actual de una regla de monitoreo.</summary>
public sealed class MonitorStatus
{
    public MonitorStatus(string path, long usedBytes, long totalBytes, double usedPercent, bool exceeded, long limitBytes, string modeLabel, string error = "")
    {
        Path = path;
        UsedBytes = usedBytes;
        TotalBytes = totalBytes;
        UsedPercent = usedPercent;
        Exceeded = exceeded;
        LimitBytes = limitBytes;
        ModeLabel = modeLabel;
        Error = error;
    }

    public string Path { get; }
    public long UsedBytes { get; }
    public long TotalBytes { get; }
    public double UsedPercent { get; }
    public bool Exceeded { get; }
    public long LimitBytes { get; }
    public string ModeLabel { get; }
    public string Error { get; }

    public double BarFraction => TotalBytes > 0 ? Math.Clamp(UsedBytes / (double)TotalBytes, 0, 1) : 0;
    public string DisplayUsed => UsedBytes < 0 ? "-" : Formatter.Bytes(UsedBytes);
    public string DisplayTotal => TotalBytes > 0 ? Formatter.Bytes(TotalBytes) : "-";
    public string DisplayPercent => UsedBytes < 0 ? "-" : $"{UsedPercent:0.0} %";
    public string StateText => Error.Length > 0 ? "Error" : Exceeded ? "LIMITE SUPERADO" : "En rango";
    public bool HasError => Error.Length > 0;
}

/// <summary>
/// Verifica periodicamente el espacio usado por carpetas y unidades, y emite alertas
/// con enfriamiento (30 min) para no repetir la misma notificacion en cada ciclo.
/// </summary>
public sealed class SizeMonitorService : IDisposable
{
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(30);

    private readonly Dictionary<string, DateTime> _lastAlertAt = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;
    private int _busy;

    public SizeMonitorService() { }

    public bool IsRunning => _timer is not null;

    public IReadOnlyList<MonitorTarget> Targets { get; private set; } = Array.Empty<MonitorTarget>();

    public void Configure(IEnumerable<MonitorTarget> targets, int intervalMinutes, bool enabled)
    {
        Targets = targets?.ToList() ?? new List<MonitorTarget>();
        if (enabled && Targets.Count > 0) Start(intervalMinutes);
        else Stop();
    }

    public void Start(int intervalMinutes)
    {
        Stop();
        var period = TimeSpan.FromMinutes(Math.Clamp(intervalMinutes, 1, 1440));
        _timer = new Timer(_ => CheckNow(), null, TimeSpan.FromSeconds(2), period);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Ejecuta un ciclo inmediato (boton "Comprobar ahora").</summary>
    public void CheckNow()
    {
        // Evita ciclos solapados si el analisis tarda mas que el intervalo.
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;

        try
        {
            var snapshot = Targets.ToList();
            if (snapshot.Count == 0) return;

            var statuses = new List<MonitorStatus>(snapshot.Count);
            var alerts = new List<AlertItem>();

            foreach (var target in snapshot)
            {
                var status = Evaluate(target);
                if (status is null) continue;

                statuses.Add(status);

                if (status.HasError)
                {
                    continue;
                }

                if (status.Exceeded)
                {
                    if (ShouldRaise(target))
                    {
                        alerts.Add(new AlertItem(
                            DateTime.Now,
                            target.Path,
                            target.Mode == MonitorLimitMode.Bytes
                                ? $"Uso {Formatter.Bytes(status.UsedBytes)}, por encima del limite de {Formatter.Bytes(target.LimitBytes)}."
                                : $"Uso {status.UsedPercent:0.0} %, por encima del limite de {target.LimitBytes:0} %.",
                            status.UsedBytes,
                            target.LimitBytes));
                    }
                }
                else
                {
                    _lastAlertAt.Remove(target.Path);
                }
            }

            if (statuses.Count > 0) StatusesUpdated?.Invoke(statuses);

            foreach (var alert in alerts)
            {
                LimitExceeded?.Invoke(alert);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private bool ShouldRaise(MonitorTarget target)
    {
        var now = DateTime.Now;
        if (_lastAlertAt.TryGetValue(target.Path, out var last) && now - last < AlertCooldown)
            return false;

        _lastAlertAt[target.Path] = now;
        return true;
    }

    private static MonitorStatus? Evaluate(MonitorTarget target)
    {
        try
        {
            long used;
            long total;

            if (target.Kind == MonitorKind.Drive)
            {
                var drive = new DriveInfo(target.Path);
                total = drive.TotalSize;
                used = total - drive.AvailableFreeSpace;
            }
            else
            {
                var root = Path.GetPathRoot(target.Path);
                if (string.IsNullOrEmpty(root)) return null;
                total = new DriveInfo(root).TotalSize;
                used = SizeOf(target.Path);
            }

            if (used < 0) return null;

            var usedPercent = total > 0 ? used * 100d / total : 0;

            var exceeded = target.Mode == MonitorLimitMode.Bytes
                ? used >= target.LimitBytes
                : usedPercent >= target.LimitBytes;

            var modeLabel = target.Mode == MonitorLimitMode.Bytes
                ? "Limite: " + Formatter.Bytes(target.LimitBytes)
                : "Limite: " + target.LimitBytes + " %";

            return new MonitorStatus(target.Path, used, total, usedPercent, exceeded, target.LimitBytes, modeLabel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or DriveNotFoundException)
        {
            var modeLabel = target.Mode == MonitorLimitMode.Bytes
                ? "Limite: " + Formatter.Bytes(target.LimitBytes)
                : "Limite: " + target.LimitBytes + " %";
            return new MonitorStatus(target.Path, 0, 0, 0, false, target.LimitBytes, modeLabel, ex.Message);
        }
    }

    /// <summary>Tamano total de una carpeta (recorrido seguro, ignora junctions y symlinks).</summary>
    private static long SizeOf(string path)
    {
        if (!Directory.Exists(path)) return -1;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            long sum = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                try
                {
                    sum += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                    // Archivo bloqueado o eliminado durante el recorrido.
                }
            }
            return sum;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return -1;
        }
    }

    public event Action<IReadOnlyList<MonitorStatus>>? StatusesUpdated;
    public event Action<AlertItem>? LimitExceeded;

    public void Dispose() => Stop();
}