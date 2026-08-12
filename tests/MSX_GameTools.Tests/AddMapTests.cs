using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Crear un mapa desde el botón, con su pestaña y su nodo en el árbol.</summary>
public class AddMapTests
{
    [AvaloniaFact]
    public void El_formulario_pide_tamano_y_juego_de_tiles()
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        main.AddMapCommand.Execute(null);

        var form = Assert.IsType<EditMapViewModel>(main.RightPanViewModel);

        Assert.Equal("Agregar mapa", form.Header);
        Assert.Equal((32, 24), (form.Columns, form.Rows));
        Assert.Same(main.TileSets[0], form.TileSet);
        Assert.False(form.HasError);
    }

    /// <summary>
    /// Un mapa son números de tile: sin un juego con el que dibujarlo no hay nada que
    /// crear, y se dice al abrir el formulario en vez de al aceptar.
    /// </summary>
    [AvaloniaFact]
    public void Sin_juegos_de_tiles_el_formulario_lo_dice_de_entrada()
    {
        var main = new MainWindowViewModel();

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        Assert.True(form.HasError);
        Assert.Contains("juego de tiles", form.ErrorMessage);

        form.Name = "Bosque";
        form.AcceptMapCommand.Execute(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
    }

    [AvaloniaFact]
    public void Aceptar_abre_el_mapa_y_lo_cuelga_del_arbol()
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = "Nivel 1";
        form.Columns = 40;
        form.Rows = 20;
        form.AcceptMapCommand.Execute(null);

        MapEditorViewModel editor = Assert.Single(main.Tabs.OfType<MapEditorViewModel>());

        Assert.Equal("Nivel 1 (MP)", editor.Header);
        Assert.Equal((40, 20), (editor.Map.Width, editor.Map.Height));
        Assert.Same(editor, main.SelectedTab);

        // Y el formulario se cierra al aceptar, como los demás.
        Assert.Empty(main.RightPanels);

        ItemTree node = Assert.Single(main.TreeGeneralVm.PrimaryNodes[2].Childs);

        Assert.Equal("Nivel 1 (MP)", node.DisplayText);
        Assert.True(node.CanDelete);
    }

    /// <summary>El mapa se queda con el nombre del juego, que es lo que se guarda.</summary>
    [AvaloniaFact]
    public void El_mapa_recuerda_con_que_juego_se_dibuja()
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        MapEditorViewModel editor = Create(main, "Nivel 1");

        Assert.Equal("Bosque", editor.Map.TileSetName);
    }

    /// <summary>
    /// El fondo arranca en el color más oscuro de la paleta y no en un índice fijo: dar
    /// por negro el 1 ya costó un fallo con las paletas generadas de un png.
    /// </summary>
    [AvaloniaFact]
    public void El_fondo_arranca_en_el_color_mas_oscuro()
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        ColorPalette palette = main.Palettes.ActivePalette.Clone("Rara");
        palette[1].SetComponents(7, 0, 0);
        palette[9].SetComponents(0, 0, 0);

        main.Palettes.Palettes.Add(palette);
        main.Palettes.ActivePalette = palette;

        MapEditorViewModel editor = Create(main, "Nivel 1");

        Assert.Equal(9, editor.Map.BackgroundColorIndex);
    }

    [AvaloniaTheory]
    [InlineData("", 32, 24, "nombre")]
    [InlineData("Nivel 1", 0, 24, "tamaño")]
    [InlineData("Nivel 1", 32, 9999, "tamaño")]
    public void Un_mapa_imposible_no_se_crea(string name, int columns, int rows, string expected)
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = name;
        form.Columns = columns;
        form.Rows = rows;
        form.AcceptMapCommand.Execute(null);

        Assert.Contains(expected, form.ErrorMessage);
        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
    }

    [AvaloniaFact]
    public void Cerrar_la_pestana_y_volver_a_abrirla_recupera_el_mapa()
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        MapEditorViewModel editor = Create(main, "Nivel 1");
        editor.Paint(1, 1);

        main.CloseTabCommand.Execute(editor);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());

        main.OpenTreeItemCommand.Execute(main.TreeGeneralVm.PrimaryNodes[2].Childs[0]);

        var reopened = Assert.Single(main.Tabs.OfType<MapEditorViewModel>());

        Assert.Same(editor, reopened);
        Assert.Equal(0, reopened.ActiveLayer!.Layer.Grid[1, 1]);
    }

    private static MapEditorViewModel Create(MainWindowViewModel main, string name)
    {
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = name;
        form.AcceptMapCommand.Execute(null);

        return main.Tabs.OfType<MapEditorViewModel>().Last();
    }

    private static MainWindowViewModel WithTileSet(string name)
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet(name));

        return main;
    }
}
