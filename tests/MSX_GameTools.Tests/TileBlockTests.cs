using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Un bloque de tiles: la rejilla que después se estampa en el mapa de una vez.
/// </summary>
public class TileBlockTests
{
    [AvaloniaFact]
    public void Un_bloque_nace_de_una_celda_y_vacio()
    {
        var block = new TileBlock("Arbol");

        Assert.Equal(1, block.Width);
        Assert.Equal(1, block.Height);
        Assert.True(block.IsEmpty);
        Assert.Null(block[0, 0]);
    }

    /// <summary>
    /// El tile 0 es un tile de verdad, asi que "vacio" no puede ser el 0: al estampar, la
    /// celda vacia deja lo que hubiera debajo y la del 0 pinta el primer tile del juego.
    /// </summary>
    [AvaloniaFact]
    public void El_tile_0_puesto_no_es_lo_mismo_que_una_celda_vacia()
    {
        var block = new TileBlock();

        block[0, 0] = 0;

        Assert.False(block.IsEmpty);
        Assert.Equal(0, block[0, 0]);

        block[0, 0] = null;

        Assert.True(block.IsEmpty);
    }

    [AvaloniaFact]
    public void Poner_un_tile_mas_alla_estira_el_bloque()
    {
        var block = new TileBlock();

        block[2, 1] = 40;

        Assert.Equal(3, block.Width);
        Assert.Equal(2, block.Height);
        Assert.Equal(40, block[2, 1]);
        Assert.Null(block[0, 0]);
    }

    /// <summary>
    /// Es justo el caso del supertile de 2x2 con una esquina vacia: si el tamaño se
    /// dedujera de hasta donde llegan los tiles, se guardaria como 2x1 y dejaria de serlo.
    /// </summary>
    [AvaloniaFact]
    public void Borrar_en_el_borde_no_encoge_el_bloque()
    {
        var block = new TileBlock();

        block[1, 1] = 7;
        block[0, 0] = 3;

        Assert.Equal((2, 2), (block.Width, block.Height));

        block[1, 1] = null;

        Assert.Equal((2, 2), (block.Width, block.Height));
        Assert.Equal((1, 1), block.UsedSize());
    }

    [AvaloniaFact]
    public void El_tamano_se_puede_fijar_y_lo_que_queda_fuera_se_olvida()
    {
        var block = new TileBlock();

        block[2, 0] = 9;
        block[0, 0] = 5;

        block.Width = 1;

        Assert.Null(block[2, 0]);
        Assert.Equal(5, block[0, 0]);

        // Y al volver a crecer la celda sale vacia, no con lo de antes.
        block.Width = 3;

        Assert.Null(block[2, 0]);
    }

    [AvaloniaFact]
    public void No_se_puede_pasar_del_maximo()
    {
        var block = new TileBlock();

        Assert.False(block.Set(TileBlock.MaxSide, 0, 12));
        Assert.False(block.Set(-1, 0, 12));

        block.Width = 100;

        Assert.Equal(TileBlock.MaxSide, block.Width);
    }

    /// <summary>Preguntar por una celda de fuera no es un error: alli no hay nada.</summary>
    [AvaloniaFact]
    public void Fuera_del_bloque_no_hay_tile()
    {
        var block = new TileBlock();

        Assert.Null(block[5, 5]);
        Assert.False(block.Contains(5, 5));
    }

    [AvaloniaFact]
    public void Estampar_un_trozo_lo_copia_desde_esa_esquina()
    {
        var block = new TileBlock();
        var patch = new TilePatch(2, 2);

        patch[0, 0] = 1;
        patch[1, 0] = 2;
        patch[1, 1] = 3;   // la celda 0,1 se queda vacia

        block.Stamp(1, 1, patch);

        Assert.Equal(1, block[1, 1]);
        Assert.Equal(2, block[2, 1]);
        Assert.Equal(3, block[2, 2]);
        Assert.Null(block[1, 2]);
        Assert.Equal((3, 3), (block.Width, block.Height));
    }

    /// <summary>
    /// Un tile suelto es un trozo de 1x1, asi que estampar no tiene dos caminos.
    /// </summary>
    [AvaloniaFact]
    public void Un_tile_suelto_es_un_trozo_de_una_celda()
    {
        TilePatch patch = TilePatch.Single(42);

        Assert.Equal((1, 1), (patch.Width, patch.Height));
        Assert.Equal(42, patch[0, 0]);
    }

    [AvaloniaFact]
    public void Un_bloque_se_puede_copiar_a_un_trozo_para_estamparlo()
    {
        var block = new TileBlock();

        block[0, 0] = 4;
        block[1, 1] = 6;

        TilePatch patch = block.ToPatch();

        Assert.Equal((2, 2), (patch.Width, patch.Height));
        Assert.Equal(4, patch[0, 0]);
        Assert.Null(patch[1, 0]);
        Assert.Equal(6, patch[1, 1]);
    }

    [AvaloniaFact]
    public void Los_bloques_cuelgan_del_juego_de_tiles()
    {
        var tileSet = new TileSet("Bosque");

        Assert.Empty(tileSet.Blocks);

        tileSet.Blocks.Add(new TileBlock("Arbol"));

        Assert.Equal("Arbol", tileSet.Blocks[0].Name);
    }
}
