using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Juegos de tiles que van por supertiles.
/// </summary>
/// <remarks>
/// El tamaño del supertile es del juego de tiles y no del mapa. Un supertile es un bloque
/// con el tamaño clavado, y los bloques cuelgan del juego porque son números de tile; por
/// mapa, dos mapas del mismo juego podrían pedir tamaños distintos y la regla «los bloques
/// de este juego miden 2x2» no se podría cumplir para los dos.
/// </remarks>
public class SuperTileTests
{
    // ------------------------------------------------------------------ el modelo

    [AvaloniaFact]
    public void Un_juego_normal_no_tiene_supertiles()
    {
        var tileSet = new TileSet("Bosque");

        Assert.False(tileSet.HasSuperTiles);
        Assert.Equal(0, tileSet.SuperTileWidth);
    }

    [AvaloniaFact]
    public void El_lado_del_supertile_tiene_tope()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 40, SuperTileHeight = 3 };

        Assert.Equal(TileSet.MaxSuperTileSide, tileSet.SuperTileWidth);
        Assert.Equal(3, tileSet.SuperTileHeight);
        Assert.True(tileSet.HasSuperTiles);
    }

    /// <summary>Con un lado a cero no va de supertiles, aunque el otro diga algo.</summary>
    [AvaloniaFact]
    public void Con_un_lado_sin_poner_no_va_de_supertiles()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2 };

        Assert.False(tileSet.HasSuperTiles);
    }

    // ------------------------------------------------------------------ el fichero

    [AvaloniaFact]
    public void El_tamaño_del_supertile_va_y_vuelve_del_fichero()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 4, SuperTileHeight = 2 };

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());
        TileSet read = TileSetSerializer.Deserialize(json).TileSet;

        Assert.True(read.HasSuperTiles);
        Assert.Equal(4, read.SuperTileWidth);
        Assert.Equal(2, read.SuperTileHeight);
    }

    /// <summary>Un fichero de antes no trae el tamaño y se abre como lo que era.</summary>
    [AvaloniaFact]
    public void Un_juego_de_antes_se_abre_sin_supertiles()
    {
        string json = TileSetSerializer.Serialize(
            new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        Assert.False(TileSetSerializer.Deserialize(json).TileSet.HasSuperTiles);
    }

    // ------------------------------------------------------------------ crear el juego

    [AvaloniaFact]
    public void El_formulario_crea_el_juego_con_su_tamaño_de_supertile()
    {
        var main = new MainWindowViewModel();

        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;
        form.Name = "Bosque";
        form.UseSuperTiles = true;
        form.SuperTileWidth = 3;
        form.SuperTileHeight = 2;
        form.AcceptTileSetCommand.Execute(null);

        TileSet created = main.Tabs.OfType<TileSetEditorViewModel>().Last().TileSet;

        Assert.True(created.HasSuperTiles);
        Assert.Equal(3, created.SuperTileWidth);
        Assert.Equal(2, created.SuperTileHeight);
    }

    [AvaloniaFact]
    public void Sin_marcarlo_el_juego_sale_normal()
    {
        var main = new MainWindowViewModel();

        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;
        form.Name = "Bosque";
        form.SuperTileWidth = 3;
        form.SuperTileHeight = 2;
        form.AcceptTileSetCommand.Execute(null);

        // Los numeros estan puestos, pero la casilla no: no va de supertiles.
        Assert.False(main.Tabs.OfType<TileSetEditorViewModel>().Last().TileSet.HasSuperTiles);
    }

    // ------------------------------------------------------------------ los bloques

    /// <summary>
    /// En un juego de supertiles, un bloque nuevo nace con el tamaño que toca.
    /// </summary>
    /// <remarks>
    /// Ahí un bloque no es un adorno para pintar más rápido: es una celda del mapa. Uno de
    /// otro tamaño no se podría colocar en ninguna parte.
    /// </remarks>
    [AvaloniaFact]
    public void Un_bloque_nuevo_nace_del_tamaño_del_supertile()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 3, SuperTileHeight = 2 };
        var blocks = new TileBlocksViewModel(
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        blocks.AddBlockCommand.Execute(null);

        TileBlock block = Assert.Single(tileSet.Blocks);

        Assert.Equal(3, block.Width);
        Assert.Equal(2, block.Height);
    }

    [AvaloniaFact]
    public void En_un_juego_normal_el_bloque_nuevo_sigue_naciendo_de_una_celda()
    {
        var tileSet = new TileSet("Bosque");
        var blocks = new TileBlocksViewModel(
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        blocks.AddBlockCommand.Execute(null);

        Assert.Equal(1, tileSet.Blocks[0].Width);
    }

    /// <summary>
    /// Un bloque de otro tamaño se enseña con el del juego, no con el suyo.
    /// </summary>
    /// <remarks>
    /// Puede haberlos: de antes de que el juego fuera de supertiles, o de un fichero
    /// anterior. El exportador lee por el tamaño del juego, así que si la imagen se hiciera
    /// por el del bloque, el editor enseñaría una cosa y la máquina vería otra.
    /// </remarks>
    [AvaloniaFact]
    public void Un_bloque_descuadrado_se_enseña_con_el_tamaño_del_juego()
    {
        var tileSet = new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 2 };
        var block = new TileBlock("Viejo") { Width = 4, Height = 5, [0, 0] = 1 };

        tileSet.Blocks.Add(block);

        ImageMini image = SuperTileRenderer.Render(
            block, tileSet, ColorPalette.CreateMsxStandard(), Avalonia.Media.Colors.Black);

        Assert.Equal(2 * TileRow.Columns, image.Width);
        Assert.Equal(2 * Tile.Rows, image.Height);
    }

    /// <summary>Y no se puede ajustar al contenido, que cambiaría el tamaño.</summary>
    [AvaloniaFact]
    public void Con_supertiles_no_se_deja_ajustar_el_bloque()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2, SuperTileHeight = 2 };
        var blocks = new TileBlocksViewModel(
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        blocks.AddBlockCommand.Execute(null);

        Assert.True(blocks.HasSuperTiles);
        Assert.False(blocks.CanResizeBlock);
        Assert.False(blocks.FitToContentCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void En_un_juego_normal_se_sigue_pudiendo_ajustar()
    {
        var tileSet = new TileSet("Bosque");
        var blocks = new TileBlocksViewModel(
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        blocks.AddBlockCommand.Execute(null);

        Assert.True(blocks.CanResizeBlock);
        Assert.True(blocks.FitToContentCommand.CanExecute(null));
    }

    // ------------------------------------------------------------------ crear el mapa

    // ------------------------------------------------------------------ el editor del mapa

    /// <summary>
    /// En un mapa de supertiles la celda mide el supertile y se pinta con sus imágenes.
    /// </summary>
    /// <remarks>
    /// El lienzo dibuja una imagen por celda en los dos casos; lo que cambia es cuánto mide
    /// la celda y de qué lista salen las imágenes. Así no hay dos formas de pintar el mapa.
    /// </remarks>
    [AvaloniaFact]
    public void La_celda_del_mapa_mide_el_supertile()
    {
        MapEditorViewModel normal = NewMap(new TileSet("Normal"));
        MapEditorViewModel super = NewMap(new TileSet("Grande") { SuperTileWidth = 3, SuperTileHeight = 2 });

        Assert.False(normal.UsesSuperTiles);
        Assert.Equal(1, normal.CellTilesWidth);
        Assert.Equal(1, normal.CellTilesHeight);
        Assert.Same(normal.Tiles, normal.CellImagesByThird[0]);

        Assert.True(super.UsesSuperTiles);
        Assert.Equal(3, super.CellTilesWidth);
        Assert.Equal(2, super.CellTilesHeight);
        Assert.Single(super.CellImagesByThird);
        Assert.Same(super.SuperTiles, super.CellImagesByThird[0]);
    }

    /// <summary>Cada supertile trae su imagen, del tamaño que le toca.</summary>
    [AvaloniaFact]
    public void Cada_supertile_tiene_su_imagen_compuesta()
    {
        var tileSet = new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 3 };
        tileSet.Blocks.Add(new TileBlock("Arbol") { [0, 0] = 1, [1, 2] = 2 });

        MapEditorViewModel map = NewMap(tileSet);

        ImageMini image = Assert.Single(map.SuperTiles);

        Assert.Equal(2 * TileRow.Columns, image.Width);
        Assert.Equal(3 * Tile.Rows, image.Height);
    }

    /// <summary>
    /// Coger un supertile deja una celda con su número, no sus tiles sueltos.
    /// </summary>
    /// <remarks>
    /// La celda del mapa es el supertile entero. Guardando sus tiles se desharía en pedazos
    /// que el mapa no sabe colocar, y al exportar saldrían números de tile donde el juego
    /// espera números de supertile.
    /// </remarks>
    [AvaloniaFact]
    public void Coger_un_supertile_estampa_su_numero()
    {
        var tileSet = new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 2 };
        tileSet.Blocks.Add(new TileBlock("Uno") { [0, 0] = 10 });
        tileSet.Blocks.Add(new TileBlock("Dos") { [0, 0] = 20 });

        MapEditorViewModel map = NewMap(tileSet);

        map.PickBlock(tileSet.Blocks[1]);

        Assert.Equal(1, map.Brush!.Width);
        Assert.Equal(1, map.Brush.Height);
        Assert.Equal(1, map.Brush[0, 0]);

        map.Paint(3, 4);

        Assert.Equal(1, map.Map.Layers[0].Grid[3, 4]);
    }

    /// <summary>En un mapa normal se sigue estampando el bloque entero, tile a tile.</summary>
    [AvaloniaFact]
    public void En_un_mapa_normal_el_bloque_sigue_estampando_sus_tiles()
    {
        var tileSet = new TileSet("Normal");
        tileSet.Blocks.Add(new TileBlock("Arbol") { [0, 0] = 10, [1, 0] = 11 });

        MapEditorViewModel map = NewMap(tileSet);

        map.PickBlock(tileSet.Blocks[0]);
        map.Paint(0, 0);

        Assert.Equal(10, map.Map.Layers[0].Grid[0, 0]);
        Assert.Equal(11, map.Map.Layers[0].Grid[1, 0]);
    }

    /// <summary>Un supertile nuevo aparece con su imagen sin tener que reabrir nada.</summary>
    [AvaloniaFact]
    public void Un_supertile_nuevo_trae_su_imagen()
    {
        var tileSet = new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 2 };
        var tiles = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());
        var map = new MapEditorViewModel(new TileMap("Nivel", 8, 8), tiles);
        var blocks = new TileBlocksViewModel(tiles);

        Assert.Empty(map.SuperTiles);

        blocks.AddBlockCommand.Execute(null);

        Assert.Single(map.SuperTiles);
    }

    /// <summary>
    /// El formulario del mapa dice qué va a salir, según el juego elegido.
    /// </summary>
    /// <remarks>
    /// No se elige ahí: enterarse después de que el mapa es de supertiles, o no serlo
    /// cuando se esperaba, obliga a rehacerlo.
    /// </remarks>
    [AvaloniaFact]
    public void El_formulario_del_mapa_dice_de_que_va_segun_el_juego()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Normal"));
        main.OpenTileSet(new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 2 });

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        form.TileSet = form.Bands.Choices.Single(choice => choice.TileSet.Name == "Normal");

        Assert.False(form.UsesSuperTiles);
        Assert.Contains("tiles sueltos", form.KindLabel);

        form.TileSet = form.Bands.Choices.Single(choice => choice.TileSet.Name == "Grande");

        Assert.True(form.UsesSuperTiles);
        Assert.Contains("2x2", form.KindLabel);
    }

    // ------------------------------------------------------------------ marcarlo despues

    /// <summary>
    /// Un juego que ya existe se puede pasar a supertiles desde sus propiedades.
    /// </summary>
    /// <remarks>
    /// Es justo el caso: un juego con sus tiles ya dibujados es el que uno quiere pasar a
    /// supertiles, y volver a empezar de cero para eso no tiene sentido.
    /// </remarks>
    [AvaloniaFact]
    public void Un_juego_que_ya_existe_se_puede_pasar_a_supertiles()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        tiles.MarkClean();

        var form = new EditPropertiesViewModel(main, tiles)
        {
            UseSuperTiles = true,
            SuperTileWidth = 2,
            SuperTileHeight = 3,
        };

        form.AcceptPropertiesCommand.Execute(null);

        Assert.True(tiles.TileSet.HasSuperTiles);
        Assert.Equal(2, tiles.TileSet.SuperTileWidth);
        Assert.Equal(3, tiles.TileSet.SuperTileHeight);

        // Y el juego queda sin guardar, que su fichero ha cambiado.
        Assert.True(tiles.IsModified);
    }

    /// <summary>
    /// Los bloques que ya hubiera pasan a medir el supertile.
    /// </summary>
    /// <remarks>
    /// Dejar uno de 3x1 entre supertiles de 2x2 sería dejar algo que no se puede colocar en
    /// ninguna parte.
    /// </remarks>
    [AvaloniaFact]
    public void Al_pasar_a_supertiles_los_bloques_se_ajustan()
    {
        var tileSet = new TileSet("Bosque");
        tileSet.Blocks.Add(new TileBlock("Ancho") { [0, 0] = 1, [2, 0] = 3 });
        tileSet.Blocks.Add(new TileBlock("Alto") { [0, 0] = 1, [0, 3] = 4 });

        tileSet.UseSuperTiles(2, 2);

        Assert.All(tileSet.Blocks, block =>
        {
            Assert.Equal(2, block.Width);
            Assert.Equal(2, block.Height);
        });

        // Lo que cabia se queda: la esquina de arriba a la izquierda no se toca.
        Assert.Equal(1, tileSet.Blocks[0][0, 0]);
    }

    /// <summary>Y el aviso lo dice antes de aceptar, con lo que hay delante.</summary>
    [AvaloniaFact]
    public void El_panel_avisa_de_lo_que_va_a_pasar()
    {
        var main = new MainWindowViewModel();
        var tileSet = new TileSet("Bosque");

        tileSet.Blocks.Add(new TileBlock("Arbol"));
        tileSet.Blocks.Add(new TileBlock("Roca"));

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet);
        main.OpenMap(new TileMap("Nivel", 8, 8), tiles);

        var form = new EditPropertiesViewModel(main, tiles);

        // Sin tocar nada no hay nada que avisar.
        Assert.False(form.HasSuperTileWarning);

        form.UseSuperTiles = true;

        Assert.Contains("2 bloques", form.SuperTileWarning);
        Assert.Contains("supertile", form.SuperTileWarning);
    }

    /// <summary>Un mapa abierto se entera de que su juego ha cambiado.</summary>
    [AvaloniaFact]
    public void El_mapa_abierto_se_entera_del_cambio()
    {
        var main = new MainWindowViewModel();
        var tileSet = new TileSet("Bosque");

        tileSet.Blocks.Add(new TileBlock("Arbol") { [0, 0] = 1 });

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet);
        MapEditorViewModel map = main.OpenMap(new TileMap("Nivel", 8, 8), tiles);

        Assert.False(map.UsesSuperTiles);
        Assert.Empty(map.SuperTiles);

        var form = new EditPropertiesViewModel(main, tiles) { UseSuperTiles = true };
        form.AcceptPropertiesCommand.Execute(null);

        Assert.True(map.UsesSuperTiles);
        Assert.Equal(2, map.CellTilesWidth);
        Assert.Single(map.SuperTiles);
    }

    /// <summary>Renombrar sin tocar los supertiles no ajusta nada.</summary>
    [AvaloniaFact]
    public void Renombrar_no_toca_los_bloques()
    {
        var main = new MainWindowViewModel();
        var tileSet = new TileSet("Bosque");

        tileSet.Blocks.Add(new TileBlock("Ancho") { [2, 0] = 3 });

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet);

        var form = new EditPropertiesViewModel(main, tiles) { Name = "Otro" };
        form.AcceptPropertiesCommand.Execute(null);

        Assert.Equal("Otro", tileSet.Name);
        Assert.Equal(3, tileSet.Blocks[0].Width);
        Assert.False(tileSet.HasSuperTiles);
    }

    /// <summary>
    /// El selector de abajo nunca se queda en una pestaña escondida.
    /// </summary>
    /// <remarks>
    /// Ocultar la pestaña de tiles en un mapa de supertiles no movía la selección: la
    /// cabecera desaparecía pero su contenido seguía delante bajo la cabecera de Bloques,
    /// así que lo que se cogía eran tiles sueltos, que ahí no se pueden colocar en ninguna
    /// parte. La etiqueta de la brocha lo decía —«Tile 4»— y la tira enseñaba cinco cosas
    /// con tres bloques definidos.
    /// </remarks>
    [AvaloniaFact]
    public void El_selector_de_abajo_no_se_queda_en_la_pestaña_escondida()
    {
        var tileSet = new TileSet("Grande") { SuperTileWidth = 2, SuperTileHeight = 2 };
        tileSet.Blocks.Add(new TileBlock("Uno") { [0, 0] = 1 });

        var editor = new MapEditorViewModel(
            new TileMap("Nivel", 8, 8),
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        var view = new MapEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 1100, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var selected = (TabItem)tabs.SelectedItem!;

        Assert.True(selected.IsVisible, "El selector se ha quedado en una pestaña escondida.");

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Un mapa abierto sobre ese juego de tiles.</summary>
    private static MapEditorViewModel NewMap(TileSet tileSet) =>
        new(new TileMap("Nivel", 8, 8),
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));
}
