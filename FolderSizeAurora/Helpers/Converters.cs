using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FolderSizeAurora.Helpers;

/// <summary>bool -> Visibility. Usar ConverterParameter="invert" para invertir.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (IsInverted(parameter)) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible ^ IsInverted(parameter);

    internal static bool IsInverted(object? parameter) =>
        parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
}

/// <summary>null -> Collapsed, cualquier otro valor -> Visible.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Convierte (fraccion 0-1, ancho maximo) en un ancho en px.
/// Se usa con MultiBinding: primer valor = fraccion, segundo = ancho disponible.
/// </summary>
public sealed class FractionToWidthConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        double fraction = 0;
        double max = 100;

        if (values is { Length: >= 1 } && values[0] is not null)
        {
            fraction = values[0] switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                decimal m => (double)m,
                _ => 0d
            };
        }

        if (values is { Length: >= 2 } && values[1] is not null)
        {
            max = values[1] switch
            {
                double d => d,
                float f => f,
                int i => i,
                _ => 100d
            };
        }
        else if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            max = parsed;
        }

        if (double.IsNaN(fraction) || fraction <= 0) return 0d;
        if (fraction > 1) fraction = 1;
        if (max <= 0) return 0d;

        // Un minimo de 2 px hace visible cualquier valor distinto de cero.
        return Math.Max(2d, fraction * max);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => Array.Empty<object>();
}

/// <summary>Indice de paleta (0-5) -> pincel del recurso indicado.</summary>
public sealed class PaletteBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var index = value is int i ? Math.Abs(i) % 6 : 0;
        var key = $"Palette.{index}";
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.SteelBlue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Estado de una fila de monitoreo o de una alerta -> pincel (normal / alerta / error).</summary>
public sealed class StateBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = "Ink";

        switch (value)
        {
            case bool exceeded when exceeded:
                key = "Danger";
                break;
            case Services.MonitorStatus status when status.Exceeded:
                key = "Danger";
                break;
            case Services.MonitorStatus status when status.HasError:
                key = "Ink3";
                break;
            case Models.AlertItem alert when alert.IsError:
                key = "Ink3";
                break;
        }

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Nombre legible de un tipo de filtro.</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum e) return string.Empty;

        return e switch
        {
            Models.SortMode.Size => "Tamano",
            Models.SortMode.Name => "Nombre",
            Models.SortMode.Files => "Cantidad de archivos",
            Models.SortMode.Date => "Ultima modificacion",
            Models.TypeFilter.All => "Todos los tipos",
            Models.TypeFilter.Images => "Imagenes",
            Models.TypeFilter.Video => "Video",
            Models.TypeFilter.Audio => "Audio",
            Models.TypeFilter.Documents => "Documentos",
            Models.TypeFilter.Archives => "Comprimidos",
            Models.TypeFilter.Code => "Codigo fuente",
            Models.TypeFilter.DiskImages => "Imagenes de disco",
            Models.TypeFilter.Executables => "Ejecutables",
            _ => e.ToString()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Sangria del nivel (en px) -> margen izquierdo de la celda de nombre.
/// El ConverterParameter es el margen derecho que acompana a la sangria, para
/// poder escribir Margin="{Binding Indent, Converter=..., ConverterParameter=10}"
/// sin anidar otro Grid.
/// </summary>
public sealed class IndentToMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var px = value switch
        {
            double d => d,
            int i => i,
            _ => 0d
        };

        if (double.IsNaN(px) || px < 0) px = 0;

        var right = 0d;
        if (parameter is string s &&
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            right = Math.Max(0, parsed);
        }

        return new Thickness(px, 0, right, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Carril lateral plegado (44 px, solo iconos) o abierto (288 px).</summary>
public sealed class SidebarWidthConverter : IValueConverter
{
    public const double Collapsed = 44;
    public const double Expanded = 288;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string s &&
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var explicitWidth))
        {
            return value is bool collapsed && collapsed ? explicitWidth : Expanded;
        }

        return value is bool b && b ? Collapsed : Expanded;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Indicador de orden de una cabecera. El ViewModel publica el estado como
/// "Columna|sentido"; aqui solo se compara con el ConverterParameter de cada
/// cabecera. Asi la logica de orden no vive en el codep-behind.
/// </summary>
public sealed class SortArrowConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string current || parameter is not string wanted) return Visibility.Collapsed;

        return current.Equals(wanted, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Proporcion 0-1 -> pincel segun el margen que queda.</summary>
public sealed class LevelBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value switch
        {
            double d => d,
            int i => i,
            float f => f,
            _ => 0d
        };

        if (double.IsNaN(fraction)) fraction = 0;

        // El rojo se reserva para lo que esta a punto de romperse; lo simplemente lleno
        // se avisa en ambar.
        var key = fraction >= 0.95 ? "Danger"
            : fraction >= 0.85 ? "Warn"
            : "Accent";

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.SteelBlue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Bandera booleana -> pincel. true = Danger, false = Accent.
/// Con ConverterParameter="ok", true = Ok y false = Ink3.
/// </summary>
public sealed class FlagBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        var positive = parameter is string s && s.Equals("ok", StringComparison.OrdinalIgnoreCase);

        var key = positive
            ? (flag ? "Ok" : "Ink3")
            : (flag ? "Danger" : "Accent");

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.SteelBlue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Cantidad de elementos -> Visible cuando no hay ninguno (estado vacio).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            double d => (int)d,
            _ => -1
        };

        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Enum -> Visible cuando su nombre coincide con el ConverterParameter.</summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return Visibility.Collapsed;

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}