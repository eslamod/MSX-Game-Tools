using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El panel del mapa: herramientas, capas y lo que se estampa.</summary>
public class MapEditorTests
{
    [AvaloniaFact]
    public void Arranca_en_la_capa_de_arriba_y_con_algo_cogido()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.Equal(MapTool.Stamp, editor.Tool);
        Assert.Same(editor.Layers[^1], editor.ActiveLayer);
        Assert.True(editor.CanEdit);

        // Con algo cogido de partida, pulsar hace algo desde el principio.
        Assert.Equal(0, editor.Brush[0, 0]);
    }

    [AvaloniaFact]
    public void Estampar_deja_el_tile_en_la_capa_activa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.PickTile(TilePatch.Single(12), "Tile 12");
        editor.Paint(3, 2);

        Assert.Equal(12, editor.ActiveLayer!.Layer.Grid[3, 2]);
        Assert.True(editor.UndoCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void En_una_capa_bloqueada_no_se_pinta()
    {
        MapEditorViewModel editor = NewEditor();

        editor.ActiveLayer!.IsLocked = true;
        editor.Paint(1, 1);

        Assert.Null(editor.ActiveLayer.Layer.Grid[1, 1]);
        Assert.False(editor.CanEdit);
    }

    // ------------------------------------------------------------------ seleccion

    /// <summary>
    /// El rectángulo sale igual arrastrando en cualquier sentido, y se queda dentro del
    /// mapa aunque el ratón se salga.
    /// </summary>
    [AvaloniaFact]
    public void La_seleccion_se_normaliza_y_no_se_sale_del_mapa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.Select(5, 4, 2, 1);

        Assert.Equal(new MapRegion(2, 1, 4, 4), editor.Selection);

        editor.Select(0, 0, 999, 999);

        Assert.Equal(new MapRegion(0, 0, editor.Map.Width, editor.Map.Height), editor.Selection);
    }

    [AvaloniaFact]
    public void Sin_seleccion_no_se_puede_rellenar_ni_copiar()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.False(editor.FillSelectionCommand.CanExecute(null));
        Assert.False(editor.CopySelectionCommand.CanExecute(null));
        Assert.False(editor.EraseSelectionCommand.CanExecute(null));

        editor.Select(0, 0, 1, 1);

        Assert.True(editor.FillSelectionCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Rellenar_la_seleccion_embaldosa_lo_cogido()
    {
        MapEditorViewModel editor = NewEditor();

        editor.PickTile(TilePatch.Single(7), "Tile 7");
        editor.Select(1, 1, 2, 2);
        editor.FillSelectionCommand.Execute(null);

        TileGrid grid = editor.ActiveLayer!.Layer.Grid;

        Assert.Equal(7, grid[1, 1]);
        Assert.Equal(7, grid[2, 2]);
        Assert.Null(grid[0, 0]);
    }

    /// <summary>
    /// Copiar coge de la capa activa y no de lo que se ve: pegar tiene que devolver lo
    /// mismo que se cogió, y lo que se ve puede venir de otra capa.
    /// </summary>
    [AvaloniaFact]
    public void Copiar_coge_de_la_capa_activa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.AddLayerCommand.Execute(null);

        // Debajo hay otra cosa, que no debe colarse en la copia.
        editor.Map.Stamp(0, 0, 0, TilePatch.Single(99));

        editor.PickTile(TilePatch.Single(5), "Tile 5");
        editor.Paint(1, 0);

        editor.Select(0, 0, 1, 0);
        editor.CopySelectionCommand.Execute(null);

        Assert.Equal((2, 1), (editor.Brush.Width, editor.Brush.Height));
        Assert.Null(editor.Brush[0, 0]);
        Assert.Equal(5, editor.Brush[1, 0]);
        Assert.Contains("2x1", editor.BrushName);
    }

    // ------------------------------------------------------------------ lo cogido

    /// <summary>
    /// El rectángulo se coge sobre las 32 columnas del selector, que son las del editor y
    /// las del png: los tiles 1 y 34 son las esquinas de un cuadrado de 2x2.
    /// </summary>
    [AvaloniaFact]
    public void Coger_un_rectangulo_de_tiles_lo_marca_entero()
    {
        MapEditorViewModel editor = NewEditor();

        editor.PickTiles(1, 34);

        Assert.Equal((2, 2), (editor.Brush.Width, editor.Brush.Height));
        Assert.Equal(34, editor.Brush[1, 1]);

        Assert.True(editor.TileChoices[1].IsSelected);
        Assert.True(editor.TileChoices[33].IsSelected);
        Assert.False(editor.TileChoices[3].IsSelected);
    }

    [AvaloniaFact]
    public void Coger_otro_tile_borra_la_marca_anterior()
    {
        MapEditorViewModel editor = NewEditor();

        editor.PickTiles(1, 34);
        editor.PickTiles(7, 7);

        Assert.True(editor.TileChoices[7].IsSelected);
        Assert.False(editor.TileChoices[1].IsSelected);
        Assert.Equal("Tile 7", editor.BrushName);
    }

    /// <summary>
    /// Sólo se estampa una cosa a la vez, así que dos recuadros rojos a la vez serían
    /// mentira: coger un bloque apaga la marca de los tiles y al revés.
    /// </summary>
    [AvaloniaFact]
    public void Coger_un_bloque_apaga_la_marca_de_los_tiles()
    {
        MapEditorViewModel editor = NewEditor(out TileSet tileSet);

        tileSet.Blocks.Add(new TileBlock("Arbol") { [0, 0] = 1 });
        editor.RefreshBlocks();

        editor.PickTiles(5, 5);
        editor.PickBlock(tileSet.Blocks[0]);

        Assert.True(editor.BlockChoices[0].IsSelected);
        Assert.False(editor.TileChoices[5].IsSelected);

        editor.PickTiles(5, 5);

        Assert.False(editor.BlockChoices[0].IsSelected);
    }

    // ------------------------------------------------------------------ atributos

    /// <summary>
    /// El fondo se puede cambiar, no sólo mirar: es el mismo R#7 del borde del editor de
    /// tiles y aquí también hace falta tocarlo.
    /// </summary>
    [AvaloniaFact]
    public void Elegir_el_color_de_fondo_lo_cambia_y_repinta()
    {
        MapEditorViewModel editor = NewEditor();

        int repaints = 0;
        editor.RefreshRequested += () => repaints++;

        editor.PickBackgroundColorCommand.Execute(editor.BackgroundChoices[7]);

        Assert.Equal(editor.BackgroundChoices[7].Index, editor.Map.BackgroundColorIndex);
        Assert.Same(editor.BackgroundChoices[7], editor.BackgroundColor);
        Assert.Equal(1, repaints);
    }

    /// <summary>El 0 no vale de fondo: es el transparente.</summary>
    [AvaloniaFact]
    public void El_fondo_no_se_elige_entre_el_color_0()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.DoesNotContain(editor.BackgroundChoices, color => color.Index == 0);
    }

    /// <summary>
    /// El tile de relleno enseña su dibujo: un número suelto no dice qué va a salir donde
    /// el mapa está vacío.
    /// </summary>
    [AvaloniaFact]
    public void El_tile_de_relleno_ensena_su_miniatura()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.Same(editor.Tiles[0], editor.EmptyTileImage);

        editor.EmptyTile = 42;

        Assert.Equal(42, editor.Map.EmptyTile);
        Assert.Same(editor.Tiles[42], editor.EmptyTileImage);
    }

    [AvaloniaFact]
    public void El_tile_de_relleno_no_se_sale_del_juego()
    {
        MapEditorViewModel editor = NewEditor();

        editor.EmptyTile = 9999;

        Assert.Equal(255, editor.Map.EmptyTile);
        Assert.NotNull(editor.EmptyTileImage);
    }

    // ------------------------------------------------------------------ deshacer

    [AvaloniaFact]
    public void Deshacer_y_rehacer_se_encienden_solos()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.False(editor.UndoCommand.CanExecute(null));

        editor.Paint(0, 0);

        Assert.True(editor.UndoCommand.CanExecute(null));
        Assert.False(editor.RedoCommand.CanExecute(null));

        editor.UndoCommand.Execute(null);

        Assert.False(editor.UndoCommand.CanExecute(null));
        Assert.True(editor.RedoCommand.CanExecute(null));
        Assert.Null(editor.ActiveLayer!.Layer.Grid[0, 0]);
    }

    // ------------------------------------------------------------------ capas

    [AvaloniaFact]
    public void Agregar_una_capa_la_pone_encima_y_activa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.AddLayerCommand.Execute(null);

        Assert.Equal(2, editor.Layers.Count);
        Assert.Same(editor.Layers[^1], editor.ActiveLayer);
        Assert.Equal(1, editor.ActiveLayerIndex);
        Assert.True(editor.ActiveLayer!.IsActive);
        Assert.False(editor.Layers[0].IsActive);
    }

    /// <summary>Un mapa sin capas no se puede editar, así que la última no se borra.</summary>
    [AvaloniaFact]
    public void La_ultima_capa_no_se_puede_eliminar()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.False(editor.DeleteLayerCommand.CanExecute(null));

        editor.AddLayerCommand.Execute(null);

        Assert.True(editor.DeleteLayerCommand.CanExecute(null));

        editor.DeleteLayerCommand.Execute(null);

        Assert.Single(editor.Layers);
        Assert.Single(editor.Map.Layers);
        Assert.NotNull(editor.ActiveLayer);
    }

    /// <summary>Apagar una capa cambia lo que se ve, así que el lienzo tiene que repintarse.</summary>
    [AvaloniaFact]
    public void Apagar_una_capa_pide_repintar()
    {
        MapEditorViewModel editor = NewEditor();

        int repaints = 0;
        editor.RefreshRequested += () => repaints++;

        editor.Layers[0].IsVisible = false;

        Assert.Equal(1, repaints);
    }

    // ------------------------------------------------------------------ zoom y pincel

    [AvaloniaFact]
    public void El_zoom_se_queda_entre_los_topes()
    {
        MapEditorViewModel editor = NewEditor();

        for (int i = 0; i < 20; i++)
            editor.ZoomInCommand.Execute(null);

        Assert.Equal(MapEditorViewModel.MaxZoom, editor.Zoom);

        for (int i = 0; i < 20; i++)
            editor.ZoomOutCommand.Execute(null);

        Assert.Equal(MapEditorViewModel.MinZoom, editor.Zoom);
    }

    /// <summary>El ajuste coge el paso más grande con el que el mapa entero cabe.</summary>
    [AvaloniaFact]
    public void Ajustar_el_zoom_mete_el_mapa_entero()
    {
        MapEditorViewModel editor = NewEditor();   // 8 x 6 tiles = 64 x 48 pixeles

        editor.FitZoom(200, 200);

        Assert.Equal(3, editor.Zoom);

        editor.FitZoom(64, 48);

        Assert.Equal(1, editor.Zoom);
    }

    /// <summary>
    /// Un mapa grande no cabía con el mínimo en x1: 96x96 tiles son 768 pixeles y no
    /// entran en la pantalla, así que «Ajustar» no ajustaba nada.
    /// </summary>
    [AvaloniaFact]
    public void Un_mapa_grande_cabe_gracias_a_los_pasos_de_menos_de_uno()
    {
        var editor = new MapEditorViewModel(
            new TileMap("Grande", 96, 96),
            new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard()));

        // 96 tiles son 768 pixeles a x1; en 700 de alto solo cabe a la mitad.
        editor.FitZoom(1500, 700);

        Assert.Equal(0.5, editor.Zoom);
        Assert.Equal("x½", editor.ZoomLabel);

        // Y en un hueco diminuto, al cuarto.
        editor.FitZoom(300, 300);

        Assert.Equal(0.25, editor.Zoom);
        Assert.Equal("x¼", editor.ZoomLabel);
    }

    /// <summary>Los pasos van en mitades por debajo de uno y de uno en uno por encima.</summary>
    [AvaloniaFact]
    public void El_zoom_recorre_sus_pasos()
    {
        MapEditorViewModel editor = NewEditor();

        editor.Zoom = 1;

        editor.ZoomOutCommand.Execute(null);
        Assert.Equal(0.5, editor.Zoom);

        editor.ZoomOutCommand.Execute(null);
        Assert.Equal(0.25, editor.Zoom);

        editor.ZoomInCommand.Execute(null);
        Assert.Equal(0.5, editor.Zoom);

        editor.Zoom = 4;
        editor.ZoomInCommand.Execute(null);
        Assert.Equal(5, editor.Zoom);
    }

    [AvaloniaFact]
    public void Coger_un_bloque_lo_deja_entero_como_pincel()
    {
        MapEditorViewModel editor = NewEditor(out TileSet tileSet);

        var block = new TileBlock("Arbol") { [0, 0] = 1, [1, 1] = 2 };
        tileSet.Blocks.Add(block);

        editor.PickBlock(block);

        Assert.Equal((2, 2), (editor.Brush.Width, editor.Brush.Height));
        Assert.Equal(2, editor.Brush[1, 1]);
        Assert.Contains("Arbol", editor.BrushName);
    }

    /// <summary>La rueda con shift pasa al bloque siguiente sin salirse de la lista.</summary>
    [AvaloniaFact]
    public void Pasar_de_bloque_se_para_en_los_extremos()
    {
        MapEditorViewModel editor = NewEditor(out TileSet tileSet);

        tileSet.Blocks.Add(new TileBlock("Uno") { [0, 0] = 1 });
        tileSet.Blocks.Add(new TileBlock("Dos") { [0, 0] = 2 });

        editor.PickBlock(tileSet.Blocks[0]);

        editor.StepBlock(1);
        Assert.Contains("Dos", editor.BrushName);

        editor.StepBlock(1);
        Assert.Contains("Dos", editor.BrushName);

        editor.StepBlock(-1);
        Assert.Contains("Uno", editor.BrushName);
    }

    // ------------------------------------------------------------------ los tercios

    /// <summary>
    /// Un mapa de una pantalla se abre con un juego de tiles por tercio.
    /// </summary>
    /// <remarks>
    /// Los paneles los pone quien abre el mapa, que es el único que sabe cuáles están abiertos.
    /// </remarks>
    [AvaloniaFact]
    public void El_mapa_se_abre_con_un_juego_de_tiles_por_tercio()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));
        TileSetEditorViewModel suelo = main.OpenTileSet(new TileSet("Suelo"));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Ref(cielo), Ref(ciudad), Ref(suelo)]);

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        Assert.Equal(3, editor.CellImagesByThird.Count);

        Assert.Same(cielo.Thumbnails, editor.CellImagesByThird[0]);
        Assert.Same(ciudad.Thumbnails, editor.CellImagesByThird[1]);
        Assert.Same(suelo.Thumbnails, editor.CellImagesByThird[2]);
    }

    /// <summary>Y uno de los de siempre se pinta entero con el suyo.</summary>
    [AvaloniaFact]
    public void Un_mapa_de_un_solo_juego_se_pinta_entero_con_el()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        Assert.Single(editor.CellImagesByThird);
        Assert.Same(bosque.Thumbnails, editor.CellImagesByThird[0]);
    }

    /// <summary>
    /// El tercio cuyo juego no está abierto se dibuja con el primero.
    /// </summary>
    /// <remarks>
    /// Negarse a abrir el mapa sería peor que dibujarlo: el mapa está ahí, y el juego del primer
    /// tercio también.
    /// </remarks>
    [AvaloniaFact]
    public void El_tercio_sin_juego_abierto_se_dibuja_con_el_primero()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));

        var map = new TileMap("Nivel", 32, 24);

        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        map.UseTileSets([Ref(cielo), Ref(ciudad), new TileSetRef(Guid.NewGuid(), "El que no está")]);

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        Assert.Equal(3, editor.CellImagesByThird.Count);

        Assert.Same(ciudad.Thumbnails, editor.CellImagesByThird[1]);
        Assert.Same(cielo.Thumbnails, editor.CellImagesByThird[2]);
    }

    /// <summary>
    /// La tira de abajo enseña los tiles de la banda que se elija.
    /// </summary>
    /// <remarks>
    /// El número es el mismo en las tres bandas; lo que cambia es el dibujo que la máquina lee
    /// para él. Así que esto no cambia lo que se estampa, cambia lo que se está mirando al
    /// cogerlo, que hasta ahora era siempre el juego del primer tercio.
    /// </remarks>
    [AvaloniaFact]
    public void La_tira_ensena_los_tiles_de_la_banda_elegida()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Ref(cielo), Ref(ciudad), Ref(cielo)]);

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        Assert.Same(cielo.Thumbnails[7], editor.TileChoices[7].Image);

        editor.PickingBand = 1;

        Assert.Same(ciudad.Thumbnails[7], editor.TileChoices[7].Image);
    }

    /// <summary>La banda no se pregunta cuando sólo hay una.</summary>
    [AvaloniaFact]
    public void Solo_se_pregunta_la_banda_cuando_hay_mas_de_una()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        MapEditorViewModel sencillo = main.OpenMap(new TileMap("Llano", 32, 24), cielo);

        Assert.False(sencillo.ShowsBands);

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Ref(cielo), Ref(ciudad), Ref(cielo)]);

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        Assert.True(editor.ShowsBands);
        Assert.True(editor.HasBottomBand);
    }

    /// <summary>
    /// Y la etiqueta del ratón dice de qué juego es el tile que hay debajo.
    /// </summary>
    /// <remarks>
    /// Con más de una banda el número solo no dice qué dibujo es: el mismo 77 es otro tile ocho
    /// filas más abajo.
    /// </remarks>
    [AvaloniaFact]
    public void La_etiqueta_dice_de_que_juego_es_el_tile()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Ref(cielo), Ref(ciudad), Ref(cielo)]);
        map.Stamp(0, 0, 8, TilePatch.Single(77));

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        editor.Hover = (0, 8);

        Assert.Contains("77", editor.HoverLabel);
        Assert.Contains("Ciudad", editor.HoverLabel);
    }

    /// <summary>Y con un solo juego no dice nada de más, que no hay nada que aclarar.</summary>
    [AvaloniaFact]
    public void Con_un_solo_juego_la_etiqueta_no_nombra_el_juego()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));

        var map = new TileMap("Nivel", 32, 24);

        map.Stamp(0, 0, 8, TilePatch.Single(77));

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        editor.Hover = (0, 8);

        Assert.Contains("77", editor.HoverLabel);
        Assert.DoesNotContain("Cielo", editor.HoverLabel);
    }

    /// <summary>Al quedarse sin bandas, la tira vuelve a la primera.</summary>
    [AvaloniaFact]
    public void Al_quedarse_sin_bandas_la_tira_vuelve_a_la_primera()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel cielo = main.OpenTileSet(new TileSet("Cielo"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Ref(cielo), Ref(ciudad), Ref(ciudad)]);

        MapEditorViewModel editor = main.OpenMap(map, cielo);

        editor.PickingBand = 2;

        editor.UseTileSets([cielo]);

        Assert.Equal(0, editor.PickingBand);
        Assert.Same(cielo.Thumbnails[7], editor.TileChoices[7].Image);
    }

    private static TileSetRef Ref(TileSetEditorViewModel panel) =>
        new(panel.TileSet.Id, panel.TileSet.Name);

    private static MapEditorViewModel NewEditor() => NewEditor(out _);

    private static MapEditorViewModel NewEditor(out TileSet tileSet)
    {
        tileSet = new TileSet("Bosque");

        return new MapEditorViewModel(
            new TileMap("Mapa 1", 8, 6),
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));
    }
}
