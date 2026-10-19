using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FolderSizeAurora.Helpers;
using FolderSizeAurora.Models;
using FolderSizeAurora.Services;

namespace FolderSizeAurora.ViewModels;

public sealed class OptionItem<T>
{
    public OptionItem(T value, string label)
    {
        Value = value;
        Label = label;
    }

    public T Value { get; }
    public string Label { get; }

    public override string ToString() => Label;
}

public sealed class MonitorRow
{
    public MonitorRow(MonitorTarget target, string stateText, string used, string limit, double fraction, bool exceeded)
    {
        Target = target;
        StateText = stateText;
        DisplayUsed = used;
        DisplayLimit = limit;
        BarFraction = fraction;
        Exceeded = exceeded;
    }

    public MonitorTarget Target { get; }
    public string Label => Target.Label;
    public string Path => Target.Path;
    public string KindText => Target.DisplayKind;
    public string StateText { get; }
    public string DisplayUsed { get; }
    public string DisplayLimit { get; }
    public double BarFraction { get; }
    public bool Exceeded { get; }
    public string KindLine => $"{Target.DisplayKind} · {Target.DisplayLimit}";
}

public sealed class NotificationRow
{
    public NotificationRow(DateTime time, string channel, bool success, string detail)
    {
        Time = time;
        Channel = channel;
        Success = success;
        Detail = detail;
    }

    public DateTime Time { get; }
    public string Channel { get; }
    public bool Success { get; }
    public string Detail { get; }
    public string TimeText => Time.ToString("HH:mm:ss");
    public string StatusText => Success ? "OK" : "Fallo";
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SettingsService _settingsService = new();
    private readonly DirectoryScanner _scanner = new();
    private readonly DriveService _drives = new();
    private readonly SizeMonitorService _monitor = new();
    private readonly NotificationService _notifications;
    private readonly HashSet<string> _expandedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _searchTimer;

    private AppSettings _settings;
    private CancellationTokenSource? _cts;
    private ScanResult? _result;
    private FolderNode? _viewRoot;

    /// <summary>Raíz del escaneo original, para navegar sin re-escanear.</summary>
    private FolderNode? _scanRoot;

    /// <summary>Capa actual mostrada. Null = vista completa del escaneo.</summary>
    private FolderNode? _currentNode;

    /// <summary>Pila de navegación para el breadcrumb y volver atrás.</summary>
    private readonly Stack<FolderNode> _navigationStack = new();

    private TreeRow? _selectedRow;
    private DriveItem? _selectedDrive;
    private MonitorRow? _selectedMonitor;
    private AlertItem? _toast;
    private string _toastMessage = string.Empty;
    private string _toastPath = string.Empty;
    private bool _isExportMenuOpen;

    private bool _isDark;
    private bool _isScanning;
    private bool _isSidebarCollapsed;
    private string _statusText = "Listo. Elige una carpeta o pulsa Escanear en una unidad.";
    private string _currentPath = string.Empty;
    private string _searchText = string.Empty;
    private double _progress;
    private long _scannedBytes;
    private int _scannedFiles;
    private int _scannedFolders;
    private long _minSizeBytes;
    private TypeFilter _typeFilter = TypeFilter.All;
    private SortMode _sortMode = SortMode.Size;
    private bool _sortAscending;
    private bool _includeSystemHidden;
    private bool _monitoringEnabled;
    private int _monitorIntervalMinutes = 5;
    private string _scanSummary = "Sin analisis todavia.";
    private string _selectionTitle = "Sin seleccion";
    private string _selectionMeta = "Pulsa una fila del arbol para ver su detalle.";
    private string _selectionPath = string.Empty;
    private AlertItem? _selectedAlert;

    public MainViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _settings = _settingsService.Load();
        _notifications = new NotificationService(_settings, (msg, _) => Post(() => StatusText = msg));

