using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

public partial class TreeGeneralView : UserControl
{
    public TreeGeneralView() => InitializeComponent();

    private TreeGeneralViewModel? ViewModel => DataContext as TreeGeneralViewModel;

    /// <summary>
    /// Doble clic sobre un nodo con panel: vuelve a enseñar su pestaña.
    /// </summary>
    /// <remarks>
    /// El gesto es doble clic y no sencillo a propósito: con uno solo, recorrer el árbol
    /// con el teclado o el ratón iría abriendo pestañas por el camino.
    /// </remarks>
    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ItemOf(e.Source) is { } item)
            ViewModel?.OpenItemCommand?.Execute(item);
    }

    private void OnOpenItem(object? sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
            ViewModel?.OpenItemCommand?.Execute(item);
    }

    private void OnDeleteItem(object? sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
            ViewModel?.DeleteItemCommand?.Execute(item);
    }

    /// <summary>
    /// El nodo sobre el que se ha hecho el gesto. Sale del DataContext del control que
    /// lo recibió, que en un TreeView es el que lleva el elemento de esa fila.
    /// </summary>
    private static ItemTree? ItemOf(object? source) =>
        (source as Control)?.DataContext as ItemTree;
}
