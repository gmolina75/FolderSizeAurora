using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FolderSizeAurora.Helpers;
using FolderSizeAurora.Models;
using FolderSizeAurora.ViewModels;

namespace FolderSizeAurora;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>DependencyProperty que enlaza WindowStyle/WindowState con el ViewModel.</summary>
    public static readonly DependencyProperty IsPresentationModeProperty =
        DependencyProperty.Register(
            "IsPresentationMode",
            typeof(bool),
            typeof(MainWindow),
            new PropertyMetadata(false, OnIsPresentationModeChanged));

    /// <summary>Medida deseada en una pantalla holgada; nunca se impone si no cabe.</summary>
    public const double PreferredWidth = 1320;
    public const double PreferredHeight = 800;

    /// <summary>
    /// Minimos de comfort, no de diseno: si el area de trabajo es menor, el
    /// minimo tambien baja (ver <see cref="ApplyWorkAreaPlacement"/>).
    /// </summary>
    public const double PreferredMinWidth = 1024;
    public const double PreferredMinHeight = 560;

    public MainWindow()
    {
        // El DataContext se asigna ANTES de construir el arbol visual. Al revés,
        // WPF evalua cada binding contra un contexto nulo y deja el registro de
        // diagnostico lleno de errores que luego no son errores reales.
        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;

        InitializeComponent();

        // ScrollChanged llega por burbuja desde el ScrollViewer interno del listado.
        Tree.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTreeScrollChanged));
        Tree.PreviewMouseRightButtonDown += OnTreeRightButtonDown;

        // El binding bidireccional sincroniza WindowStyle/WindowState del Window
        // con IsPresentationMode del ViewModel. Se registra despues de
        // InitializeComponent porque las propiedades de ventana necesitan el HWND.
        var binding = new Binding(nameof(MainViewModel.IsPresentationMode)) { Mode = BindingMode.TwoWay };
        SetBinding(IsPresentationModeProperty, binding);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // La seleccion de fila va por binding; los atajos de teclado se registran
        // aqui porque un KeyBinding no hereda el DataContext de la ventana.
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.O, ModifierKeys.Control),
            Command = _viewModel.BrowseCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.R, ModifierKeys.Control),
            Command = _viewModel.RescanCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.B, ModifierKeys.Control),
            Command = _viewModel.ToggleSidebarCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.D, ModifierKeys.Control),
            Command = _viewModel.ToggleThemeCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.E, ModifierKeys.Control),
            Command = _viewModel.ExpandAllCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.L, ModifierKeys.Control),
            Command = _viewModel.CollapseAllCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.F11),
            Command = _viewModel.TogglePresentationModeCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.T, ModifierKeys.Control),
            Command = _viewModel.ToggleTopmostCommand
        });
        InputBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.K, ModifierKeys.Control | ModifierKeys.Shift),
            Command = _viewModel.ToggleKioskModeCommand
        });

        Loaded += OnLoaded;
        SizeChanged += (_, _) => SyncHeaderGutter(null);
        Closed += (_, _) => _viewModel.Dispose();
    }

    /// <summary>
    /// Geometria pura de encaje. Vive aqui, y no solo dentro de
    /// <see cref="ApplyWorkAreaPlacement"/>, para que el arnes pueda comprobar con
    /// numeros que la ventana cabe en pantallas de cualquier medida sin tener que
    /// reiniciar el equipo.
    /// </summary>
    public static Rect FitToWorkArea(Rect workArea) =>
        ScreenGeometry.FitToWorkArea(
            workArea,
            PreferredWidth,
            PreferredHeight,
            PreferredMinWidth,
            PreferredMinHeight);

    /// <summary>
    /// En modo presentacion, la ventana pierde bordes y se maximiza.
    /// Al salir, vuelve al estado normal con bordes.
    /// </summary>
    private static void OnIsPresentationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MainWindow window) return;

        if (e.NewValue is true)
        {
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.WindowState = WindowState.Normal;
            window.ApplyWorkAreaPlacement();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsPresentationMode))
        {
            SetValue(IsPresentationModeProperty, _viewModel.IsPresentationMode);
        }
    }

    /// <summary>
    /// Se ajusta antes del primer dibujado: OnSourceInitialized ocurre con el
    /// HWND ya creado pero antes de que la ventana sea visible, de modo que
    /// nadie llega a ver un destello fuera de la pantalla.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        ApplyWorkAreaPlacement();
    }

    private void ApplyWorkAreaPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var workArea = ScreenGeometry.WorkAreaInDips(handle, this);
        var target = FitToWorkArea(workArea);

        // MinWidth/MinHeight por encima del area de trabajo obligarian a WPF a
        // mantener una ventana mas grande que la pantalla. Bajarlos aqui es la
        // parte que evita el defecto, no un detalle.
        var min = ScreenGeometry.ClampMinimum(workArea, PreferredMinWidth, PreferredMinHeight);
        MinWidth = min.Width;
        MinHeight = min.Height;

        // Primero el tamano y despues la posicion: Left/Top se aplican sobre el
        // tamano ya fijado, y al reves el centrado sale desplazado.
        WindowState = WindowState.Normal;
        Width = target.Width;
        Height = target.Height;
        Left = target.X;
        Top = target.Y;
    }

    /// <summary>Pixeles que la ventana se salen del area de trabajo. Cero es correcto.</summary>
    public double PlacementOverflow
    {
        get
        {
            var handle = new WindowInteropHelper(this).Handle;
            var workArea = ScreenGeometry.WorkAreaInDips(handle, this);

            var left = double.IsNaN(Left) || double.IsInfinity(Left) ? workArea.Left : Left;
            var top = double.IsNaN(Top) || double.IsInfinity(Top) ? workArea.Top : Top;
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : Height;

            return ScreenGeometry.Overflow(new Rect(left, top, width, height), workArea);
        }
    }

    /// <summary>Cifras de la colocacion, para el arnés y para depurar a mano.</summary>
    public string PlacementInfo
    {
        get
        {
            var workArea = ScreenGeometry.WorkAreaInDips(new WindowInteropHelper(this).Handle, this);

            return $"ventana=({Left:0.#},{Top:0.#},{ActualWidth:0.#},{ActualHeight:0.#}) " +
                   $"work=({workArea.Left:0.#},{workArea.Top:0.#},{workArea.Width:0.#},{workArea.Height:0.#}) " +
                   $"min=({MinWidth:0.#},{MinHeight:0.#}) desborde={PlacementOverflow:0.##}";
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // El tema se aplica despues de que los recursos de la ventana existen.
        if (_viewModel.IsDark) _viewModel.IsDark = true;

        if (!string.IsNullOrWhiteSpace(_viewModel.CurrentPath))
        {
            _viewModel.RescanCommand.Execute(null);
        }

        // El canal de la cabecera depende de si el listado muestra ya la barra
        // vertical; el ancho real solo se conoce despues del primer layout.
        Dispatcher.BeginInvoke(new Action(() => SyncHeaderGutter(null)), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Cuando aparece la barra vertical el listado pierde ese ancho por la derecha.
    /// En vez de suponer cuanto ocupa, se mide la geometria real de ambos elementos y
    /// se corrige el margen de la cabecera hasta que coinciden. Asi da igual si el
    /// ancho viene del sistema, del padding del listado o del propio riel.
    /// </summary>
    private void OnTreeScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.OriginalSource is ScrollViewer viewer) SyncHeaderGutter(viewer);
    }

    /// <summary>
    /// El ContextMenu vive en un arbol visual aparte, asi que no alcanza por
    /// RelativeSource hasta el ViewModel. Se le da antes de que se abra, con el
    /// boton derecho: preparar despues haria que el menu apareciese un instante con
    /// todos los elementos visibles y luego se encogiese. <c>Opened</c> queda como
    /// red de seguridad para abrirlo con el teclado.
    /// </summary>
    private void RowMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && DataContext is not null)
        {
            menu.DataContext = DataContext;
        }
    }

    private void OnTreeRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindRowMenu(e.OriginalSource as DependencyObject) is ContextMenu menu && DataContext is not null)
        {
            menu.DataContext = DataContext;
        }
    }

    private static ContextMenu? FindRowMenu(DependencyObject? source)
    {
        while (source is Visual visual)
        {
            if (visual is FrameworkElement element && element.ContextMenu is ContextMenu menu) return menu;

            source = VisualTreeHelper.GetParent(visual);
        }

        return null;
    }

    private void SyncHeaderGutter(ScrollViewer? viewer)
    {
        viewer ??= FindScrollViewer(Tree);
        if (viewer is null) return;

        var headerRight = HeaderRow.TranslatePoint(new Point(HeaderRow.ActualWidth, 0), Tree).X;
        var viewportRight = viewer.TranslatePoint(new Point(0, 0), Tree).X + viewer.Padding.Left + viewer.ViewportWidth;

        var delta = viewportRight - headerRight;
        if (Math.Abs(delta) < 0.5) return;

        var right = Math.Max(0, HeaderRow.Margin.Right - delta);
        HeaderRow.Margin = new Thickness(HeaderRow.Margin.Left, HeaderRow.Margin.Top, right, HeaderRow.Margin.Bottom);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is null || root is not Visual || root is not DependencyObject visualRoot) return null;

        var count = VisualTreeHelper.GetChildrenCount(visualRoot);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(visualRoot, i);

            if (child is ScrollViewer viewer) return viewer;

            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }

        return null;
    }

    private void OnDriveDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element) return;
        if (element.DataContext is not DriveItem drive) return;

        _viewModel.SelectedDrive = drive;
        _viewModel.ScanSelectedDriveCommand.Execute(drive);
    }

    /// <summary>
    /// Diferencia en pixeles entre el borde derecho del contenido de la cabecera y
    /// el borde derecho del area visible del listado. Cero significa que las cifras
    /// de cada columna caen justo debajo de su cabecera. Se expose para que el arnes
    /// pueda comprobarlo con numeros en vez de a ojo.
    /// </summary>
    public double HeaderAlignmentDelta
    {
        get
        {
            var viewer = FindScrollViewer(Tree);
            if (viewer is null) return double.NaN;

            var headerRight = HeaderRow.TranslatePoint(new Point(HeaderRow.ActualWidth, 0), Tree).X;
            var viewportRight = viewer.TranslatePoint(new Point(0, 0), Tree).X + viewer.Padding.Left + viewer.ViewportWidth;

            return Math.Round(headerRight - viewportRight, 2);
        }
    }

    /// <summary>Cifras crudas del riel de cabecera, para depurar la alineacion.</summary>
    public string HeaderLayoutInfo
    {
        get
        {
            var viewer = FindScrollViewer(Tree);
            if (viewer is null) return "sin visor";

            return $"margin=({HeaderRow.Margin.Left:0.#},{HeaderRow.Margin.Right:0.#}) " +
                   $"headerW={HeaderRow.ActualWidth:0.##} treeW={Tree.ActualWidth:0.##} " +
                   $"viewerW={viewer.ActualWidth:0.##} viewportW={viewer.ViewportWidth:0.##} " +
                   $"vScroll={viewer.VerticalScrollBarVisibility}";
        }
    }
}