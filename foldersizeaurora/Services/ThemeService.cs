using System;
using System.Windows;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>
/// Cambia el tema en caliente: sustituye el diccionario de pinceles por el del tema pedido.
/// Los estilos consumen los pinceles con DynamicResource, por lo que el cambio es inmediato.
/// </summary>
public sealed class ThemeService
{
    public ThemeService() => Apply(false);

    public bool IsDark { get; private set; }

    public void Apply(bool dark)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var themeUri = new Uri(dark ? "/Themes/Dark.xaml" : "/Themes/Light.xaml", UriKind.Relative);
        var dictionaries = resources.MergedDictionaries;

        // Quita cualquier diccionario de tema previo para no acumularlos.
        for (int i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source;
            if (source is not null && (source.OriginalString.Contains("Light.xaml", StringComparison.OrdinalIgnoreCase)
                                    || source.OriginalString.Contains("Dark.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Insert(0, new ResourceDictionary { Source = themeUri });
        IsDark = dark;
    }

    public void Toggle() => Apply(!IsDark);
}