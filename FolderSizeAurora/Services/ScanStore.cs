using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>
/// Persistencia de escaneos en SQLite embebido. Permite recargar un árbol
/// sin reescanear y comparar tamaños entre versiones.
/// </summary>
public sealed class ScanStore : IDisposable
{
    private readonly string _dbPath;
    private Microsoft.Data.Sqlite.SqliteConnection? _conn;

    public ScanStore(string? dbPath = null)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderSizeAurora");
        Directory.CreateDirectory(baseDir);
        _dbPath = dbPath ?? Path.Combine(baseDir, "scans.db");
    }

    public async Task EnsureSchemaAsync()
    {
        _conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        await _conn.OpenAsync();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS scans (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    root_path TEXT NOT NULL,
    total_bytes INTEGER NOT NULL,
    total_files INTEGER NOT NULL,
    total_folders INTEGER NOT NULL,
    skipped INTEGER NOT NULL,
    elapsed_ms INTEGER NOT NULL,
    scanned_at TEXT NOT NULL,
    json TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_scans_path ON scans(root_path);
CREATE INDEX IF NOT EXISTS idx_scans_at ON scans(scanned_at);
";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveAsync(ScanResult result)
    {
        if (_conn is null) await EnsureSchemaAsync();
        var json = JsonSerializer.Serialize(result.Root, new JsonSerializerOptions { WriteIndented = false });
        await using var cmd = _conn!.CreateCommand();
        cmd.CommandText = @"
INSERT INTO scans (root_path, total_bytes, total_files, total_folders, skipped, elapsed_ms, scanned_at, json)
VALUES (@p, @b, @f, @fo, @s, @e, @d, @j);
";
        cmd.Parameters.AddWithValue("@p", result.Root.FullPath);
        cmd.Parameters.AddWithValue("@b", result.TotalBytes);
        cmd.Parameters.AddWithValue("@f", result.TotalFiles);
        cmd.Parameters.AddWithValue("@fo", result.TotalFolders);
        cmd.Parameters.AddWithValue("@s", result.SkippedItems);
        cmd.Parameters.AddWithValue("@e", (long)result.Elapsed.TotalMilliseconds);
        cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("O"));
        cmd.Parameters.AddWithValue("@j", json);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ScanHistoryItem>> ListAsync(string? rootPath = null)
    {
        if (_conn is null) await EnsureSchemaAsync();
        await using var cmd = _conn!.CreateCommand();
        if (rootPath is null)
        {
            cmd.CommandText = "SELECT id, root_path, total_bytes, total_files, total_folders, scanned_at FROM scans ORDER BY scanned_at DESC LIMIT 500;";
        }
        else
        {
            cmd.CommandText = "SELECT id, root_path, total_bytes, total_files, total_folders, scanned_at FROM scans WHERE root_path = @p ORDER BY scanned_at DESC LIMIT 500;";
            cmd.Parameters.AddWithValue("@p", rootPath);
        }
        var list = new List<ScanHistoryItem>();
        // ExecuteDbDataReader para lectura
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ScanHistoryItem(
                r.GetInt32(0),
                r.GetString(1),
                r.GetInt64(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetString(5)));
        }
        return list;
    }

    public async Task<FolderNode?> LoadRootAsync(int scanId)
    {
        if (_conn is null) await EnsureSchemaAsync();
        await using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT json FROM scans WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", scanId);
        var json = await cmd.ExecuteScalarAsync() as string;
        if (json is null) return null;
        return JsonSerializer.Deserialize<FolderNode>(json);
    }

    public void Dispose()
    {
        _conn?.Dispose();
        _conn = null;
    }
}

public sealed class ScanHistoryItem
{
    public ScanHistoryItem(int id, string rootPath, long totalBytes, int totalFiles, int totalFolders, string scannedAt)
    {
        Id = id;
        RootPath = rootPath;
        TotalBytes = totalBytes;
        TotalFiles = totalFiles;
        TotalFolders = totalFolders;
        ScannedAt = scannedAt;
    }

    public int Id { get; }
    public string RootPath { get; }
    public long TotalBytes { get; }
    public int TotalFiles { get; }
    public int TotalFolders { get; }
    public string ScannedAt { get; }
    public string DisplayTotal => Formatter.Bytes(TotalBytes);
    public string DisplayDate
    {
        get
        {
            if (DateTime.TryParse(ScannedAt, out var dt)) return dt.ToString("dd/MM/yy HH:mm");
            return ScannedAt;
        }
    }
}