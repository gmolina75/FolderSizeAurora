using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FolderSizeAurora.Models;
using FolderSizeAurora.Services;
using FolderSizeAurora.ViewModels;

namespace FolderSizeAurora;

public partial class App : Application
{
    public const string ProtocolScheme = "foldersizeaurora";

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Registrar protocolo personalizado para toast actions
        RegisterProtocol();

        // Manejar activación por protocolo (si la app ya está corriendo)
        if (e.Args.Length > 0 && e.Args[0].StartsWith($"{ProtocolScheme}://"))
        {
            HandleProtocolActivation(e.Args[0]);
            base.OnStartup(e);
            return;
        }

        // CLI: si hay argumentos que no son protocolo, ejecutar en modo headless
        if (e.Args.Length > 0 && !e.Args[0].StartsWith($"{ProtocolScheme}://"))
        {
            RunCliAsync(e.Args).ConfigureAwait(false);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    private static async Task RunCliAsync(string[] args)
    {
        try
        {
            var scanner = new DirectoryScanner();
            var progress = new Progress<ScanSnapshot>();

            // Parse simple: foldersizeaurora scan "C:\path" --csv out.csv
            if (args.Length >= 2 && args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
            {
                var path = args[1].Trim('"');
                if (!Directory.Exists(path))
                {
                    Console.Error.WriteLine($"Ruta no encontrada: {path}");
                    return;
                }

                Console.WriteLine($"Escaneando: {path}");
                var result = await scanner.ScanAsync(path, false, progress, CancellationToken.None);

                Console.WriteLine($"Archivos: {result.TotalFiles}, Carpetas: {result.TotalFolders}, Tamaño: {Formatter.Bytes(result.TotalBytes)}");

                // Buscar opciones de exportación
                string? csvPath = null;
                string? jsonPath = null;
                string? reportPath = null;

                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i].Equals("--csv", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        csvPath = args[++i];
                    else if (args[i].Equals("--json", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        jsonPath = args[++i];
                    else if (args[i].Equals("--report", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        reportPath = args[++i];
                }

                var exportService = new ExportService(result.Root, result.TotalBytes);

                if (csvPath is not null)
                {
                    exportService.ExportCsv(csvPath);
                    Console.WriteLine($"CSV exportado a: {csvPath}");
                }
                if (jsonPath is not null)
                {
                    exportService.ExportJson(jsonPath);
                    Console.WriteLine($"JSON exportado a: {jsonPath}");
                }
                if (reportPath is not null)
                {
                    exportService.ExportReport(reportPath);
                    Console.WriteLine($"Reporte exportado a: {reportPath}");
                }

                return;
            }

            Console.WriteLine("Folder Size Aurora CLI");
            Console.WriteLine("Uso: FolderSizeAurora scan \"<ruta>\" [--csv <archivo>] [--json <archivo>] [--report <archivo>]");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void RegisterProtocol()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";
            var scheme = ProtocolScheme;

            using var classesKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Classes", true);
            using var schemeKey = classesKey?.CreateSubKey(scheme);
            if (schemeKey != null)
            {
                schemeKey.SetValue("", "URL:Folder Size Aurora Protocol");
                schemeKey.SetValue("URL Protocol", "");

                using var shellKey = schemeKey.CreateSubKey("shell\\open\\command");
                shellKey?.SetValue("", $"\"{exePath}\" \"%1\"");
            }
        }
        catch
        {
            // Silencioso: si falla, los botones del toast no harán nada
        }
    }

    private void HandleProtocolActivation(string uri)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(uri);
            // foldersizeaurora://openFolder?path=C:\Users\...
            if (decoded.Contains("action=openFolder"))
            {
                var pathStart = decoded.IndexOf("path=") + 5;
                var path = decoded.Substring(pathStart);
                Current.Dispatcher.BeginInvoke(() =>
                {
                    if (Current.MainWindow is MainWindow mw && mw.DataContext is ViewModels.MainViewModel vm)
                    {
                        vm.BrowseCommand.Execute(path);
                    }
                });
            }
            else if (decoded.Contains("action=snooze1h"))
            {
                var pathStart = decoded.IndexOf("path=") + 5;
                var path = decoded.Substring(pathStart);
                Current.Dispatcher.BeginInvoke(() =>
                {
                    if (Current.MainWindow is MainWindow mw && mw.DataContext is ViewModels.MainViewModel vm)
                    {
                        vm.SnoozeRule(path, TimeSpan.FromHours(1));
                    }
                });
            }
            else if (decoded.Contains("action=snooze24h"))
            {
                var pathStart = decoded.IndexOf("path=") + 5;
                var path = decoded.Substring(pathStart);
                Current.Dispatcher.BeginInvoke(() =>
                {
                    if (Current.MainWindow is MainWindow mw && mw.DataContext is ViewModels.MainViewModel vm)
                    {
                        vm.SnoozeRule(path, TimeSpan.FromHours(24));
                    }
                });
            }
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Un fallo no deberia cerrar la aplicacion; se informa y se continua.
        MessageBox.Show(
            $"Ocurrio un error inesperado:{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
            "Folder Size Aurora",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }
}