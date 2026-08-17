using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El análisis contra un juego de verdad: Lunar Rescue.
/// </summary>
/// <remarks>
/// <para>
/// Su autor mantenía la tabla a mano y llevaba en la cabeza dónde podía poner cada tile. Esa
/// tabla es la única verdad de campo que hay para esto, así que aquí se reproduce a pequeña
/// escala lo que se midió con el mapa entero: de los 55 tiles que el mapa restringía, 39
/// coincidían con lo elegido a mano, y en todos los conflictivos había elegido la mayoría.
/// </para>
/// <para>
/// No se leen sus ficheros: eso ataría las pruebas a una ruta de otra máquina. Se reconstruye el
/// caso que importa —un fondo macizo que toca con hueco y con macizo— que es el que se llevaba
/// 112 de las 134 posiciones rotas del nivel.
/// </para>
/// </remarks>
public class MapShiftLunarRescueTests
{
    private const int Fondo = 77;      // macizo, el más usado del nivel
    private const int Hueco = 76;      // vacío
    private const int Macizo = 78;     // macizo también

    private static TileSet Tiles()
    {
        var tileSet = new TileSet("Lunar");

        foreach (int tile in (int[])[Fondo, Macizo])
        {
            foreach (TileRow row in tileSet.ListOfTiles[tile].ArrayTileRows)
            {
                for (int column = 0; column < TileRow.Columns; column++)
                    row.ArrayPattern[column] = true;
            }
        }

        return tileSet;
    }

    /// <summary>Un mapa de una fila, como una franja de terreno.</summary>
    private static TileMap Map(params int[] row)
    {
        var map = new TileMap("Nivel", row.Length, 1);
        map.AddLayer();

        for (int column = 0; column < row.Length; column++)
            map.Layers[0].Grid[column, 0] = row[column];

        return map;
    }

    /// <summary>
    /// El tile de fondo se contradice, y la mayoría es la que su autor eligió.
    /// </summary>
    /// <remarks>
    /// El terreno macizo toca con más terreno casi siempre y con hueco al terminar. La tabla del
    /// juego le daba «entran unos», que es lo que pide la mayoría; los sitios donde toca con
    /// hueco son los que quedan sin desplazarse, y son pocos.
    /// </remarks>
    [Fact]
    public void El_tile_de_fondo_se_contradice_y_gana_la_mayoria()
    {
        // Cinco veces junto a macizo y una junto a hueco, como el terreno del nivel.
        MapShiftReport report = MapShiftAnalysis.Of(
            Map(Fondo, Macizo, Fondo, Macizo, Fondo, Macizo, Fondo, Macizo, Fondo, Macizo, Fondo, Hueco),
            Tiles());

        ShiftTileReport fondo = report.Tiles.Single(tile => tile.Tile == Fondo);

        Assert.False(fondo.Clean);
        Assert.Equal(ShiftFill.Ones, fondo.Suggested);

        Assert.Equal(5, fondo.Demands.Single(demand => demand.Fill == ShiftFill.Ones).Times);
        Assert.Equal(1, fondo.Demands.Single(demand => demand.Fill == ShiftFill.Zeros).Times);

        // Y el sitio que se queda sin desplazar es el borde del terreno, no el terreno.
        Assert.Equal(new MapCell(10, 0), Assert.Single(fondo.Broken));
    }

    /// <summary>
    /// Sacando el fondo del informe, lo que queda son los tiles que sí se pueden arreglar.
    /// </summary>
    /// <remarks>
    /// Es para lo que existe la lista de ignorados. En el nivel medido, el fondo se llevaba 112
    /// de las 134 posiciones rotas: dejándolo dentro, los otros trece tiles conflictivos quedan
    /// enterrados debajo de su cuenta.
    /// </remarks>
    [Fact]
    public void Ignorando_el_fondo_se_ven_los_demas()
    {
        TileMap map = Map(Fondo, Macizo, Fondo, Hueco, Macizo, Hueco);

        MapShiftReport todos = MapShiftAnalysis.Of(map, Tiles());
        MapShiftReport sinFondo = MapShiftAnalysis.Of(map, Tiles(), [Fondo]);

        Assert.Contains(todos.Dirty, tile => tile.Tile == Fondo);
        Assert.DoesNotContain(sinFondo.Tiles, tile => tile.Tile == Fondo);

        // Y las cuentas del total bajan con él, que es lo que hace legible el informe.
        Assert.True(sinFondo.Broken < todos.Broken);
    }
}
