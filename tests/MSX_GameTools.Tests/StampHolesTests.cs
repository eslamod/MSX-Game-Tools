using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Los huecos de la brocha dejan ver lo que hay debajo.
/// </summary>
/// <remarks>
/// Un bloque no tiene por qué llenar su rectángulo: el de una curva trae sus celdas y deja
/// vacías las esquinas que no le tocan. Esas vacías son huecos de la brocha y no una orden
/// de borrar, así que estampar la curva sobre la hierba tiene que dejar la hierba asomando.
/// Se escribían tal cual y borraban el mapa por donde el bloque no pintaba nada.
/// </remarks>
public class StampHolesTests
{
    /// <summary>Un bloque de 2x2 con una sola celda puesta, en diagonal.</summary>
    private static TilePatch Holed()
    {
        var block = new TileBlock("Curva") { [0, 0] = 7, [1, 1] = 9 };

        return block.ToPatch();
    }

    [AvaloniaFact]
    public void Estampar_no_borra_por_los_huecos()
    {
        var map = new TileMap("Nivel", 4, 4);
        TileGrid grid = map.Layers[0].Grid;

        // Hierba por todas partes.
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
                grid[column, row] = 1;
        }

        Assert.True(map.Stamp(0, 0, 0, Holed()));

        Assert.Equal(7, grid[0, 0]);
        Assert.Equal(9, grid[1, 1]);

        // Las dos celdas que el bloque no pinta siguen con lo que habia.
        Assert.Equal(1, grid[1, 0]);
        Assert.Equal(1, grid[0, 1]);
    }

    /// <summary>Y rellenar tampoco, que es la misma brocha con otra herramienta.</summary>
    [AvaloniaFact]
    public void Rellenar_no_borra_por_los_huecos()
    {
        var map = new TileMap("Nivel", 4, 4);
        TileGrid grid = map.Layers[0].Grid;

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
                grid[column, row] = 1;
        }

        Assert.True(map.Fill(0, 0, 0, 4, 4, Holed()));

        Assert.Equal(7, grid[0, 0]);
        Assert.Equal(1, grid[1, 0]);
        Assert.Equal(7, grid[2, 2]);
        Assert.Equal(1, grid[3, 2]);
    }

    /// <summary>
    /// Deshacer sí devuelve las celdas vacías.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad y va al revés: al restaurar, un hueco es parte de lo que había y
    /// hay que reponerlo. Con la brocha, deshacer dejaría puesto el tile que se estampó
    /// sobre una celda que estaba vacía.
    /// </remarks>
    [AvaloniaFact]
    public void Deshacer_vuelve_a_dejar_vacio_lo_que_estaba_vacio()
    {
        var map = new TileMap("Nivel", 4, 4);
        TileGrid grid = map.Layers[0].Grid;

        Assert.Null(grid[0, 0]);

        map.Stamp(0, 0, 0, TilePatch.Single(5));

        Assert.Equal(5, grid[0, 0]);

        map.Undo.Undo(map);

        Assert.Null(grid[0, 0]);
    }

    /// <summary>Y desde el editor, que es por donde entra de verdad.</summary>
    [AvaloniaFact]
    public void El_editor_estampa_el_bloque_sin_borrar_alrededor()
    {
        var tiles = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());
        var block = new TileBlock("Curva") { [0, 0] = 7, [1, 1] = 9 };

        tiles.TileSet.Blocks.Add(block);

        var editor = new MapEditorViewModel(new TileMap("Nivel", 4, 4), tiles);
        TileGrid grid = editor.Map.Layers[0].Grid;

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
                grid[column, row] = 1;
        }

        editor.PickBlock(block);
        editor.Paint(0, 0);

        Assert.Equal(7, grid[0, 0]);
        Assert.Equal(1, grid[1, 0]);
        Assert.Equal(1, grid[0, 1]);
        Assert.Equal(9, grid[1, 1]);
    }
}
