using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La rejilla de números de tile: lo que hay debajo de un bloque y de una capa de mapa.
/// </summary>
public class TileGridTests
{
    [AvaloniaFact]
    public void Nace_del_tamano_que_se_le_pide_y_vacia()
    {
        var grid = new TileGrid(4, 3);

        Assert.Equal((4, 3), (grid.Width, grid.Height));
        Assert.True(grid.IsEmpty);
        Assert.Null(grid[0, 0]);
    }

    /// <summary>El tile 0 es un tile de verdad; vacío es otra cosa.</summary>
    [AvaloniaFact]
    public void El_tile_0_puesto_no_deja_la_rejilla_vacia()
    {
        var grid = new TileGrid(2, 2);

        grid[1, 1] = 0;

        Assert.False(grid.IsEmpty);
        Assert.Equal(0, grid[1, 1]);
    }

    /// <summary>
    /// Al contrario que un bloque, una rejilla no crece al pintar fuera: un mapa tiene el
    /// tamaño que tiene y pintar más allá del borde no lo agranda.
    /// </summary>
    [AvaloniaFact]
    public void Pintar_fuera_no_hace_crecer_la_rejilla()
    {
        var grid = new TileGrid(2, 2);

        grid[5, 5] = 9;

        Assert.Equal((2, 2), (grid.Width, grid.Height));
        Assert.Null(grid[5, 5]);
        Assert.True(grid.IsEmpty);
        Assert.False(grid.Contains(5, 5));
    }

    [AvaloniaFact]
    public void Al_crecer_se_conserva_lo_que_habia()
    {
        var grid = new TileGrid(2, 2);

        grid[0, 0] = 1;
        grid[1, 1] = 2;

        grid.Resize(4, 4);

        Assert.Equal(1, grid[0, 0]);
        Assert.Equal(2, grid[1, 1]);
        Assert.Null(grid[3, 3]);
    }

    [AvaloniaFact]
    public void Al_encoger_se_olvida_lo_que_queda_fuera()
    {
        var grid = new TileGrid(4, 4);

        grid[0, 0] = 1;
        grid[3, 3] = 2;

        grid.Resize(2, 2);

        Assert.Equal(1, grid[0, 0]);

        // Y al volver a crecer sale vacia, no con lo de antes.
        grid.Resize(4, 4);

        Assert.Null(grid[3, 3]);
    }

    /// <summary>
    /// Es el caso del mapa: al redimensionar, una fila entera tiene que seguir siendo la
    /// misma fila y no correrse, que es lo que pasaría copiando el array de seguido.
    /// </summary>
    [AvaloniaFact]
    public void Cambiar_el_ancho_no_desplaza_las_filas()
    {
        var grid = new TileGrid(3, 2);

        grid[0, 0] = 10;
        grid[0, 1] = 20;

        grid.Resize(5, 2);

        Assert.Equal(10, grid[0, 0]);
        Assert.Equal(20, grid[0, 1]);
    }

    [AvaloniaFact]
    public void Estampar_copia_el_trozo_desde_esa_esquina()
    {
        var grid = new TileGrid(4, 4);
        var patch = new TilePatch(2, 2);

        patch[0, 0] = 1;
        patch[1, 1] = 3;

        grid.Stamp(1, 1, patch);

        Assert.Equal(1, grid[1, 1]);
        Assert.Equal(3, grid[2, 2]);
        Assert.Null(grid[2, 1]);
    }

    /// <summary>Estampar en el borde recorta y no se sale.</summary>
    [AvaloniaFact]
    public void Estampar_en_el_borde_no_se_sale()
    {
        var grid = new TileGrid(2, 2);
        var patch = new TilePatch(2, 2);

        patch[0, 0] = 1;
        patch[1, 0] = 2;
        patch[0, 1] = 3;
        patch[1, 1] = 4;

        grid.Stamp(1, 1, patch);

        Assert.Equal(1, grid[1, 1]);
        Assert.Equal((2, 2), (grid.Width, grid.Height));
    }

    /// <summary>Copiar un rectángulo es lo que hará el mapa al copiar y pegar.</summary>
    [AvaloniaFact]
    public void Se_puede_copiar_un_rectangulo_suelto()
    {
        var grid = new TileGrid(4, 4);

        grid[1, 1] = 5;
        grid[2, 1] = 6;

        TilePatch patch = grid.ToPatch(1, 1, 2, 1);

        Assert.Equal((2, 1), (patch.Width, patch.Height));
        Assert.Equal(5, patch[0, 0]);
        Assert.Equal(6, patch[1, 0]);
    }

    [AvaloniaFact]
    public void Copiar_sin_rectangulo_coge_la_rejilla_entera()
    {
        var grid = new TileGrid(3, 2);
        grid[2, 1] = 7;

        TilePatch patch = grid.ToPatch();

        Assert.Equal((3, 2), (patch.Width, patch.Height));
        Assert.Equal(7, patch[2, 1]);
    }

    [AvaloniaFact]
    public void Lo_ocupado_puede_ser_menos_que_el_tamano()
    {
        var grid = new TileGrid(8, 8);

        grid[1, 2] = 4;

        Assert.Equal((2, 3), grid.UsedSize());

        grid.Clear();

        Assert.Equal((0, 0), grid.UsedSize());
        Assert.Equal((8, 8), (grid.Width, grid.Height));
    }
}
