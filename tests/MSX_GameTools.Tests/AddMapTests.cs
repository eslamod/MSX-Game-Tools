using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
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

        Assert.Equal(Localizer.Instance["NewMapTitle"], form.Header);
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

    // ------------------------------------------------------------------ las bandas

    /// <summary>
    /// Las otras dos bandas se ofrecen según lo alto que vaya a ser el mapa.
    /// </summary>
    /// <remarks>
    /// Uno más alto que la pantalla no se reparte: sus filas cambian de tercio al
    /// desplazarse, y no queda banda que atar a un banco.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(8, false, false)]
    [InlineData(16, true, false)]
    [InlineData(24, true, true)]
    [InlineData(30, false, false)]
    public void Las_bandas_se_ofrecen_segun_el_alto(int rows, bool middle, bool bottom)
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        form.Rows = rows;

        Assert.Equal(middle, form.Bands.ShowsMiddle);
        Assert.Equal(bottom, form.Bands.ShowsBottom);
    }

    /// <summary>Y en screen 1 no se ofrecen, que allí la tabla de patrones es una sola.</summary>
    [AvaloniaFact]
    public void En_screen_1_no_se_ofrecen_bandas()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Marcador", TileSet.GraphicMode.Graphic1));
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        Assert.False(form.Bands.ShowsMiddle);
        Assert.False(form.Bands.ShowsBottom);
    }

    /// <summary>El mapa creado lleva el juego de cada banda.</summary>
    [AvaloniaFact]
    public void El_mapa_creado_lleva_el_juego_de_cada_banda()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        form.Name = "Nivel";
        form.TileSet = cielo;
        form.Bands.Middle = ciudad;

        form.AcceptMapCommand.Execute(null);

        TileMap map = main.Tabs.OfType<MapEditorViewModel>().Single().Map;

        Assert.Equal(3, map.TileSets.Count);

        Assert.Equal("Cielo", map.TileSetFor(0).Name);
        Assert.Equal("Ciudad", map.TileSetFor(8).Name);

        // La de abajo no se tocó, así que se queda con el juego del mapa.
        Assert.Equal("Cielo", map.TileSetFor(16).Name);
    }

    /// <summary>
    /// No deja mezclar paletas entre bandas.
    /// </summary>
    /// <remarks>
    /// En la pantalla hay una sola paleta, así que tres juegos con tres paletas es algo que la
    /// máquina no puede pintar. Se dice al aceptar y con el nombre del que no encaja, que es lo
    /// único que hace falta para arreglarlo.
    /// </remarks>
    [AvaloniaFact]
    public void No_deja_mezclar_paletas_entre_bandas()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));

        main.OpenTileSet(new TileSet("Ciudad"), ColorPalette.CreateMsxStandard());

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        form.Name = "Nivel";
        form.TileSet = cielo;
        form.Bands.Middle = main.TileSets[1];

        form.AcceptMapCommand.Execute(null);

        Assert.True(form.HasError);
        Assert.Contains("Ciudad", form.ErrorMessage);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
    }

    /// <summary>Las bandas siguen al juego del mapa mientras no se toquen.</summary>
    [AvaloniaFact]
    public void Las_bandas_siguen_al_juego_del_mapa()
    {
        MainWindowViewModel main = WithTileSet("Bosque");

        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        Assert.Same(main.TileSets[0], form.Bands.Middle);

        form.TileSet = ciudad;

        Assert.Same(ciudad, form.Bands.Middle);
        Assert.Same(ciudad, form.Bands.Bottom);
    }

    /// <summary>
    /// El formulario se entera de lo que se elige dentro del selector de bandas.
    /// </summary>
    /// <remarks>
    /// De qué va a ir el mapa lo dice el formulario y sale del juego elegido, pero la elección
    /// vive en el selector, así que nadie se lo cuenta si él no escucha. Se mira el aviso y no
    /// el valor: el valor se calcula al leerlo y sale bien aunque nadie avise, que es
    /// exactamente lo que la pantalla no hace.
    /// </remarks>
    [AvaloniaFact]
    public void El_formulario_se_entera_del_juego_elegido_en_las_bandas()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Normal"));

        TileSetEditorViewModel grande = main.OpenTileSet(
            new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 2 });

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        var told = new List<string?>();

        form.PropertyChanged += (_, e) => told.Add(e.PropertyName);

        form.Bands.TileSet = grande;

        Assert.Contains(nameof(EditMapViewModel.UsesSuperTiles), told);
        Assert.Contains(nameof(EditMapViewModel.KindLabel), told);
    }

    private static MainWindowViewModel WithTileSet(string name)
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet(name));

        return main;
    }
}
