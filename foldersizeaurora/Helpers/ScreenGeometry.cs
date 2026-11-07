using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FolderSizeAurora.Helpers;

/// <summary>
/// Colocacion de la ventana contra el area de trabajo real del monitor.
///
/// El fallo que motiva este archivo: la ventana declaraba 1320x800 fijos y se
/// centraba en la pantalla. En un monitor cuyo area de trabajo es menor que eso
/// (1366x768 con barra de tareas, o 1920x1080 al 150 %, que deja 1280x720
/// utiles) el centrado empuja la parte superior y la izquierda fuera de la
/// pantalla: la barra superior de 48 px y el pie de 28 px desaparecen y el
/// carril lateral queda cortado.
///
/// La regla es sencilla: la ventana nunca puede medir mas que el area de trabajo
/// y nunca puede empezar antes de su esquina superior izquierda. El ajuste se
/// hace con numeros, no a ojo.
/// </summary>
internal static class ScreenGeometry
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    /// <summary>
    /// Devuelve el rectangulo que debe ocupar la ventana dentro del area de
    /// trabajo indicada. Es geometria pura: no toca la ventana ni la pantalla,
    /// asi que se puede comprobar con cualquier medida sin depender del equipo.
    /// </summary>
    public static Rect FitToWorkArea(
        Rect workArea,
        double preferredWidth,
        double preferredHeight,
        double minWidth,
        double minHeight)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return new Rect(0, 0, preferredWidth, preferredHeight);
        }

        // Si el area de trabajo es menor que el minimo, manda el area de trabajo.
        var minimumWidth = Math.Min(minWidth, workArea.Width);
        var minimumHeight = Math.Min(minHeight, workArea.Height);

        var width = Math.Max(Math.Min(preferredWidth, workArea.Width), minimumWidth);
        var height = Math.Max(Math.Min(preferredHeight, workArea.Height), minimumHeight);

        var left = workArea.Left + Math.Round((workArea.Width - width) / 2);
        var top = workArea.Top + Math.Round((workArea.Height - height) / 2);

        return new Rect(left, top, width, height);
    }

    /// <summary>
    /// Minimos que la ventana puede declarar: nunca por encima del area de
    /// trabajo, porque WPF impide encoger por debajo de MinWidth y eso volveria
    /// a sacar la ventana de la pantalla.
    /// </summary>
    public static (double Width, double Height) ClampMinimum(
        Rect workArea,
        double minWidth,
        double minHeight)
    {
        var width = workArea.Width > 0 ? Math.Min(minWidth, workArea.Width) : minWidth;
        var height = workArea.Height > 0 ? Math.Min(minHeight, workArea.Height) : minHeight;

        return (width, height);
    }

    /// <summary>
    /// Cantidad de pixeles, en cualquiera de los cuatro lados, que la ventana
    /// se sale del area de trabajo. Cero significa que cabe entera.
    /// </summary>
    public static double Overflow(Rect window, Rect workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0) return 0;

        var over = Math.Max(
            Math.Max(workArea.Left - window.Left, window.Right - workArea.Right),
            Math.Max(workArea.Top - window.Top, window.Bottom - workArea.Bottom));

        return Math.Round(Math.Max(0, over), 2);
    }

    /// <summary>
    /// Area de trabajo del monitor en el que esta la ventana, ya expresada en
    /// las mismas unidades que las propiedades de <see cref="Window"/> (DIP).
    /// Si Windows no responde se cae al area de trabajo del monitor principal,
    /// que WPF ya trae convertida.
    /// </summary>
    public static Rect WorkAreaInDips(IntPtr windowHandle, Visual? visual)
    {
        if (TryGetWorkAreaInPixels(windowHandle, out var pixels))
        {
            var scale = DeviceScale(visual);

            if (scale.Width > 0 && scale.Height > 0)
            {
                return new Rect(
                    pixels.X / scale.Width,
                    pixels.Y / scale.Height,
                    pixels.Width / scale.Width,
                    pixels.Height / scale.Height);
            }
        }

        return SystemParameters.WorkArea;
    }

    private static bool TryGetWorkAreaInPixels(IntPtr windowHandle, out Rect workArea)
    {
        workArea = default;

        if (windowHandle == IntPtr.Zero) return false;

        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return false;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;

        workArea = new Rect(
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top);

        return workArea.Width > 0 && workArea.Height > 0;
    }

    /// <summary>
    /// Factor de escala del monitor. Con DPI por monitor activado no basta con
    /// el 96 del sistema: a 150 % el area de trabajo en pixeles se divide.
    /// </summary>
    private static Size DeviceScale(Visual? visual)
    {
        if (visual is null) return new Size(1, 1);

        try
        {
            var dpi = VisualTreeHelper.GetDpi(visual);

            if (dpi.DpiScaleX > 0 && dpi.DpiScaleY > 0)
            {
                return new Size(dpi.DpiScaleX, dpi.DpiScaleY);
            }
        }
        catch
        {
            // Sin monitor que consultar se asume el 100 %; es la unica conjetura
            // que no puede empeorar la situacion porque el area de trabajo de
            // respaldo ya viene en DIP.
        }

        return new Size(1, 1);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }
}