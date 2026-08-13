using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Cómo se llega al panel de bloques: cuelga de su juego de tiles en el árbol y se abre
/// en el lateral, para verlo a la vez que se dibujan los tiles.
/// </summary>
/// <remarks>
/// Por el comando y no construyendo el panel a mano: un valor por omisión sólo vale si
/// nadie lo pisa por el camino, y quien lo pisa suele estar en el punto de entrada.
/// </remarks>
public class TileBlocksTreeTests
{
    [AvaloniaFact]
    public void El_juego_de_tiles_trae_su_nodo_de_bloques()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        ItemTree node = BlocksNode(main);

        Assert.Equal("Bloques", node.DisplayText);
        Assert.True(node.IsPanelNode);

        // Los bloques no son un elemento aparte: se van con su juego y no se eliminan solos.
        Assert.False(node.CanDelete);
    }

    /// <summary>
    /// El botón del editor trae el mismo panel que el nodo del árbol.
    /// </summary>
    /// <remarks>
    /// Está porque un doble clic en un nodo no lo descubre nadie. Y tiene que traer el
    /// mismo, no uno nuevo: los bloques son del juego y sólo hay unos.
    /// </remarks>
    [AvaloniaFact]
    public void El_boton_de_bloques_trae_el_mismo_panel_que_el_arbol()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        var fromTree = (TileBlocksViewModel)main.RightPanViewModel!;

        main.CloseRightPanel(fromTree);

        Assert.Null(main.RightPanViewModel);

        main.ShowBlocksCommand.Execute(null);

        Assert.Same(fromTree, main.RightPanViewModel);
    }

    /// <summary>Con dos juegos abiertos, trae los del que esté delante.</summary>
    [AvaloniaFact]
    public void El_boton_trae_los_bloques_del_juego_que_esta_delante()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));

        TileSetEditorViewModel second = main.OpenTileSet(new TileSet("Cueva"));

        main.ShowBlocksCommand.Execute(null);

        var panel = (TileBlocksViewModel)main.RightPanViewModel!;

        Assert.Same(second.TileSet, panel.TileSet);
    }

    [AvaloniaFact]
    public void Sin_un_juego_delante_el_boton_esta_apagado()
    {
        var main = new MainWindowViewModel();

        Assert.False(main.ShowBlocksCommand.CanExecute(null));

        main.OpenTileSet(new TileSet("Bosque"));

        Assert.True(main.ShowBlocksCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Abrir_el_nodo_lo_pone_en_el_lateral_y_no_en_una_pestana()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        var panel = Assert.IsType<TileBlocksViewModel>(main.RightPanViewModel);

        Assert.EndsWith(": Bosque", panel.Header, StringComparison.Ordinal);
        Assert.Contains(panel, main.RightPanels);

        // La pestaña del centro sigue siendo el editor, que es lo que se quiere ver a la vez.
        Assert.Single(main.Tabs);
    }

    [AvaloniaFact]
    public void Volver_a_abrirlo_no_duplica_el_panel()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        TileBlocksViewModel panel = (TileBlocksViewModel)main.RightPanViewModel!;
        panel.AddBlockCommand.Execute(null);

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        Assert.Single(main.RightPanels);
        Assert.Same(panel, main.RightPanViewModel);
        Assert.Single(panel.Blocks);
    }

    [AvaloniaFact]
    public void La_cruz_cierra_el_panel_y_el_nodo_lo_trae_de_vuelta()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTreeItemCommand.Execute(BlocksNode(main));
        ((TileBlocksViewModel)main.RightPanViewModel!).AddBlockCommand.Execute(null);

        main.CloseRightPanelCommand.Execute(main.RightPanViewModel);

        Assert.Empty(main.RightPanels);
        Assert.Null(main.RightPanViewModel);

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        // El mismo panel con lo que tenía, como al reabrir una pestaña.
        Assert.Single(((TileBlocksViewModel)main.RightPanViewModel!).Blocks);
    }

    /// <summary>
    /// Un formulario de un uso y una herramienta conviven: crear un juego con los bloques
    /// abiertos no se los lleva por delante.
    /// </summary>
    [AvaloniaFact]
    public void Un_formulario_no_cierra_la_herramienta_que_hubiera_puesta()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));
        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        main.AddTileSetCommand.Execute(null);

        Assert.Equal(2, main.RightPanels.Count);
        Assert.IsType<EditTileSetViewModel>(main.RightPanViewModel);

        // Y al cerrarse el formulario vuelve a verse la herramienta.
        main.RightPanViewModel = null;

        Assert.IsType<TileBlocksViewModel>(main.RightPanViewModel);
    }

    [AvaloniaFact]
    public async Task Eliminar_el_juego_se_lleva_sus_bloques()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });
        main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        await main.DeleteTreeItemCommand.ExecuteAsync(TileSetNode(main));

        Assert.Empty(main.RightPanels);
        Assert.Null(main.RightPanViewModel);
        Assert.Empty(main.TreeGeneralVm.PrimaryNodes[1].Childs);
    }

    /// <summary>El nodo de bloques no ofrece eliminar, así que el comando tampoco lo hace.</summary>
    [AvaloniaFact]
    public async Task El_nodo_de_bloques_no_se_puede_eliminar()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });
        main.OpenTileSet(new TileSet("Bosque"));

        await main.DeleteTreeItemCommand.ExecuteAsync(BlocksNode(main));

        Assert.Single(main.TreeGeneralVm.PrimaryNodes[1].Childs);
        Assert.Single(TileSetNode(main).Childs);
    }

    private static ItemTree TileSetNode(MainWindowViewModel main) =>
        main.TreeGeneralVm.PrimaryNodes[1].Childs[0];

    private static ItemTree BlocksNode(MainWindowViewModel main) => TileSetNode(main).Childs[0];
}
