using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El panel de bloques y el selector del mapa, que enseñan lo mismo a la vez.
/// </summary>
/// <remarks>
/// Los bloques se editan en su panel y se estampan desde el mapa, y los dos pueden estar
/// abiertos al mismo tiempo. El selector del mapa se rehacía sólo al montarse, así que un
/// bloque creado con el mapa delante no aparecía hasta cambiar de pestaña y volver.
/// </remarks>
public class BlocksAndMapTests
{
    [AvaloniaFact]
    public void Crear_un_bloque_lo_pone_en_el_selector_del_mapa()
    {
        (MapEditorViewModel map, TileBlocksViewModel blocks) = NewPair();

        Assert.Empty(map.BlockChoices);

        blocks.AddBlockCommand.Execute(null);

        Assert.Single(map.BlockChoices);
    }

    [AvaloniaFact]
    public void Borrar_un_bloque_lo_quita_del_selector_del_mapa()
    {
        (MapEditorViewModel map, TileBlocksViewModel blocks) = NewPair();

        blocks.AddBlockCommand.Execute(null);
        blocks.AddBlockCommand.Execute(null);

        Assert.Equal(2, map.BlockChoices.Count);

        blocks.DeleteBlockCommand.Execute(null);

        Assert.Single(map.BlockChoices);
    }

    /// <summary>
    /// Y retocar uno cambia lo que enseña el selector, no sólo la lista.
    /// </summary>
    /// <remarks>
    /// Las casillas del selector se construyen con los tiles del bloque, así que estampar
    /// dentro del bloque tiene que rehacerlas: si no, el dibujo de la tira se queda con el
    /// de antes aunque el bloque ya sea otro.
    /// </remarks>
    [AvaloniaFact]
    public void Retocar_un_bloque_cambia_su_dibujo_en_el_selector()
    {
        (MapEditorViewModel map, TileBlocksViewModel blocks) = NewPair();

        blocks.AddBlockCommand.Execute(null);
        blocks.SelectTile(7);
        blocks.Paint(0, 0);

        BlockChoiceViewModel choice = Assert.Single(map.BlockChoices);

        Assert.Equal(7, choice.Block[0, 0]);
        Assert.Same(map.Tiles[7], choice.Cells[0]);
    }

    /// <summary>Un mapa y un panel de bloques sobre el mismo juego de tiles.</summary>
    private static (MapEditorViewModel Map, TileBlocksViewModel Blocks) NewPair()
    {
        var tiles = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        return (new MapEditorViewModel(new TileMap("Mapa", 8, 8), tiles), new TileBlocksViewModel(tiles));
    }
}
