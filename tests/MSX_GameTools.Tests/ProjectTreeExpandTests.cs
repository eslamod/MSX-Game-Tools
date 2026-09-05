using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El árbol del proyecto enseña lo que hay dentro sin que haya que ir a buscarlo.
/// </summary>
/// <remarks>
/// Los cajones —bancos, juegos de tiles, mapas— nacían cerrados, así que un documento
/// recién traído no se veía en ninguna parte del árbol. Y con él tampoco se veía su
/// asterisco: al cerrar la pestaña, el usuario se queda sin ninguna señal de que sigue ahí
/// y con cambios.
/// </remarks>
public class ProjectTreeExpandTests
{
    /// <summary>El cajón se abre al llegarle un documento.</summary>
    [AvaloniaFact]
    public void El_cajon_se_abre_al_traer_un_documento()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        ItemTree drawer = main.TreeGeneralVm.PrimaryNodes
            .Single(node => node.Tag == Constants.TAG_ID_NODE_TILESETS);

        Assert.False(drawer.IsExpanded);

        main.OpenTileSet(new TileSet("Bosque"));

        Assert.True(drawer.IsExpanded);
    }

    /// <summary>
    /// Y en el árbol de la pantalla, el documento se lee sin desplegar nada.
    /// </summary>
    /// <remarks>
    /// Montado porque el que abre el cajón es el control, y que obedezca al nodo depende de
    /// un enlace que sólo existe en el XAML.
    /// </remarks>
    [AvaloniaFact]
    public void El_documento_se_lee_en_el_arbol_sin_tocar_nada()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        string[] said = Rows(window);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(said, text => text.StartsWith("Bosque", StringComparison.Ordinal));
    }

    /// <summary>
    /// Cerrarlo a mano se respeta, y el siguiente documento lo vuelve a abrir.
    /// </summary>
    /// <remarks>
    /// Lo primero es lo que hace falta para que el enlace vaya en las dos direcciones: si
    /// sólo fuera de ida, el nodo se quedaría creyendo que está abierto. Y lo segundo es a
    /// lo que viene todo esto: lo que acaba de llegar hay que enseñarlo.
    /// </remarks>
    [AvaloniaFact]
    public void Cerrar_el_cajon_a_mano_se_respeta_hasta_que_llega_otro()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        ItemTree drawer = main.TreeGeneralVm.PrimaryNodes
            .Single(node => node.Tag == Constants.TAG_ID_NODE_TILESETS);

        TreeViewItem row = window.GetVisualDescendants()
            .OfType<TreeView>()
            .First()
            .GetRealizedContainers()
            .OfType<TreeViewItem>()
            .Single(item => ReferenceEquals(item.DataContext, drawer));

        row.IsExpanded = false;
        Dispatcher.UIThread.RunJobs();

        bool told = !drawer.IsExpanded;

        main.OpenTileSet(new TileSet("Cueva"));
        Dispatcher.UIThread.RunJobs();

        bool opened = row.IsExpanded;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(told, "Cerrar el cajon a mano tendria que llegar al nodo.");
        Assert.True(opened, "Al traer otro documento el cajon tendria que abrirse solo.");
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Lo que se lee en el árbol ahora mismo.</summary>
    private static string[] Rows(Window window) =>
    [
        .. window.GetVisualDescendants()
            .OfType<TreeView>()
            .First()
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty),
    ];
}
