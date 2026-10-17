using System.Windows;
using System.Windows.Threading;

namespace FolderSizeAurora;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
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