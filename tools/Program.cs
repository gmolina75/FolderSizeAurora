using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FolderSizeAurora;
using FolderSizeAurora.Models;
using FolderSizeAurora.Services;
using FolderSizeAurora.ViewModels;

namespace UiCheck;

/// <summary>
/// Recoge los mensajes de diagnostico de binding de WPF.
/// Los errores de binding solo se emiten en compilaciones Debug, por eso el
/// arnes se fuerza a Debug.
/// </summary>
internal sealed class Collector : TraceListener
{
    public List<string> Messages { get; } = new();

    public override void Write(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message) && !message.Contains("SynchronizationContext"))
        {
            Messages.Add(message);
        }
    }

    public override void WriteLine(string? message) => Write(message);
}

internal static class Program
{
    private static readonly string[] RealErrorMarkers =
    {
        "read-only",
        "TwoWay",
        "OneWayToSource",
        "Cannot find",
        "Cannot get",
        "was not found",
        "Exception",
        "does not exist",
        "RelativeSource",
        "FrameworkElement",
        "BindingExpression",
        "is not a valid"
    };

    private static readonly string[] NoiseMarkers =
    {
        "no valid fallback value exists",
        "SynchronizationContext"
    };

    private static readonly string SampleFolder =
        @"Z:\Customers\Demo\4.1\win32\GEN_MODULES\Process\foldersizeAurora";

    private static readonly string ShotFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fsa-shots");

    private static readonly string[] StallSamples =
    {
        @"C:\Windows\System32",
        @"C:\Program Files"
    };

    private sealed class LongBox
    {
        private long _value;
        public long Value => Interlocked.Read(ref _value);
        public void Update(double milliseconds)
        {
            var v = (long)milliseconds;
            while (v > Interlocked.Read(ref _value))
            {
                var seen = Interlocked.Read(ref _value);
                if (v <= seen || Interlocked.CompareExchange(ref _value, v, seen) == seen) return;
            }
        }
    }

