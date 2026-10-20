using System;
using System.Windows;
using System.Windows.Threading;

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
        }

        base.OnStartup(e);
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