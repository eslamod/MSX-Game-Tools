using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
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

        form.TileSet = form.TileSets.Single(choice => choice.TileSet.Name == "Normal");

        Assert.False(form.UsesSuperTiles);
        Assert.Contains("tiles sueltos", form.KindLabel);

        form.TileSet = form.TileSets.Single(choice => choice.TileSet.Name == "Grande");

        Assert.True(form.UsesSuperTiles);
        Assert.Contains("2x2", form.KindLabel);
    }
}