    [STAThread]
    private static int Main()
    {
        var collector = new Collector();

        PresentationTraceSources.Refresh();
        var source = PresentationTraceSources.DataBindingSource;
        source.Listeners.Add(collector);
        source.Switch.Level = SourceLevels.All;

        var exitCode = 0;
        var settledAt = 0;
        var settingsService = new SettingsService();
        var originalSettings = File.Exists(settingsService.SettingsPath)
            ? File.ReadAllText(settingsService.SettingsPath)
            : null;

        try
        {
            Directory.CreateDirectory(ShotFolder);

            var placementFailures = CheckPlacementGeometry();
            if (placementFailures > 0) exitCode = 1;

            SeedSettings(settingsService);

            var app = new App();
            app.InitializeComponent();

            var window = new MainWindow();
            window.Show();

            // Todo lo que WPF emite antes de que el code-behind asigne el DataContext
            // son avisos transitorios: el binding se resuelve en cuanto hay contexto.
            // A partir de aqui cualquier error es real.
            settledAt = collector.Messages.Count;

            Pump(window.Dispatcher, 400);

            var vm = (MainViewModel)window.DataContext;
            Console.WriteLine($"DataContext : {vm.GetType().FullName}");
            Console.WriteLine($"IsLoaded    : {window.IsLoaded}");
            Console.WriteLine($"Colocacion  : {window.PlacementInfo}");

            // La ventana real, en el monitor real de este equipo.
            if (window.PlacementOverflow > 0)
            {
                Console.WriteLine($"FALLO REAL DE COLOCACION: se sale {window.PlacementOverflow:0.##} px del area de trabajo.");
                exitCode = 1;
            }
            else
            {
                Console.WriteLine("Colocacion  : la ventana cabe entera en el area de trabajo.");
            }

            // Un analisis real: sin filas el listado y los graficos quedan vacios.
            RunScan(window.Dispatcher, vm);
            Console.WriteLine($"Filas       : {vm.Rows.Count}");
            Console.WriteLine($"Graficos    : carpetas={vm.TopFolders.Count} archivos={vm.TopFiles.Count} extensiones={vm.ByExtension.Count}");

            SeedActivity(vm);
            Pump(window.Dispatcher, 250);

            var tab = FindTabControl(window);
            if (tab is null)
            {
                Console.WriteLine("NO SE ENCONTRO EL TabControl");
                return 2;
            }

            Report(collector, settledAt, "instanciado");
            Console.WriteLine($"VM StatusText  ahora: {vm.StatusText}");
            Console.WriteLine($"VM ScanSummary ahora: {vm.ScanSummary}");
            Console.WriteLine($"RebuildCount: {vm.RebuildCount}");

            Pump(window.Dispatcher, 1200);
            Console.WriteLine($"RebuildCount tras 1,2 s en reposo: {vm.RebuildCount}  (debe ser el mismo)");
            DumpTexts(window, "·");

            // 1. Explorador, tema claro, tamano por defecto
            Select(tab, 0);
            Pump(window.Dispatcher, 300);
            Shot(window, "01-explorador-claro.png");
            Console.WriteLine($"Alineacion cabecera: {window.HeaderAlignmentDelta:0.##} px");
            Console.WriteLine($"  {window.HeaderLayoutInfo}");

            // 2. Explorador, tema oscuro
            vm.IsDark = true;
            Pump(window.Dispatcher, 350);
            Shot(window, "02-explorador-oscuro.png");

            // 3. Graficos
            vm.IsDark = false;
            Select(tab, 1);
            Pump(window.Dispatcher, 300);
            Shot(window, "03-graficos.png");

            // 4. Vigilancia
            Select(tab, 2);
            Pump(window.Dispatcher, 300);
            Shot(window, "04-vigilancia.png");

            // 5. Ancho minimo: aqui es donde un diseño se sale de la pantalla
            Select(tab, 0);
            window.Width = 1024;
            window.Height = 620;
            Pump(window.Dispatcher, 350);
            Shot(window, "05-ancho-minimo.png");
            Console.WriteLine($"Alineacion cabecera (1024): {window.HeaderAlignmentDelta:0.##} px");

            // 6. Area de trabajo tipica de un portatil a 125 %: 1600x900 fisico son
            // 1280x720 utiles. Es el tamaño al que abre la app en un monitor pequeño.
            window.Width = 1280;
            window.Height = 720;
            Pump(window.Dispatcher, 350);
            Shot(window, "06-area-de-trabajo-1280.png");
            Console.WriteLine($"Alineacion cabecera (1280): {window.HeaderAlignmentDelta:0.##} px");

            // 7. Carril plegado
            window.Width = MainWindow.PreferredWidth;
            window.Height = MainWindow.PreferredHeight;
            vm.IsSidebarCollapsed = true;
            Pump(window.Dispatcher, 350);
            Shot(window, "07-carril-plegado.png");
            vm.IsSidebarCollapsed = false;
            Pump(window.Dispatcher, 250);

            // 8. Aviso flotante
            ShowToast(vm);
            Pump(window.Dispatcher, 300);
            Shot(window, "08-aviso.png");
            HideToast(vm);
            Pump(window.Dispatcher, 200);

            // 9. Pantalla pequeña. Se bajan los minimos a mano para poder imitar
            // un portatil de 900x600: hay que comprobar que la barra de 48 px y el
            // pie de 28 px siguen visibles y que nada se pisa al encoger.
            var savedMinWidth = window.MinWidth;
            var savedMinHeight = window.MinHeight;
            window.MinWidth = 640;
            window.MinHeight = 420;
            window.Width = 900;
            window.Height = 600;
            Pump(window.Dispatcher, 350);
            Shot(window, "09-pantalla-pequena.png");
            Console.WriteLine($"Alineacion cabecera (900): {window.HeaderAlignmentDelta:0.##} px");
            window.Width = MainWindow.PreferredWidth;
            window.Height = MainWindow.PreferredHeight;
            window.MinWidth = savedMinWidth;
            window.MinHeight = savedMinHeight;
            Pump(window.Dispatcher, 250);

            // Recorrido final de las tres pestañas para instanciar todos los VisualTree.
            for (var i = 0; i < tab.Items.Count; i++)
            {
                Select(tab, i);
                Pump(window.Dispatcher, 150);
            }

            Console.WriteLine($"Capturas    : {ShotFolder}");

            // Bloqueo de la interfaz durante un escaneo grande. Es lo que el usuario
            // describe como "se queda freezeada": la ventana no repinta y no responde.
            MeasureUiStallDuringScan(window.Dispatcher, vm);

            Report(collector, settledAt, "recorrido completo");

            window.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPCION AL CARGAR LA VENTANA:");
            Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            exitCode = 2;
        }
        finally
        {
            try
            {
                if (originalSettings is null)
                {
                    File.Delete(settingsService.SettingsPath);
                }
                else
                {
                    File.WriteAllText(settingsService.SettingsPath, originalSettings);
                }

                Console.WriteLine("Ajustes del usuario restaurados.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Aviso: no se pudieron restaurar los ajustes ({ex.Message}).");
            }
        }

        var bindingErrors = collector.Messages
            .Skip(settledAt)
            .Where(m => RealErrorMarkers.Any(n => m.Contains(n, StringComparison.OrdinalIgnoreCase))
                     && !NoiseMarkers.Any(n => m.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Console.WriteLine($"Mensajes totales : {collector.Messages.Count}");
        Console.WriteLine($"Mensajes tras asentar: {collector.Messages.Count - settledAt}");
        Console.WriteLine($"ERRORES REALES   : {bindingErrors.Count}");

        if (bindingErrors.Count == 0)
        {
            Console.WriteLine("RESULTADO: sin errores de binding.");
            return exitCode;
        }

        Console.WriteLine("RESULTADO: se detectaron errores de binding:");
        foreach (var error in bindingErrors.Take(40))
        {
            Console.WriteLine("  - " + error.Trim());
        }

        return 1;
    }

    /// <summary>
    /// Mide cuanto tiempo pasa el hilo de la interfaz sin responder mientras se
    /// escanea una carpeta de verdad. Un dispatcher que responde al instante no esta
    /// congelado; una llamada que tarda cientos de milisegundos es la ventana muerta
    /// que el usuario ve. Se mide con una sonda en otro hilo que pide una ejecucion
    /// inmediata al dispatcher y cronometra la espera.
    /// </summary>
    private static void MeasureUiStallDuringScan(Dispatcher dispatcher, MainViewModel vm)
    {
        foreach (var folder in StallSamples)
        {
            MeasureUiStall(dispatcher, vm, folder);
        }
    }

    private static void MeasureUiStall(Dispatcher dispatcher, MainViewModel vm, string StallSampleFolder)
    {
        if (!Directory.Exists(StallSampleFolder))
        {
            Console.WriteLine($"[bloqueo] no existe {StallSampleFolder}, se omite.");
            return;
        }

        var method = typeof(MainViewModel).GetMethod("ScanAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            Console.WriteLine("[bloqueo] no se encontro ScanAsync.");
            return;
        }

        var worst = new LongBox();
        var watch = Stopwatch.StartNew();

        // Invoke devuelve en cuanto el hilo de la interfaz ejecuta la accion. Si
        // esta ocupado escaneando, el tiempo que tarda aqui es el tiempo que la
        // ventana lleva sin repintar.
        var task = method.Invoke(vm, new object?[] { StallSampleFolder }) as Task;
        if (task is null)
        {
            Console.WriteLine("[bloqueo] ScanAsync no devolvio una tarea.");
            return;
        }

        var probe = new Thread(() =>
        {
            while (!task.IsCompleted)
            {
                var start = Stopwatch.GetTimestamp();
                try
                {
                    dispatcher.Invoke(() => { }, DispatcherPriority.Send);
                }
                catch
                {
                    return;
                }

                worst.Update((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                Thread.Sleep(2);
            }
        })
        { IsBackground = true, Name = "sonda-interfaz" };

        probe.Start();

        while (!task.IsCompleted)
        {
            try { dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle); }
            catch { break; }
            Thread.Sleep(2);
        }

        watch.Stop();
        probe.Join(3000);

        if (task.IsFaulted)
        {
            Console.WriteLine($"[bloqueo] el escaneo fallo: {task.Exception?.GetBaseException().Message}");
            return;
        }

        Console.WriteLine($"[bloqueo] carpeta            : {StallSampleFolder}");
        Console.WriteLine($"[bloqueo] escaneo completo   : {watch.Elapsed.TotalMilliseconds:N0} ms");
        Console.WriteLine($"[bloqueo] peor sin responder : {worst.Value:N0} ms");
        Console.WriteLine($"[bloqueo] veredicto          : {(worst.Value > 500 ? "LA VENTANA SE CONGELA" : "la interfaz aguanta")}");

        // El caso que reporta el usuario: desplegar la unidad. Es el peor momento
        // posible, porque el listado entero pasa de unas pocas filas a miles de golpe.
        var expandWorst = new LongBox();
        using var stop = new ManualResetEventSlim(false);

        var expandProbe = new Thread(() =>
        {
            while (!stop.IsSet)
            {
                var start = Stopwatch.GetTimestamp();
                try
                {
                    dispatcher.Invoke(() => { }, DispatcherPriority.Send);
                }
                catch
                {
                    return;
                }

                expandWorst.Update((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                Thread.Sleep(2);
            }
        })
        { IsBackground = true, Name = "sonda-desplegar" };

        expandProbe.Start();

        var expandWatch = Stopwatch.StartNew();
        vm.ExpandAllCommand.Execute(null);
        expandWatch.Stop();

        // Construir las filas es una cosa; el trabajo de WPF al medirlas y pintarlas es
        // otra, y ocurre despues, en el siguiente paso del dispatcher. Se sigue
        // sondeando un rato para medir tambien esa parte, que es donde se leia
        // «se freezea al desplegar».
        var settle = Stopwatch.StartNew();
        while (settle.ElapsedMilliseconds < 2500)
        {
            try { dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle); }
            catch { break; }
            Thread.Sleep(2);
        }

        settle.Stop();

        stop.Set();
        expandProbe.Join(3000);

        Console.WriteLine($"[desplegar] filas visibles   : {vm.Rows.Count:N0} (tope {MainViewModel.MaxRows:N0})");
        Console.WriteLine($"[desplegar] construir filas  : {expandWatch.Elapsed.TotalMilliseconds:N0} ms");
        Console.WriteLine($"[desplegar] ventana de sondeo : {settle.Elapsed.TotalMilliseconds:N0} ms");
        Console.WriteLine($"[desplegar] peor sin responder: {expandWorst.Value:N0} ms");
        Console.WriteLine($"[desplegar] veredicto        : {(expandWorst.Value > 500 ? "LA VENTANA SE CONGELA" : "la interfaz aguanta")}");
        Console.WriteLine($"[desplegar] pie              : {vm.StatusText}");
    }

    private static int CheckPlacementGeometry()
    {
        // El defecto reportado era que al abrir, la ventana se salia de la
        // pantalla y la barra superior y el pie quedaban fuera. Aqui se comprueba
        // la misma geometria con areas de trabajo pequenas, con escalado DPI y en
        // monitores secundarios: no hace falta tocar el hardware para reproducirlo.
        var cases = new (string Name, Rect Work)[]
        {
            ("1920x1080 al 100 %", new Rect(0, 0, 1920, 1040)),
            ("1366x768 con barra de tareas", new Rect(0, 0, 1366, 728)),
            ("1920x1080 al 150 % (1280x720 utiles)", new Rect(0, 0, 1280, 680)),
            ("portatil 1024x600", new Rect(0, 0, 1024, 560)),
            ("monitor antiguo 800x600", new Rect(0, 0, 800, 560)),
            ("segundo monitor a la izquierda", new Rect(-1920, 0, 1920, 1040)),
            ("segundo monitor por encima", new Rect(1920, -240, 1600, 1200))
        };

        var failures = 0;

        foreach (var (name, work) in cases)
        {
            var rect = MainWindow.FitToWorkArea(work);

            var inside =
                rect.Left >= work.Left - 0.5 &&
                rect.Top >= work.Top - 0.5 &&
                rect.Right <= work.Right + 0.5 &&
                rect.Bottom <= work.Bottom + 0.5;

            var capped = rect.Width <= work.Width && rect.Height <= work.Height;

            var centered =
                Math.Abs((rect.Left - work.Left) - (work.Right - rect.Right)) <= 1 &&
                Math.Abs((rect.Top - work.Top) - (work.Bottom - rect.Bottom)) <= 1;

            var ok = inside && capped && centered;

            if (!ok) failures++;

            Console.WriteLine(
                $"  [{(ok ? "OK   " : "FALLO")}] {name,-42} -> {rect.Width:0}x{rect.Height:0} en ({rect.X:0},{rect.Y:0})");
        }

        Console.WriteLine(failures == 0
            ? "Colocacion geometrica: la ventana cabe entera en los 7 escenarios."
            : $"Colocacion geometrica: {failures} escenarios se salen del area de trabajo.");

        return failures;
    }

    private static void DumpTexts(DependencyObject root, string filter)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is TextBlock block &&
                !string.IsNullOrEmpty(block.Text) &&
                block.Text.Contains(filter, StringComparison.Ordinal))
            {
                Console.WriteLine($"   TEXTO «{block.Text}»  ctx={block.DataContext?.GetType().Name ?? "null"} id={RuntimeHelpers.GetHashCode(block.DataContext)}");
            }

            DumpTexts(child, filter);
        }
    }

    private static void Report(Collector collector, int from, string phase)
    {
        var slice = collector.Messages.Skip(from).ToList();

        var errors = slice
            .Where(m => RealErrorMarkers.Any(n => m.Contains(n, StringComparison.OrdinalIgnoreCase))
                     && !NoiseMarkers.Any(n => m.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var distinct = errors
            .Select(e => e.Contains("; target element is") ? e[..e.IndexOf("; target element is", StringComparison.Ordinal)] : e)
            .Distinct()
            .Take(6)
            .ToList();

        Console.WriteLine($"--- fase '{phase}': {errors.Count} errores ---");

        foreach (var error in distinct)
        {
            Console.WriteLine("      " + error.Trim());
        }
    }

    private static void SeedSettings(SettingsService service)
    {
        var settings = service.Load();
        // LastPath vacio: si no, la ventana autoescanea al cargar y el analisis del
        // arnes se solaparia con ese, dejando la vista en un estado intermedio.
        settings.LastPath = null;
        settings.DarkTheme = false;
        settings.IncludeSystemHidden = false;
        settings.NotifyEmail = true;
        settings.NotifyWebhook = true;
        settings.SmtpHost = "smtp.ejemplo.local";
        settings.SmtpUser = "aurora";
        settings.EmailFrom = "aurora@ejemplo.local";
        settings.EmailTo = "sistemas@ejemplo.local";
        settings.WebhookUrl = "https://ejemplo.local/hook/espacio";
        settings.MonitorRules.Clear();
        settings.MonitorRules.Add(new MonitorRule
        {
            Path = @"Z:\Customers\Demo\4.1\win32\GEN_MODULES\Process\foldersizeAurora\TreeSizeClone",
            Label = "TreeSizeClone",
            Kind = MonitorKind.Folder,
            LimitBytes = 100L * 1024 * 1024,
            Mode = MonitorLimitMode.Bytes
        });
        settings.MonitorRules.Add(new MonitorRule
        {
            Path = "Z:\\",
            Label = "Unidad de trabajo (Z:)",
            Kind = MonitorKind.Drive,
            LimitBytes = 88,
            Mode = MonitorLimitMode.Percent
        });
        service.Save(settings);
    }

    private static void RunScan(Dispatcher dispatcher, MainViewModel vm)
    {
        var method = typeof(MainViewModel).GetMethod("ScanAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            Console.WriteLine("AVISO: no se encontro ScanAsync, se dibuja sin datos.");
            return;
        }

        var task = method.Invoke(vm, new object?[] { SampleFolder }) as Task;
        if (task is null) return;

        var deadline = Environment.TickCount64 + 90_000;
        while (!task.IsCompleted && Environment.TickCount64 < deadline)
        {
            Pump(dispatcher, 100);
        }

        if (!task.IsCompleted) Console.WriteLine("AVISO: el analisis no termino a tiempo.");

        if (task.IsFaulted)
        {
            Console.WriteLine("FALLO REAL EN EL ANALISIS:");
            Console.WriteLine(task.Exception?.GetBaseException().Message);
            Console.WriteLine(task.Exception?.GetBaseException().StackTrace);
        }

        Console.WriteLine($"ScannedSummary: {vm.ScanSummary}");
        Console.WriteLine($"StatusText   : {vm.StatusText}");
    }

    private static void SeedActivity(MainViewModel vm)
    {
        vm.Alerts.Insert(0, new AlertItem(
            DateTime.Now.AddMinutes(-3), @"Z:\Customers\Demo\4.1\...\TreeSizeClone",
            "La carpeta supera el limite de 100 MB.", 118_300_000, 104_857_600));

        vm.Alerts.Insert(0, new AlertItem(
            DateTime.Now.AddMinutes(-1), @"Z:\Customers\Demo\4.1\...\foldersizeAurora",
            "No se pudo leer una subcarpeta por permisos. Se continua con el resto.",
            0, 0, isError: true));

        vm.Notifications.Insert(0, new NotificationRow(DateTime.Now.AddMinutes(-1), "Correo", true, "SMTP smtp.ejemplo.local: accepted"));
        vm.Notifications.Insert(0, new NotificationRow(DateTime.Now.AddMinutes(-1), "Webhook", true, "HTTP 200 en 143 ms"));
        vm.Notifications.Insert(0, new NotificationRow(DateTime.Now.AddMinutes(-3), "Sonido", false, "No se encontro el recurso de sonido"));
    }

    private static void ShowToast(MainViewModel vm)
    {
        var property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.Toast));
        property?.GetSetMethod(nonPublic: true)?.Invoke(vm, new object?[]
        {
            new AlertItem(DateTime.Now, @"Z:\Customers\Demo\4.1\win32\GEN_MODULES\Process\foldersizeAurora",
                "Se ha superado el limite configurado para esta carpeta.", 118_300_000, 104_857_600)
        });
    }

    private static void HideToast(MainViewModel vm)
    {
        var property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.Toast));
        property?.GetSetMethod(nonPublic: true)?.Invoke(vm, new object?[] { null });
    }

    private static void Select(TabControl tab, int index)
    {
        if (index >= 0 && index < tab.Items.Count) tab.SelectedIndex = index;
    }

    private static void Pump(Dispatcher dispatcher, int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;

        while (Environment.TickCount64 < deadline)
        {
            dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
            Thread.Sleep(5);
        }
    }

    private static void Shot(Window window, string name)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);

        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);

        if (width <= 0 || height <= 0)
        {
            Console.WriteLine($"[shot] {name}: la ventana no tiene tamano, se omite.");
            return;
        }

        Visual target = window;
        try
        {
            var probe = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
            probe.Render(window);
        }
        catch
        {
            if (window.Content is Visual fallback) target = fallback;
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(target);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(Path.Combine(ShotFolder, name));
        encoder.Save(stream);

        Console.WriteLine($"[shot] {name}  {width}x{height}");
    }

    private static TabControl? FindTabControl(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is TabControl tab) return tab;

            var nested = FindTabControl(child);
            if (nested is not null) return nested;
        }

        return null;
    }
}