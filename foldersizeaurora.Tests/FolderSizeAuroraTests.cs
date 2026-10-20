using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderSizeAurora.Models;
using FolderSizeAurora.Services;
using Xunit;

namespace FolderSizeAurora.Tests;

public class FormatterTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(512, "512 bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void Bytes_FormatsCorrectly(long value, string expected)
    {
        var result = Formatter.Bytes(value);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1 KB", 1024)]
    [InlineData("1.5 MB", 1572864)]
    [InlineData("2GB", 2147483648)]
    [InlineData("500", 500)]
    public void TryParseSize_ParsesCorrectly(string input, long expected)
    {
        var success = Formatter.TryParseSize(input, out var bytes);
        Assert.True(success);
        Assert.Equal(expected, bytes);
    }
}

public class MonitorTargetTests
{
    [Fact]
    public void SnoozeUntilUtc_CanBeSet()
    {
        var target = new MonitorTarget("C:\\Test", MonitorKind.Folder, "Test", 1000, MonitorLimitMode.Bytes);
        var snoozeTime = DateTime.UtcNow.AddHours(1);

        target.SnoozeUntilUtc = snoozeTime;

        Assert.Equal(snoozeTime, target.SnoozeUntilUtc);
    }

    [Fact]
    public void DisplayLimit_BytesMode_ShowsFormattedBytes()
    {
        var target = new MonitorTarget("C:\\Test", MonitorKind.Folder, "Test", 1024 * 1024, MonitorLimitMode.Bytes);
        Assert.Equal("1 MB", target.DisplayLimit);
    }

    [Fact]
    public void DisplayLimit_PercentMode_ShowsPercent()
    {
        var target = new MonitorTarget("C:\\Test", MonitorKind.Folder, "Test", 90, MonitorLimitMode.Percent);
        Assert.Equal("90 %", target.DisplayLimit);
    }
}

public class AlertItemTests
{
    [Fact]
    public void IsRead_DefaultsToFalse()
    {
        var alert = new AlertItem(DateTime.Now, "C:\\Test", "Test", 100, 1000);
        Assert.False(alert.IsRead);
    }

    [Fact]
    public void IsRead_CanBeSet()
    {
        var alert = new AlertItem(DateTime.Now, "C:\\Test", "Test", 100, 1000);
        alert.IsRead = true;
        Assert.True(alert.IsRead);
    }

    [Fact]
    public void CurrentDisplay_ShowsFormattedBytes()
    {
        var alert = new AlertItem(DateTime.Now, "C:\\Test", "Test", 1024 * 1024, 1000);
        Assert.Equal("1 MB", alert.CurrentDisplay);
    }

    [Fact]
    public void CurrentDisplay_ErrorShowsDash()
    {
        var alert = new AlertItem(DateTime.Now, "C:\\Test", "Test", 0, 0, true);
        Assert.Equal("-", alert.CurrentDisplay);
    }
}

public class AppSettingsTests
{
    [Fact]
    public void MonitorIntervalMinutes_DefaultsTo5()
    {
        var settings = new AppSettings();
        Assert.Equal(5, settings.MonitorIntervalMinutes);
    }

    [Fact]
    public void MinimizeToTray_DefaultsToTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.MinimizeToTray);
    }

    [Fact]
    public void CheckForUpdates_DefaultsToTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.CheckForUpdates);
    }
}

public class DirectoryScannerTests
{
    [Fact]
    public async Task ScanAsync_ThrowsOnNonExistentPath()
    {
        var scanner = new DirectoryScanner();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            scanner.ScanAsync(path, false, null, CancellationToken.None));
    }

    [Fact]
    public async Task ScanAsync_WorksOnEmptyTempFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"FolderSizeAurora_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var scanner = new DirectoryScanner();
            var result = await scanner.ScanAsync(tempDir, false, null, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(0, result.TotalFiles);
            Assert.Equal(0, result.TotalFolders);
            Assert.Equal(0, result.TotalBytes);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

public class SizeMonitorServiceTests
{
    [Fact]
    public void Configure_EmptyTargets_DoesNotStartTimer()
    {
        var service = new SizeMonitorService();
        service.Configure(Array.Empty<MonitorTarget>(), 5, true);

        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Configure_WithTargetsAndEnabled_StartsTimer()
    {
        var service = new SizeMonitorService();
        var targets = new[] { new MonitorTarget("C:\\", MonitorKind.Drive, "C:", 100, MonitorLimitMode.Percent) };

        service.Configure(targets, 5, true);

        Assert.True(service.IsRunning);
    }

    [Fact]
    public void Configure_WithTargetsButDisabled_DoesNotStartTimer()
    {
        var service = new SizeMonitorService();
        var targets = new[] { new MonitorTarget("C:\\", MonitorKind.Drive, "C:", 100, MonitorLimitMode.Percent) };

        service.Configure(targets, 5, false);

        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Stop_StopsTimer()
    {
        var service = new SizeMonitorService();
        var targets = new[] { new MonitorTarget("C:\\", MonitorKind.Drive, "C:", 100, MonitorLimitMode.Percent) };

        service.Configure(targets, 5, true);
        service.Stop();

        Assert.False(service.IsRunning);
    }
}

public class UpdateServiceTests
{
    [Fact]
    public void NormalizeVersion_HandlesVPrefix()
    {
        var service = new UpdateService();
        var version = typeof(UpdateService)
            .GetMethod("NormalizeVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { "v1.2.3" }) as Version;

        Assert.Equal(new Version(1, 2, 3), version);
    }

    [Fact]
    public void NormalizeVersion_HandlesNoPrefix()
    {
        var version = typeof(UpdateService)
            .GetMethod("NormalizeVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { "1.2.3" }) as Version;

        Assert.Equal(new Version(1, 2, 3), version);
    }
}

public class ExportServiceTests
{
    [Fact]
    public void ExportCsv_CreatesFileWithHeaders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"FolderSizeAurora_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var root = new FolderNode("Test", tempDir, isRoot: true);
            var child = new FolderNode("Child", Path.Combine(tempDir, "Child"));
            root.Children.Add(child);

            var exportService = new ExportService(root, 0);
            var csvPath = Path.Combine(tempDir, "test.csv");

            exportService.ExportCsv(csvPath);

            Assert.True(File.Exists(csvPath));
            var content = File.ReadAllText(csvPath);
            Assert.Contains("Tipo;Ruta;Nombre;Tamano (bytes)", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}