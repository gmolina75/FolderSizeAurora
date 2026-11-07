using System;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>
/// Servicio para el icono en la bandeja del sistema (system tray).
/// Permite minimizar la app a la bandeja, mostrar menú contextual y restaurar.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly Window _mainWindow;
    private readonly AppSettings _settings;
    private readonly Action _onShowWindow;
    private readonly Action _onExit;
    private bool _isMinimizedToTray;

    public TrayService(Window mainWindow, AppSettings settings, Action onShowWindow, Action onExit)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _onShowWindow = onShowWindow ?? throw new ArgumentNullException(nameof(onShowWindow));
        _onExit = onExit ?? throw new ArgumentNullException(nameof(onExit));

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Folder Size Aurora",
            Visibility = _settings.MinimizeToTray ? Visibility.Visible : Visibility.Collapsed,
        };

        // Icono: usamos el icono de la aplicación
        try
        {
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "");
        }
        catch
        {
            // Si falla, TaskbarIcon usa uno por defecto
        }

        // Menú contextual
        var contextMenu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "Mostrar Folder Size Aurora" };
        showItem.Click += (_, _) => ShowWindow();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Salir" };
        exitItem.Click += (_, _) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;

        // Doble clic = mostrar ventana
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();

        // Manejar cierre de la ventana principal
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.StateChanged += OnMainWindowStateChanged;
    }

    public void UpdateVisibility()
    {
        _trayIcon.Visibility = _settings.MinimizeToTray ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowBalloon(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        _trayIcon.ShowBalloonTip(title, message, icon);
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow.WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            _mainWindow.Hide();
            _isMinimizedToTray = true;
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.MinimizeToTray && !_isMinimizedToTray)
        {
            // Si no estamos ya minimizados a la bandeja, cancelar el cierre y minimizar
            e.Cancel = true;
            _mainWindow.Hide();
            _isMinimizedToTray = true;
        }
    }

    private void ShowWindow()
    {
        if (_isMinimizedToTray)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _isMinimizedToTray = false;
        }
        _onShowWindow();
    }

    private void ExitApplication()
    {
        _mainWindow.Closing -= OnMainWindowClosing;
        _mainWindow.StateChanged -= OnMainWindowStateChanged;
        _onExit();
    }

    public void Dispose()
    {
        _mainWindow.Closing -= OnMainWindowClosing;
        _mainWindow.StateChanged -= OnMainWindowStateChanged;
        _trayIcon?.Dispose();
    }
}