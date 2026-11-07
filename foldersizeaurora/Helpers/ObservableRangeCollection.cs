using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FolderSizeAurora.Helpers;

/// <summary>
/// Coleccion observable que puede cambiar su contenido entero con un unico aviso.
///
/// Rellenar una lista de miles de elementos con Add uno a uno lanza un aviso por
/// elemento, y WPF reacciona a cada uno: medir, generar contenedor, invalidar. Con
/// el listado virtualizado eso no se nota al pintar, pero al insertar si, y al
/// expandir una unidad la ventana se quedaba pegada.
///
/// Con <see cref="ReplaceAll"/> se lanza un solo <see cref="NotifyCollectionChangedAction.Reset"/>:
/// el ListView descarta sus contenedores y vuelve a generar los que necesita, que es
/// justo lo que la virtualizacion sabe hacer.
/// </summary>
public sealed class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();

        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}