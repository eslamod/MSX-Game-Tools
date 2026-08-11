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

    [AvaloniaFact]
    public void Abrir_el_nodo_lo_pone_en_el_lateral_y_no_en_una_pestana()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTreeItemCommand.Execute(BlocksNode(main));

        var panel = Assert.IsType<TileBlocksViewModel>(main.RightPanViewModel);

        Assert.Equal("Bloques de Bosque", panel.Header);
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