        _searchTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(220),
            DispatcherPriority.Background,
            OnSearchTick,
            dispatcher);

        TypeFilters = new ObservableCollection<OptionItem<TypeFilter>>
        {
            new(TypeFilter.All, "Todos"),
            new(TypeFilter.Images, "Imagenes"),
            new(TypeFilter.Video, "Video"),
            new(TypeFilter.Audio, "Audio"),
            new(TypeFilter.Documents, "Documentos"),
            new(TypeFilter.Archives, "Comprimidos"),
            new(TypeFilter.Code, "Codigo"),
            new(TypeFilter.DiskImages, "Imagenes de disco"),
            new(TypeFilter.Executables, "Ejecutables")
        };

        SortModes = new ObservableCollection<OptionItem<SortMode>>
        {
            new(SortMode.Size, "Tamano"),
            new(SortMode.Name, "Nombre"),
            new(SortMode.Files, "Cantidad de archivos"),
            new(SortMode.Date, "Ultima modificacion")
        };

        SizePresets = new ObservableCollection<OptionItem<long>>
        {
            new(0L, "Sin minimo"),
            new(1024L, "1 KB"),
            new(100L * 1024, "100 KB"),
            new(1024L * 1024, "1 MB"),
            new(10L * 1024 * 1024, "10 MB"),
            new(100L * 1024 * 1024, "100 MB"),
            new(1024L * 1024 * 1024, "1 GB")
        };

        IntervalPresets = new ObservableCollection<OptionItem<int>>
        {
            new(1, "Cada minuto"),
            new(5, "Cada 5 minutos"),
            new(15, "Cada 15 minutos"),
            new(30, "Cada 30 minutos"),
            new(60, "Cada hora"),
            new(360, "Cada 6 horas"),
            new(1440, "Cada dia")
        };

        BrowseCommand = new AsyncRelayCommand(BrowseFolderAsync, () => !IsScanning);
        RescanCommand = new AsyncRelayCommand(RescanAsync, () => !IsScanning && !string.IsNullOrWhiteSpace(CurrentPath));
        CancelCommand = new RelayCommand(Cancel, () => IsScanning);
        ExportCsvCommand = new RelayCommand((Action)(() => Export("csv")), () => _result is not null);
        ExportJsonCommand = new RelayCommand((Action)(() => Export("json")), () => _result is not null);
        ExportReportCommand = new RelayCommand((Action)(() => Export("report")), () => _result is not null);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        RefreshDrivesCommand = new RelayCommand(RefreshDrives);
        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
        ScanSelectedDriveCommand = new AsyncRelayCommand(p => ScanAsync((p as DriveItem)?.Root));

        ToggleRowCommand = new RelayCommand(p => ToggleRow(p as TreeRow), p => (p as TreeRow)?.HasChildren == true);
        ExpandAllCommand = new RelayCommand(ExpandAll, () => _viewRoot is not null);
        CollapseAllCommand = new RelayCommand(CollapseAll, () => _viewRoot is not null);
        SortByCommand = new RelayCommand(SortBy);

        // Menu contextual de la fila. Todo lo que se puede hacer con una carpeta o un
        // archivo que ya esta en pantalla, sin salir de la aplicacion.
        OpenFolderInExplorerCommand = new RelayCommand(p => Launch("explorer.exe", $"\"{FolderPathOf(p)}\""), p => IsFolderRow(p));
        OpenFileCommand = new RelayCommand(p => Launch(RowPathOf(p), string.Empty), p => !IsFolderRow(p) && RowPathOf(p) is not null);
        RevealInExplorerCommand = new RelayCommand(p => Launch("explorer.exe", $"/select,\"{RowPathOf(p)}\""), p => !IsFolderRow(p) && RowPathOf(p) is not null);
        OpenTerminalCommand = new RelayCommand(OpenTerminal, p => DirectoryOf(p) is not null);
        // El canExecute recibe el parametro: el constructor que admite CommandParameter
        // es el que toma Func<object?, bool>, no el que toma Func<bool>.
        ScanFolderCommand = new AsyncRelayCommand(p => ScanAsync(RowPathOf(p)), p => !IsScanning);
        WatchFolderCommand = new RelayCommand(p => WatchFolder(p as TreeRow), p => IsFolderRow(p));
        CopyPathCommand = new RelayCommand(p => CopyPath(p as TreeRow), p => RowPathOf(p) is not null);
        ToggleExportMenuCommand = new RelayCommand(() => IsExportMenuOpen = !IsExportMenuOpen);
        NavigateIntoCommand = new RelayCommand(NavigateInto, p => p is TreeRow row && row.IsFolder && !IsScanning);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => CanNavigateUp);

        AddFolderRuleCommand = new RelayCommand(AddFolderRule, () => !IsScanning);
        AddDriveRuleCommand = new RelayCommand((object? p) => AddDriveRule(p as DriveItem), p => p is DriveItem);
        RemoveRuleCommand = new RelayCommand(RemoveSelectedRule, () => SelectedMonitor is not null);
        CheckNowCommand = new RelayCommand(RefreshMonitorNow, () => MonitorRows.Count > 0);
        ClearAlertsCommand = new RelayCommand(ClearAlerts, () => Alerts.Count > 0);
        DismissToastCommand = new RelayCommand(() => Toast = null);
        SendTestNotificationCommand = new AsyncRelayCommand(SendTestNotificationAsync);
        OpenAlertLocationCommand = new RelayCommand(OpenAlertLocation, () => SelectedAlert?.Path is not null);

        _monitor.StatusesUpdated += statuses => Post(() => ApplyMonitorStatuses(statuses));
        _monitor.LimitExceeded += alert => Post(() => _ = HandleAlertAsync(alert));

        RestoreSettings();
        RefreshDrives();
    }

    // ---------------------------------------------------------------- Colecciones

    public ObservableRangeCollection<TreeRow> Rows { get; } = new();
    public ObservableCollection<DriveItem> Drives { get; } = new();
    public ObservableCollection<MonitorRow> MonitorRows { get; } = new();
    public ObservableCollection<AlertItem> Alerts { get; } = new();
    public ObservableCollection<NotificationRow> Notifications { get; } = new();
    public ObservableCollection<ChartItem> TopFolders { get; } = new();
    public ObservableCollection<ChartItem> TopFiles { get; } = new();
    public ObservableCollection<ChartItem> ByExtension { get; } = new();
    public ObservableCollection<OptionItem<TypeFilter>> TypeFilters { get; }
    public ObservableCollection<OptionItem<SortMode>> SortModes { get; }
    public ObservableCollection<OptionItem<long>> SizePresets { get; }
    public ObservableCollection<OptionItem<int>> IntervalPresets { get; }

    // ---------------------------------------------------------------- Seleccion de los desplegables
    // Los ComboBox se enlazan por SelectedItem y no por SelectedValue: SelectedValuePath
    // obliga a WPF a resolver una ruta sobre una seleccion que todavia no existe y fills
    // el registro de diagnostico con errores de binding que no son fallos reales.

    public OptionItem<TypeFilter>? TypeFilterOption
    {
        get => TypeFilters.FirstOrDefault(o => o.Value.Equals(TypeFilter));
        set { if (value is not null && !value.Value.Equals(TypeFilter)) TypeFilter = value.Value; }
    }

    public OptionItem<SortMode>? SortModeOption
    {
        get => SortModes.FirstOrDefault(o => o.Value.Equals(SortMode));
        set { if (value is not null && !value.Value.Equals(SortMode)) SortMode = value.Value; }
    }

    public OptionItem<long>? MinSizeOption
    {
        get => SizePresets.FirstOrDefault(o => o.Value == MinSizeBytes);
        set { if (value is not null && value.Value != MinSizeBytes) MinSizeBytes = value.Value; }
    }

    public OptionItem<int>? MonitorIntervalOption
    {
        get => IntervalPresets.FirstOrDefault(o => o.Value == MonitorIntervalMinutes);
        set { if (value is not null && value.Value != MonitorIntervalMinutes) MonitorIntervalMinutes = value.Value; }
    }

    // ---------------------------------------------------------------- Comandos

    public AsyncRelayCommand BrowseCommand { get; }
    public AsyncRelayCommand RescanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportJsonCommand { get; }
    public RelayCommand ExportReportCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    public RelayCommand RefreshDrivesCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public AsyncRelayCommand ScanSelectedDriveCommand { get; }
    public RelayCommand ToggleRowCommand { get; }
    public RelayCommand OpenFolderInExplorerCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand RevealInExplorerCommand { get; }
    public RelayCommand OpenTerminalCommand { get; }
    public AsyncRelayCommand ScanFolderCommand { get; }
    public RelayCommand WatchFolderCommand { get; }
    public RelayCommand CopyPathCommand { get; }
    public RelayCommand ExpandAllCommand { get; }
    public RelayCommand CollapseAllCommand { get; }
    public RelayCommand SortByCommand { get; }
    public RelayCommand ToggleExportMenuCommand { get; }
    public RelayCommand NavigateIntoCommand { get; }
    public RelayCommand NavigateUpCommand { get; }
    public RelayCommand AddFolderRuleCommand { get; }
    public RelayCommand AddDriveRuleCommand { get; }
    public RelayCommand RemoveRuleCommand { get; }
    public RelayCommand CheckNowCommand { get; }
    public RelayCommand ClearAlertsCommand { get; }
    public RelayCommand DismissToastCommand { get; }
    public AsyncRelayCommand SendTestNotificationCommand { get; }
    public RelayCommand OpenAlertLocationCommand { get; }

    // ---------------------------------------------------------------- Estado

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (!SetProperty(ref _isDark, value)) return;
            new ThemeService().Apply(value);
            Persist();
        }
    }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set => SetProperty(ref _isSidebarCollapsed, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetProperty(ref _isScanning, value)) return;
            UpdateScanSummary();
            RefreshCommands();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (!SetProperty(ref _currentPath, value)) return;
            Raise(nameof(HasResult));
            RefreshCommands();
        }
    }

    public bool HasResult => _result is not null;

    /// <summary>Breadcrumb con el camino de navegación de capas.</summary>
    public string Breadcrumb
    {
        get
        {
            if (_currentNode is null) return string.Empty;
            var names = _navigationStack.Select(n => n.Name).ToList();
            names.Add(_currentNode.Name);
            return string.Join(" > ", names);
        }
    }

    /// <summary>Permite subir a la capa superior si hay navegación activa.</summary>
    public bool CanNavigateUp => _navigationStack.Count > 0 && _currentNode is not null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;

            // Filtrar es una reconstruccion completa del arbol: agrupar las pulsaciones
            // evita que teclear rapido congele la ventana.
            _searchTimer.Stop();
            _searchTimer.Start();
        }
    }

    private void OnSearchTick(object? sender, EventArgs e)
    {
        // Imprescindible: un DispatcherTimer se repite solo. Sin este Stop, la
        // busqueda seguiria reconstruyendo el arbol entero cuatro veces por
        // segundo aunque el usuario no escribiera nada mas.
        _searchTimer.Stop();
        ApplySearch();
    }

    private void ApplySearch() => RebuildView();

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    /// <summary>
    /// Resumen de la lectura. Es un valor almacenado y no una propiedad calculada:
    /// depende a la vez del progreso, del resultado y del estado de escaneo, y
    /// depender de avisos manuales para algo asi es una fuente clasica de valores
    /// congelados en pantalla.
    /// </summary>
    public string ScanSummary
    {
        get => _scanSummary;
        private set => SetProperty(ref _scanSummary, value);
    }

    private void UpdateScanSummary()
    {
        ScanSummary = IsScanning || _result is null
            ? $"{Formatter.Bytes(ScannedBytes)}  ·  {Formatter.Count(ScannedFiles)} archivos  ·  {Formatter.Count(ScannedFolders)} carpetas"
            : $"{_result.DisplayTotal}  ·  {Formatter.Count(_result.TotalFiles)} archivos  ·  " +
              $"{Formatter.Count(_result.TotalFolders)} carpetas  ·  {_result.DisplayElapsed}";
    }

    public long ScannedBytes
    {
        get => _scannedBytes;
        private set
        {
            if (SetProperty(ref _scannedBytes, value)) UpdateScanSummary();
        }
    }

    public int ScannedFiles
    {
        get => _scannedFiles;
        private set
        {
            if (SetProperty(ref _scannedFiles, value)) UpdateScanSummary();
        }
    }

    public int ScannedFolders
    {
        get => _scannedFolders;
        private set
        {
            if (SetProperty(ref _scannedFolders, value)) UpdateScanSummary();
        }
    }

    public long MinSizeBytes
    {
        get => _minSizeBytes;
        set
        {
            if (SetProperty(ref _minSizeBytes, Math.Max(0, value)))
            {
                RebuildView();
                Persist();
            }
        }
    }

    public TypeFilter TypeFilter
    {
        get => _typeFilter;
        set
        {
            if (SetProperty(ref _typeFilter, value))
            {
                RebuildView();
                Persist();
            }
        }
    }

    public SortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (SetProperty(ref _sortMode, value))
            {
                Raise(nameof(SortIndicator));
                RebuildView();
                Persist();
            }
        }
    }

    /// <summary>Estado de orden publicado para los indicadores de las cabeceras.</summary>
    public string SortIndicator => $"{SortMode}|{(SortAscending ? "up" : "down")}";

    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (SetProperty(ref _sortAscending, value))
            {
                Raise(nameof(SortIndicator));
                RebuildView();
                Persist();
            }
        }
    }

    public bool IncludeSystemHidden
    {
        get => _includeSystemHidden;
        set
        {
            if (SetProperty(ref _includeSystemHidden, value)) Persist();
        }
    }

    public AlertItem? Toast
    {
        get => _toast;
        private set
        {
            if (!SetProperty(ref _toast, value)) return;

            // Aplanados: enlazar a Toast.Message cuando Toast es null llena el
            // registro de diagnostico de errores que no son fallos.
            ToastMessage = value?.Message ?? string.Empty;
            ToastPath = value?.Path ?? string.Empty;
            Raise(nameof(HasToast));
        }
    }

    public string ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public string ToastPath
    {
        get => _toastPath;
        private set => SetProperty(ref _toastPath, value);
    }

    public bool HasToast => Toast is not null;

    public bool IsExportMenuOpen
    {
        get => _isExportMenuOpen;
        set => SetProperty(ref _isExportMenuOpen, value);
    }

    public TreeRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value)) RaiseSelectionDetail();
        }
    }

    public DriveItem? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value)) RefreshCommands();
        }
    }

    public MonitorRow? SelectedMonitor
    {
        get => _selectedMonitor;
        set
        {
            if (SetProperty(ref _selectedMonitor, value)) RefreshCommands();
        }
    }

    public AlertItem? SelectedAlert
    {
        get => _selectedAlert;
        set
        {
            if (SetProperty(ref _selectedAlert, value)) RefreshCommands();
        }
    }

    public string SelectionTitle
    {
        get => _selectionTitle;
        private set => SetProperty(ref _selectionTitle, value);
    }

    public string SelectionMeta
    {
        get => _selectionMeta;
        private set => SetProperty(ref _selectionMeta, value);
    }

    public string SelectionPath
    {
        get => _selectionPath;
        private set => SetProperty(ref _selectionPath, value);
    }

    // ---------------------------------------------------------------- Ajustes de monitoreo

    public bool MonitoringEnabled
    {
        get => _monitoringEnabled;
        set
        {
            if (!SetProperty(ref _monitoringEnabled, value)) return;
            _settings.MonitoringEnabled = value;
            ApplyMonitorConfiguration();
            Persist();
            RefreshCommands();
        }
    }

    public int MonitorIntervalMinutes
    {
        get => _monitorIntervalMinutes;
        set
        {
            var clamped = Math.Clamp(value, 1, 1440);
            if (!SetProperty(ref _monitorIntervalMinutes, clamped)) return;
            _settings.MonitorIntervalMinutes = clamped;
            ApplyMonitorConfiguration();
            Persist();
        }
    }

    public bool NotifyInApp
    {
        get => _settings.NotifyInApp;
        set { if (_settings.NotifyInApp != value) { _settings.NotifyInApp = value; Persist(); } }
    }

    public bool NotifySound
    {
        get => _settings.NotifySound;
        set { if (_settings.NotifySound != value) { _settings.NotifySound = value; Persist(); } }
    }

    public bool NotifyEmail
    {
        get => _settings.NotifyEmail;
        set { if (_settings.NotifyEmail != value) { _settings.NotifyEmail = value; Persist(); Raise(nameof(EmailEnabled)); } }
    }

    public bool NotifyWebhook
    {
        get => _settings.NotifyWebhook;
        set { if (_settings.NotifyWebhook != value) { _settings.NotifyWebhook = value; Persist(); Raise(nameof(WebhookEnabled)); } }
    }

    public bool EmailEnabled => NotifyEmail;
    public bool WebhookEnabled => NotifyWebhook;

    public string SmtpHost
    {
        get => _settings.SmtpHost;
        set { if (_settings.SmtpHost != value) { _settings.SmtpHost = value; Persist(); } }
    }

    public int SmtpPort
    {
        get => _settings.SmtpPort;
        set { if (_settings.SmtpPort != value) { _settings.SmtpPort = value; Persist(); } }
    }

    /// <summary>
    /// El puerto se edita como texto: escribir un valor a medias no debe lanzar
    /// una excepcion de validacion mientras el campo esta en uso.
    /// </summary>
    public string SmtpPortText
    {
        get => _settings.SmtpPort.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) &&
                port is > 0 and < 65536 &&
                _settings.SmtpPort != port)
            {
                _settings.SmtpPort = port;
                Persist();
            }
        }
    }

    public bool SmtpUseSsl
    {
        get => _settings.SmtpUseSsl;
        set { if (_settings.SmtpUseSsl != value) { _settings.SmtpUseSsl = value; Persist(); } }
    }

    public string SmtpUser
    {
        get => _settings.SmtpUser;
        set { if (_settings.SmtpUser != value) { _settings.SmtpUser = value; Persist(); } }
    }

    public string SmtpPassword
    {
        get => _settings.SmtpPassword;
        set { if (_settings.SmtpPassword != value) { _settings.SmtpPassword = value; Persist(); } }
    }

    public string EmailFrom
    {
        get => _settings.EmailFrom;
        set { if (_settings.EmailFrom != value) { _settings.EmailFrom = value; Persist(); } }
    }

    public string EmailTo
    {
        get => _settings.EmailTo;
        set { if (_settings.EmailTo != value) { _settings.EmailTo = value; Persist(); } }
    }

    public string WebhookUrl
    {
        get => _settings.WebhookUrl;
        set { if (_settings.WebhookUrl != value) { _settings.WebhookUrl = value; Persist(); } }
    }

    // ---------------------------------------------------------------- Escaneo

    private async Task BrowseFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Selecciona la carpeta a analizar",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            await ScanAsync(dialog.FolderName);
        }
    }

    private async Task RescanAsync()
    {
        if (!string.IsNullOrWhiteSpace(CurrentPath))
        {
            await ScanAsync(CurrentPath);
        }
    }

    private async Task ScanAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsScanning) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsScanning = true;
        Progress = 0;
        ScannedBytes = 0;
        ScannedFiles = 0;
        ScannedFolders = 0;
        StatusText = $"Analizando {path}...";
        // Resetear navegación de capas
        _currentNode = null;
        _navigationStack.Clear();
        _scanRoot = null;

        try
        {
            var progress = new Progress<ScanSnapshot>(p =>
            {
                ScannedBytes = p.BytesSoFar;
                ScannedFiles = p.FilesSoFar;
                ScannedFolders = p.FoldersSoFar;
                StatusText = $"Analizando...  {Formatter.Bytes(p.BytesSoFar)} en {Formatter.Count(p.FilesSoFar)} archivos";

                // Modo pre-scan: renderizar filas parciales con los hijos directos que ya se conocen
                if (p.PartialRootChildren.Count > 0)
                {
                    UpdatePreScanRows(path, p);
                }
            });

            // El recorrido del disco no puede tocar el hilo de la interfaz. Un
            // escaneo es trabajo de E/S de archivos: si corre aqui, la ventana
            // deja de repintar mientras dura, y el usuario lo ve como un cuelgue.
            // El progreso sigue llegando a la interfaz porque Progress<T> se creo
            // aqui y encola en el dispatcher.
            var result = await Task.Run(
                () => _scanner.ScanAsync(path, IncludeSystemHidden, progress, token),
                token);

            _result = result;
            _scanRoot = result.Root;
            _navigationStack.Push(_scanRoot);
            // Anula cualquier vista que se estuviera calculando para el escaneo anterior.
            _rebuildGeneration++;
            CurrentPath = result.Root.FullPath;
            _settings.LastPath = CurrentPath;
            _expandedPaths.Clear();

            Post(() =>
            {
                TopFolders.Clear();
                foreach (var item in result.TopFolders) TopFolders.Add(item);

                TopFiles.Clear();
                foreach (var item in result.TopFiles) TopFiles.Add(item);

                ByExtension.Clear();
                foreach (var item in result.ByExtension) ByExtension.Add(item);

                RebuildView();
                Progress = 100;
                UpdateScanSummary();

                StatusText = result.SkippedItems > 0
                    ? $"Listo en {result.DisplayElapsed}. {result.SkippedItems} elementos omitidos por permisos o enlaces."
                    : $"Listo en {result.DisplayElapsed}.";
            });

            Persist();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analisis cancelado.";
            Progress = 0;
            // Si el usuario cancela a mitad de escaneo, la vista parcial no sirve:
            // la limpiamos para que no quede un arbol a medias en pantalla.
            _viewRoot = null;
            _rebuildGeneration++;
            Rows.ReplaceAll(Array.Empty<TreeRow>());
        }
        catch (Exception ex)
        {
            StatusText = "Error durante el analisis.";
            _viewRoot = null;
            _rebuildGeneration++;
            Rows.ReplaceAll(Array.Empty<TreeRow>());
            Post(() =>
            {
                var alert = new AlertItem(DateTime.Now, path ?? string.Empty, ex.Message, 0, 0, isError: true);
                Alerts.Insert(0, alert);
                Toast = alert;
            });
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "Cancelando...";
    }

    // ---------------------------------------------------------------- Vista

    /// <summary>
    /// Cuantas veces se ha reconstruido el arbol. Sirve para detectar filtros que
    /// se realimentan en bucle: cada reconstruccion clona todo el arbol.
    /// </summary>
    public int RebuildCount { get; private set; }

    /// <summary>
    /// Tope de filas dibujadas. El listado virtualiza, asi que dibujar cien mil
    /// filas no consume memoria, pero construirlas y medirlas todas en el hilo de
    /// la interfaz si la deja congelada. Es un limite de seguridad, no de diseno:
    /// por encima de el se avisa y se acota con los filtros.
    /// </summary>
    public const int MaxRows = 5000;

    /// <summary>
    /// Cuantas ramas de primer nivel se abren solas al terminar un analisis o al
    /// cambiar un filtro. Es un tope de comodidad, no de diseno: abrir todas en una
    /// unidad genera de golpe miles de filas.
    /// </summary>
    public const int MaxAutoExpanded = 40;

    private async void RebuildView()
    {
        RebuildCount++;

        var result = _result;
        if (result is null) return;

        var generation = ++_rebuildGeneration;
        var source = _currentNode ?? result.Root;
        var minSize = MinSizeBytes;
        var filter = TypeFilter;
        var search = SearchText;
        var mode = SortMode;
        var ascending = SortAscending;

        try
        {
            // Clonar y ordenar el arbol es calculo puro sobre cientos de miles de
            // nodos. Hecho aqui, es una ventana muerta de segundos: la ventana deja
            // de repintar y parece colgada. Fuera, solo se publica el resultado ya
            // construido.
            var built = await Task.Run(() =>
            {
                var view = TreeTransformer.Build(source, minSize, filter, search, mode, ascending);

                // Lo que veria el listado si nada estuviera recogido: carpetas Y
                // archivos. Contar solo carpetas daba un total que no cuadtaba con las
                // filas mostradas y hacia que el aviso de recorte dijera locuras.
                var visible = 0;

                foreach (var node in view.DescendantsAndSelf())
                {
                    visible += node.Items.Count;
                }

                return (View: view, Visible: visible);
            });

            // Si mientras se calculaba cambio un filtro o empezo otro escaneo, este
            // resultado ya no vale: hay otro mas reciente en marcha.
            if (generation != _rebuildGeneration) return;
            if (!ReferenceEquals(result, _result)) return;

            var view = built.View;

            _viewRoot = view;

            // Al cambiar filtros se reabre el arbol: el usuario no deberia tener que
            // redescubrir que hay dentro de cada rama. Con tope, porque abrir hundreds
            // de ramas de golpe en una unidad es una lista de miles de filas que nadie
            // va a recorrer. Van por tamano, asi que son las que mas pesan.
            _expandedPaths.Clear();

            var opened = 0;

            foreach (var child in view.Children)
            {
                if (opened >= MaxAutoExpanded)
                {
                    break;
                }

                _expandedPaths.Add(child.FullPath);
                opened++;
            }

            RebuildRows();

            var shown = Rows.Count;

            // Con filtros activos el total visto ya no es el total leido: decirlo solo
            // cuando difiere evita "293 MB de 293 MB".
            var headline = view.Size == result.TotalBytes
                ? $"{built.Visible} elementos  ·  {result.DisplayTotal}"
                : $"{built.Visible} elementos  ·  {Formatter.Bytes(view.Size)} de {result.DisplayTotal}";

            // Avisar solo cuando el tope de filas muerde de verdad. Mostrar la diferencia
            // entre filas dibujadas y elementos del arbol tambien cuando lo que
            // falta esta simplemente recogido seria mentir: el listado no esta
            // recortado, esta plegado, que es lo normal.
            var capped = shown >= MaxRows;

            StatusText = capped
                ? $"{headline}  ·  se dibuja hasta {MaxRows:N0} filas de {built.Visible:N0}; afina con los filtros para ver el resto"
                : headline;
        }
        catch (Exception ex)
        {
            if (generation != _rebuildGeneration) return;

            StatusText = $"Error al preparar la vista: {ex.Message}";
        }
    }

    /// <summary>
    /// Renderiza la vista parcial mientras el escaneo sigue en marcha: muestra los
    /// hijos directos del root que ya se han descubierto, con sus tamaños parciales.
    /// No usa filtros ni busqueda: la vista parcial es solo para que el usuario
    /// vea que la aplicacion no esta congelada. Cuando termine el escaneo, RebuildView()
    /// reemplaza esto con el arbol completo y filtrado.
    /// </summary>
    private void UpdatePreScanRows(string rootPath, ScanSnapshot snapshot)
    {
        var partial = new FolderNode(Path.GetFileName(rootPath) ?? rootPath, rootPath, isRoot: true);
        partial.Size = snapshot.BytesSoFar;
        partial.FileCount = snapshot.FilesSoFar;
        partial.FolderCount = Math.Max(0, snapshot.FoldersSoFar - 1); // menos el root

        // Clonar superficialmente los hijos directos: el escaneo sigue tocando
        // los mismos nodos en el hilo de trabajo y la UI no puede ver una lista
        // mientras se modifica. Los tamaños y archivos se copian en este instante.
        foreach (var child in snapshot.PartialRootChildren)
        {
            var clone = new FolderNode(child.Name, child.FullPath)
            {
                Size = child.Size,
                DirectSize = child.DirectSize,
                FileCount = child.FileCount,
                FolderCount = child.FolderCount,
                LatestWriteTime = child.LatestWriteTime
            };

            clone.Files.AddRange(child.Files);
            clone.Children.AddRange(child.Children);
            partial.Children.Add(clone);
        }

        partial.RebuildItems(SortMode.Size);

        _viewRoot = partial;
        _rebuildGeneration++;
        Rows.ReplaceAll(Array.Empty<TreeRow>());
        RebuildRows();
    }

    private void RebuildRows()
    {
        var list = new List<TreeRow>();
        if (_viewRoot is null)
        {
            Rows.ReplaceAll(Array.Empty<TreeRow>());
            return;
        }

        var total = _viewRoot.Size <= 0 ? 1 : _viewRoot.Size;

        list.Add(new TreeRow(_viewRoot, null, 0, true) { TotalBytes = total });
        Flatten(_viewRoot, 1, list, total);

        // Un solo aviso en vez de uno por fila: es lo que hacia lento al expandir.
        Rows.ReplaceAll(list);

        RefreshCommands();
    }

    private void NavigateInto(object? parameter)
    {
        if (parameter is not TreeRow { Folder: not null } row) return;
        var node = row.Folder;
        _navigationStack.Push(_currentNode ?? _scanRoot!);
        _currentNode = node;
        _rebuildGeneration++;
        RebuildView();
        Raise(nameof(Breadcrumb));
        Raise(nameof(CanNavigateUp));
    }

    private void NavigateUp()
    {
        if (_currentNode is null || _navigationStack.Count == 0) return;
        _currentNode = _navigationStack.Pop();
        if (_currentNode == _scanRoot) _currentNode = null;
        _rebuildGeneration++;
        RebuildView();
        Raise(nameof(Breadcrumb));
        Raise(nameof(CanNavigateUp));
    }

    private int _rebuildGeneration;

    private void Flatten(FolderNode node, int depth, List<TreeRow> output, long total)
    {
        foreach (var item in node.Items)
        {
            if (output.Count >= MaxRows)
            {
                continue;
            }

            switch (item)
            {
                case FolderNode child:
                    {
                        var expanded = _expandedPaths.Contains(child.FullPath);
                        output.Add(new TreeRow(child, null, depth, expanded) { TotalBytes = total });
                        if (expanded && child.Items.Count > 0)
                        {
                            Flatten(child, depth + 1, output, total);
                        }
                        break;
                    }

                case FileEntry file:
                    output.Add(new TreeRow(null, file, depth, false) { TotalBytes = total });
                    break;
            }
        }
    }

    private void ToggleRow(TreeRow? row)
    {
        if (row?.Folder is null) return;

        if (!_expandedPaths.Remove(row.Folder.FullPath))
        {
            _expandedPaths.Add(row.Folder.FullPath);
        }

        RebuildRows();
    }

    // ---------------------------------------------------------------- Menu contextual de la fila

    private static string? RowPathOf(object? parameter) => (parameter as TreeRow)?.FullPath;

    private static string FolderPathOf(object? parameter) => (parameter as TreeRow)?.FullPath ?? string.Empty;

    private static bool IsFolderRow(object? parameter)
        => parameter is TreeRow { IsFolder: true } row && !string.IsNullOrWhiteSpace(row.FullPath);

    /// <summary>Carpeta a la que apunta la fila: la propia si es carpeta, la que la contiene si es archivo.</summary>
    private static string? DirectoryOf(object? parameter)
    {
        if (parameter is not TreeRow row || string.IsNullOrWhiteSpace(row.FullPath)) return null;

        return row.IsFolder ? row.FullPath : Path.GetDirectoryName(row.FullPath);
    }

    /// <summary>
    /// Lanza el programa del sistema. Todo lo que abre el usuario (explorador,
    /// terminal, archivo) pasa por aqui: si algo falla, se dice en el pie en vez
    /// de dejar la aplicacion en silencio.
    /// </summary>
    private bool Launch(string? fileName, string arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(fileName, arguments) { UseShellExecute = true };
            using var process = System.Diagnostics.Process.Start(info);

            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo abrir «{fileName}»: {ex.Message}";
            return false;
        }
    }

    private void OpenTerminal(object? parameter)
    {
        var directory = DirectoryOf(parameter);
        if (string.IsNullOrWhiteSpace(directory)) return;

        // El coma simple se escapa duplicandolo: una ruta con apostro no debe
        // romper el comando ni el resto de la linea.
        var literal = directory!.Replace("'", "''");
        var command = $"Set-Location -LiteralPath '{literal}'";

        var shell = HasPwsh() ? "pwsh.exe" : "powershell.exe";
        Launch(shell, $"-NoLogo -NoExit -Command \"{command}\"");
    }

    /// <summary>PowerShell 7 no viene en todas las máquinas; 5 sí.</summary>
    private static bool HasPwsh()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return false;

        foreach (var folder in path.Split(Path.PathSeparator))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder.Trim(), "pwsh.exe")))
                {
                    return true;
                }
            }
            catch
            {
                // Entrada de PATH invalida: se ignora y se sigue con la siguiente.
            }
        }

        return false;
    }

    private void WatchFolder(TreeRow? row)
    {
        if (row?.Folder is null) return;

        var path = row.Folder.FullPath;
        var label = Path.GetFileName(path.TrimEnd('\\'));

        AddRule(new MonitorTarget(path, MonitorKind.Folder, label, 100L * 1024 * 1024, MonitorLimitMode.Bytes));
    }

    private void CopyPath(TreeRow? row)
    {
        var path = row?.FullPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        var lastError = "el portapapeles no respondio";

        // El portapapeles puede estar pillado por otra aplicacion justo en ese
        // instante; dos reintentos cortos evitan el fallo que el usuario no
        // entiende ("no hace nada").
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(path);
                StatusText = $"Ruta copiada: {path}";
                return;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attempt < 2) Thread.Sleep(40);
            }
        }

        StatusText = $"No se pudo copiar la ruta: {lastError}";
    }

    private void ExpandAll()
    {
        if (_viewRoot is null) return;

        foreach (var node in _viewRoot.DescendantsAndSelf())
        {
            _expandedPaths.Add(node.FullPath);
        }

        RebuildRows();
    }

    private void CollapseAll()
    {
        if (_viewRoot is null) return;

        _expandedPaths.Clear();
        RebuildRows();
    }

    private void ResetFilters()
    {
        SearchText = string.Empty;
        MinSizeBytes = 0;
        TypeFilter = TypeFilter.All;
        SortMode = SortMode.Size;
        SortAscending = false;
    }

    /// <summary>
    /// Clic en una cabecera de columna. Si ya se estaba ordenando por esa columna,
    /// invierte el sentido; si no, la convierte en la columna activa y arranca en
    /// descendente, que es como se lee un tamano.
    /// </summary>
    private void SortBy(object? parameter)
    {
        if (parameter is not SortMode mode) return;

        if (SortMode == mode)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortMode = mode;
            SortAscending = mode is SortMode.Name;
        }
    }

    // ---------------------------------------------------------------- Unidades

    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var drive in _drives.GetDrives())
        {
            Drives.Add(drive);
        }

        if (SelectedDrive is null && Drives.Count > 0)
        {
            SelectedDrive = Drives[0];
        }
    }

    // ---------------------------------------------------------------- Monitoreo

    private void AddFolderRule()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Carpeta a monitorear", Multiselect = false };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName)) return;

        AddRule(new MonitorTarget(dialog.FolderName, MonitorKind.Folder, Path.GetFileName(dialog.FolderName.TrimEnd('\\')), 100L * 1024 * 1024, MonitorLimitMode.Bytes));
    }

    private void AddDriveRule(DriveItem? drive)
    {
        if (drive is null) return;
        AddRule(new MonitorTarget(drive.Root, MonitorKind.Drive, drive.DisplayVolume, 90, MonitorLimitMode.Percent));
    }

    private void AddRule(MonitorTarget target)
    {
        if (_settings.MonitorRules.Any(r => r.Path == target.Path && r.Kind == target.Kind))
        {
            StatusText = "Esa ruta ya esta en el monitoreo.";
            return;
        }

        _settings.MonitorRules.Add(new MonitorRule
        {
            Path = target.Path,
            Label = target.Label,
            Kind = target.Kind,
            LimitBytes = target.LimitBytes,
            Mode = target.Mode
        });

        MonitorRows.Add(new MonitorRow(target, "Pendiente", "-", target.DisplayLimit, 0, false));
        ApplyMonitorConfiguration();
        Persist();
        _monitor.CheckNow();
    }

    private void RemoveSelectedRule()
    {
        if (SelectedMonitor is null) return;

        _settings.MonitorRules.RemoveAll(r => r.Path == SelectedMonitor.Path && r.Kind == SelectedMonitor.Target.Kind);
        MonitorRows.Remove(SelectedMonitor);
        SelectedMonitor = null;
        ApplyMonitorConfiguration();
        Persist();
    }

    private void ApplyMonitorConfiguration()
    {
        var targets = _settings.MonitorRules
            .Select(r => new MonitorTarget(r.Path, r.Kind, r.Label, r.LimitBytes, r.Mode))
            .ToList();

        _monitor.Configure(targets, MonitorIntervalMinutes, MonitoringEnabled);
        RefreshCommands();
    }

    private void ApplyMonitorStatuses(IReadOnlyList<MonitorStatus> statuses)
    {
        foreach (var status in statuses)
        {
            var row = MonitorRows.FirstOrDefault(r => r.Path == status.Path);
            if (row is null) continue;

            var updated = new MonitorRow(row.Target, status.StateText, status.DisplayUsed, row.Target.DisplayLimit, status.BarFraction, status.Exceeded);

            var index = MonitorRows.IndexOf(row);
            MonitorRows[index] = updated;

            if (SelectedMonitor == row)
            {
                SelectedMonitor = updated;
            }
        }
    }

    private void RefreshMonitorNow() => _monitor.CheckNow();

    private async Task HandleAlertAsync(AlertItem alert)
    {
        Alerts.Insert(0, alert);
        while (Alerts.Count > 200) Alerts.RemoveAt(Alerts.Count - 1);

        Toast = alert;

        var results = await _notifications.SendAsync(alert);

        Post(() =>
        {
            foreach (var result in results)
            {
                Notifications.Insert(0, new NotificationRow(DateTime.Now, result.Channel, result.Success, result.Detail));
            }
            while (Notifications.Count > 100) Notifications.RemoveAt(Notifications.Count - 1);
        });
    }

    private void ClearAlerts()
    {
        Alerts.Clear();
        Notifications.Clear();
    }

    private async Task SendTestNotificationAsync()
    {
        StatusText = "Enviando notificacion de prueba...";
        var sample = new AlertItem(
            DateTime.Now,
            CurrentPath.Length > 0 ? CurrentPath : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Notificacion de prueba: los canales configurados funcionan.",
            123456789,
            100000000);

        var results = await _notifications.SendAsync(sample);

        Post(() =>
        {
            foreach (var result in results)
            {
                Notifications.Insert(0, new NotificationRow(DateTime.Now, result.Channel, result.Success, result.Detail));
            }
            Toast = sample;
            StatusText = results.All(r => r.Success)
                ? "Prueba entregada en todos los canales."
                : "Prueba enviada con fallos en algunos canales.";
        });
    }

    private void OpenAlertLocation()
    {
        var path = SelectedAlert?.Path;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (File.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            StatusText = "No se pudo abrir la ruta.";
        }
    }

    // ---------------------------------------------------------------- Exportacion

    private void Export(string kind)
    {
        if (_result is null) return;

        IsExportMenuOpen = false;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar resultado del analisis",
            FileName = kind switch
            {
                "json" => "tamano-carpetas.json",
                "report" => "informe-tamano.txt",
                _ => "tamano-carpetas.csv"
            },
            Filter = kind switch
            {
                "json" => "Archivo JSON (*.json)|*.json",
                "report" => "Informe de texto (*.txt)|*.txt",
                _ => "Archivo CSV (*.csv)|*.csv"
            }
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var exporter = new ExportService(_result.Root, _result.TotalBytes);

            switch (kind)
            {
                case "json": exporter.ExportJson(dialog.FileName); break;
                case "report": exporter.ExportReport(dialog.FileName); break;
                default: exporter.ExportCsv(dialog.FileName); break;
            }

            StatusText = $"Exportado a {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al exportar: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- Ajustes

    private void RestoreSettings()
    {
        _isDark = _settings.DarkTheme;
        _minSizeBytes = _settings.MinSizeBytes;
        _typeFilter = _settings.TypeFilter;
        _sortMode = _settings.SortMode;
        _sortAscending = _settings.SortAscending;
        _includeSystemHidden = _settings.IncludeSystemHidden;
        _monitoringEnabled = _settings.MonitoringEnabled;
        _monitorIntervalMinutes = Math.Clamp(_settings.MonitorIntervalMinutes, 1, 1440);

        foreach (var rule in _settings.MonitorRules)
        {
            var target = new MonitorTarget(rule.Path, rule.Kind, rule.Label, rule.LimitBytes, rule.Mode);
            MonitorRows.Add(new MonitorRow(target, "Pendiente", "-", target.DisplayLimit, 0, false));
        }

        ApplyMonitorConfiguration();

        Raise(
            nameof(IsDark), nameof(IsSidebarCollapsed), nameof(MinSizeBytes), nameof(MinSizeOption),
            nameof(TypeFilter), nameof(TypeFilterOption),
            nameof(SortMode), nameof(SortModeOption), nameof(SortIndicator), nameof(SortAscending),
            nameof(IncludeSystemHidden), nameof(ScanSummary),
            nameof(MonitoringEnabled), nameof(MonitorIntervalMinutes), nameof(MonitorIntervalOption),
            nameof(NotifyInApp), nameof(NotifySound), nameof(NotifyEmail), nameof(NotifyWebhook),
            nameof(SmtpHost), nameof(SmtpPort), nameof(SmtpUseSsl), nameof(SmtpUser),
            nameof(SmtpPassword), nameof(EmailFrom), nameof(EmailTo), nameof(WebhookUrl),
            nameof(EmailEnabled), nameof(WebhookEnabled));
    }

    private void ToggleTheme()
    {
        IsDark = !IsDark;
        StatusText = IsDark ? "Tema oscuro." : "Tema claro.";
    }

    private void Persist() => _settingsService.Save(_settings);

    private void RefreshCommands()
    {
        BrowseCommand.RaiseCanExecuteChanged();
        RescanCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ScanSelectedDriveCommand.RaiseCanExecuteChanged();
        RemoveRuleCommand.RaiseCanExecuteChanged();
        CheckNowCommand.RaiseCanExecuteChanged();
        ClearAlertsCommand.RaiseCanExecuteChanged();
        OpenAlertLocationCommand.RaiseCanExecuteChanged();
        ExpandAllCommand.RaiseCanExecuteChanged();
        CollapseAllCommand.RaiseCanExecuteChanged();
        ToggleRowCommand.RaiseCanExecuteChanged();
        ExportCsvCommand.RaiseCanExecuteChanged();
        ExportJsonCommand.RaiseCanExecuteChanged();
        ExportReportCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSelectionDetail()
    {
        if (SelectedRow is null)
        {
            SelectionTitle = "Sin seleccion";
            SelectionMeta = "Pulsa una fila del arbol para ver su detalle.";
            SelectionPath = string.Empty;
            return;
        }

        SelectionTitle = SelectedRow.Name;

        SelectionMeta = SelectedRow.IsFolder
            ? $"{SelectedRow.DisplaySize}   ·   {Formatter.Count(SelectedRow.TotalFiles)} archivos   ·   " +
              $"{Formatter.Count(SelectedRow.TotalFolders)} carpetas   ·   {SelectedRow.DisplayModified}"
            : $"{SelectedRow.DisplaySize}   ·   {SelectedRow.Extension.ToUpperInvariant()}   ·   {SelectedRow.DisplayModified}";

        SelectionPath = SelectedRow.FullPath;
    }

    private void Post(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _searchTimer.Stop();
        _monitor.Dispose();
    }
}