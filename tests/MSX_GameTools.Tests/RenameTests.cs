using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Cambiarle el nombre a un documento desde el panel de propiedades.
/// </summary>
/// <remarks>
/// El nombre sale en más sitios de los que parece: la pestaña, el árbol, el panel de
/// bloques de un juego de tiles y el nombre que cada uno de sus mapas lleva apuntado.
/// Renombrar tiene que arrastrarlo a todos; lo que no toca es el fichero, que es de quien
/// lo guardó y no del documento.
/// </remarks>
public class RenameTests
{
    [AvaloniaFact]
    public void Renombrar_cambia_la_pestana_y_el_arbol()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        tiles.MarkClean();

        Rename(main, tiles, "Bosque de noche");

        Assert.Equal("Bosque de noche", tiles.TileSet.Name);
        Assert.Equal("Bosque de noche (TS)", tiles.Header);
        Assert.Equal("Bosque de noche (TS)", NodeOf(main, tiles).DisplayText);

        // Y el juego queda sin guardar, que su fichero acaba de cambiar.
        Assert.True(tiles.IsModified);
        Assert.True(tiles.HasUnsavedChanges());
    }

    [AvaloniaFact]
    public void Renombrar_un_banco_y_un_mapa_tambien_vale()
    {
        var main = new MainWindowViewModel();

        SpritesEditorViewModel bank = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"));
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel map = main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);

        Rename(main, bank, "Enemigos");
        Rename(main, map, "Nivel 2");

        Assert.Equal("Enemigos (SP)", bank.Header);
        Assert.Equal("Nivel 2 (MP)", map.Header);
        Assert.Equal("Nivel 2", map.Map.Name);
    }

    /// <summary>
    /// Los mapas señalan el juego por identidad, así que no se pierden; pero llevan
    /// apuntado su nombre y hay que ponerlo al día.
    /// </summary>
    [AvaloniaFact]
    public void Renombrar_un_juego_pone_al_dia_sus_mapas()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel map = main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);

        map.MarkClean();

        Rename(main, tiles, "Bosque de noche");

        Assert.Equal("Bosque de noche", map.Map.TileSetName);
        Assert.True(map.IsModified);

        // Y lo sigue encontrando, que es lo que la identidad garantiza.
        Assert.Same(tiles, main.TileSetOf(map.Map));
    }

    [AvaloniaFact]
    public void Renombrar_un_juego_pone_al_dia_su_panel_de_bloques()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        Rename(main, tiles, "Cueva");

        TileBlocksViewModel blocks = main.TreeGeneralVm.PrimaryNodes
            .SelectMany(node => node.Childs)
            .SelectMany(node => node.Childs)
            .SelectMany(node => node.PanelsList)
            .OfType<TileBlocksViewModel>()
            .Single();

        Assert.Equal("Bloques de Cueva", blocks.Header);
    }

    /// <summary>El documento y su fichero son cosas distintas.</summary>
    [AvaloniaFact]
    public void Renombrar_no_toca_el_fichero()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        tiles.MarkSaved(@"C:\juego\bosque.json");

        Rename(main, tiles, "Cueva");

        Assert.Equal(@"C:\juego\bosque.json", tiles.FilePath);
    }

    // ------------------------------------------------------------------ el panel

    [AvaloniaFact]
    public void El_panel_avisa_de_a_cuantos_mapas_afecta()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        EditPropertiesViewModel form = OpenProperties(main, tiles);

        Assert.False(form.HasAffected);

        main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);
        main.OpenMap(new TileMap("Nivel 2", 8, 8), tiles);

        form = OpenProperties(main, tiles);

        Assert.Contains("2 mapas", form.Affected);
    }

    [AvaloniaFact]
    public void Un_nombre_vacio_no_vale_y_el_panel_se_queda()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        EditPropertiesViewModel form = OpenProperties(main, tiles);
        form.Name = "   ";
        form.AcceptPropertiesCommand.Execute(null);

        Assert.True(form.HasError);
        Assert.Equal("Bosque", tiles.TileSet.Name);
        Assert.Same(form, main.RightPanViewModel);
    }

    [AvaloniaFact]
    public void Cancelar_no_cambia_nada()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        tiles.MarkClean();

        EditPropertiesViewModel form = OpenProperties(main, tiles);
        form.Name = "Cueva";
        form.CancelPropertiesCommand.Execute(null);

        Assert.Equal("Bosque", tiles.TileSet.Name);
        Assert.False(tiles.IsModified);
        Assert.Null(main.RightPanViewModel);
    }

    /// <summary>Dos paneles de propiedades de cosas distintas sólo confundirían.</summary>
    [AvaloniaFact]
    public void Pedir_las_propiedades_de_otro_cambia_el_panel_en_vez_de_apilarlo()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel one = main.OpenTileSet(new TileSet("Bosque"));
        TileSetEditorViewModel other = main.OpenTileSet(new TileSet("Cueva"));

        OpenProperties(main, one);
        EditPropertiesViewModel second = OpenProperties(main, other);

        Assert.Single(main.RightPanels.OfType<EditPropertiesViewModel>());
        Assert.Same(other, second.Document);
    }

    // ------------------------------------------------------------------ el árbol

    [AvaloniaFact]
    public void F2_sobre_el_nodo_abre_sus_propiedades()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        var view = new TreeGeneralView { DataContext = main.TreeGeneralVm };
        var window = new Window { Content = view, Width = 300, Height = 500 };

        window.Show();
        Pump();

        TreeView tree = view.FindControl<TreeView>("ProjectTree")!;

        tree.SelectedItem = NodeOf(main, tiles);
        Pump();

        tree.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = tree,
            Key = Key.F2,
        });

        Pump();

        var form = Assert.IsType<EditPropertiesViewModel>(main.RightPanViewModel);

        Assert.Same(tiles, form.Document);

        window.Close();
        Pump();
    }

    /// <summary>Con el nombre ya marcado, para poder escribir directamente.</summary>
    [AvaloniaFact]
    public void El_panel_abre_con_el_nombre_seleccionado()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        var view = new EditPropertiesView { DataContext = OpenProperties(main, tiles) };
        var window = new Window { Content = view, Width = 400, Height = 400 };

        window.Show();
        Pump();

        TextBox box = view.FindControl<TextBox>("NameBox")!;

        Assert.Equal("Bosque", box.Text);
        Assert.Equal(0, box.SelectionStart);
        Assert.Equal("Bosque".Length, box.SelectionEnd);

        window.Close();
        Pump();
    }

    // ------------------------------------------------------------------ utilidades

    private static EditPropertiesViewModel OpenProperties(MainWindowViewModel main, PanelBaseViewModel document)
    {
        main.ShowPropertiesCommand.Execute(NodeOf(main, document));

        return (EditPropertiesViewModel)main.RightPanViewModel!;
    }

    /// <summary>Renombra por donde lo hace el usuario: el panel de propiedades.</summary>
    private static void Rename(MainWindowViewModel main, PanelBaseViewModel document, string name)
    {
        EditPropertiesViewModel form = OpenProperties(main, document);

        form.Name = name;
        form.AcceptPropertiesCommand.Execute(null);
    }

    private static ItemTree NodeOf(MainWindowViewModel main, PanelBaseViewModel panel) =>
        main.TreeGeneralVm.PrimaryNodes
            .SelectMany(node => node.Childs)
            .Single(node => node.Tag == panel.TagId);

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
