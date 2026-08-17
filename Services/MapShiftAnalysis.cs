using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>Qué entra por la derecha de un tile al desplazarlo un pixel a la izquierda.</summary>
public enum ShiftFill
{
    /// <summary>La columna izquierda del tile siguiente del juego, fila a fila.</summary>
    FromNextTile = 0,

    /// <summary>Ceros: a la derecha hay hueco.</summary>
    Zeros = 1,

    /// <summary>Unos: a la derecha hay macizo.</summary>
    Ones = 2,
}

/// <summary>Una celda del mapa, para poder ir a mirarla.</summary>
public sealed record MapCell(int Column, int Row);

/// <summary>En cuántas posiciones del mapa vale un relleno para un tile.</summary>
/// <remarks>
/// Una posición puede contar para más de uno: si el vecino empieza macizo y el tile siguiente
/// también, la cubren tanto los unos como el del siguiente. Por eso la suma de las veces puede
/// pasarse del número de posiciones.
/// </remarks>
public sealed record ShiftDemand(ShiftFill Fill, int Times);

/// <summary>
/// Lo que el mapa exige de un tile.
/// </summary>
/// <param name="Tile">Número de tile.</param>
/// <param name="Places">En cuántas celdas aparece con un vecino que ata.</param>
/// <param name="Demands">Los rellenos que valen, del que más sitios cubre al que menos.</param>
/// <param name="Suggested">El que menos posiciones estropea.</param>
/// <param name="AlsoWork">
/// Los que cubren exactamente las mismas posiciones que el sugerido. Están para que se vea que
/// ahí no hay nada que decidir: cualquiera de ellos deja la pantalla igual.
/// </param>
/// <param name="Broken">
/// Las posiciones donde el sugerido no es el que hace falta. Vacío si el tile sale limpio.
/// </param>
/// <param name="Impossible">
/// Posiciones donde no vale ninguno de los tres. Pasa cuando el vecino no empieza ni liso ni
/// macizo ni como el tile siguiente: ahí no hay relleno que valga, se elija el que se elija.
/// </param>
public sealed record ShiftTileReport(
    int Tile,
    int Places,
    IReadOnlyList<ShiftDemand> Demands,
    ShiftFill Suggested,
    IReadOnlyList<ShiftFill> AlsoWork,
    IReadOnlyList<MapCell> Broken,
    IReadOnlyList<MapCell> Impossible)
{
    /// <summary>Si el mapa lo coloca siempre en sitios que piden lo mismo.</summary>
    public bool Clean => Broken.Count == 0 && Impossible.Count == 0;
}

/// <summary>
/// Lo que costaría el desplazamiento suave en un mapa.
/// </summary>
/// <param name="Tiles">Sólo los que el mapa usa con algún vecino: los demás no atan nada.</param>
/// <param name="Places">Celdas del mapa que atan a algún tile.</param>
/// <param name="Broken">De ésas, las que no se van a ver como toca.</param>
public sealed record MapShiftReport(IReadOnlyList<ShiftTileReport> Tiles, int Places, int Broken)
{
    /// <summary>Los que hay que mirar: primero los que más sitios estropean.</summary>
    public IEnumerable<ShiftTileReport> Dirty =>
        Tiles.Where(tile => !tile.Clean).OrderByDescending(tile => tile.Broken.Count + tile.Impossible.Count);

    /// <summary>La tabla que se exporta: un relleno por tile, del 0 al 255.</summary>
    public IReadOnlyList<ShiftFill> Table
    {
        get
        {
            var table = new ShiftFill[TileSet.TileCount];

            foreach (ShiftTileReport tile in Tiles)
                table[tile.Tile] = tile.Suggested;

            return table;
        }
    }
}

/// <summary>
/// Qué hace falta para desplazar un mapa un pixel a la izquierda sin redibujarlo.
/// </summary>
/// <remarks>
/// <para>
/// La técnica: se guardan ocho copias del juego de tiles, cada una un pixel más a la izquierda,
/// y se va cambiando cuál mira el VDP. Al octavo paso se corre la tabla de nombres una columna y
/// se vuelve a empezar. Sale un scroll de un pixel sin tocar la pantalla.
/// </para>
/// <para>
/// Lo que hay que decidir es qué entra por el borde derecho de cada tile al desplazarlo, y hay
/// tres respuestas posibles: la columna izquierda del tile siguiente del juego, ceros o unos. La
/// gracia es que <b>la respuesta la dicta el mapa</b>: lo que debería entrar es la columna
/// izquierda del tile que tenga a su derecha.
/// </para>
/// <para>
/// De ahí sale la limitación de la técnica: un tile que aparece en sitios con vecinos distintos
/// no puede tener una respuesta buena para todos. Por eso esto no es un error sino un informe
/// con cuentas: casi siempre hay una opción mayoritaria, y lo que interesa saber es cuántos
/// sitios cuesta elegirla. En la práctica el tile de fondo, que toca con todo, es el que se
/// lleva casi todo el coste.
/// </para>
/// </remarks>
public static class MapShiftAnalysis
{
    /// <summary>
    /// Mira un mapa y dice qué relleno le toca a cada tile y qué cuesta.
    /// </summary>
    /// <param name="ignored">
    /// Tiles que no se quieren en el informe. Para el fondo y demás rellenos, que tocan con
    /// todo y se contradicen siempre: si ya se sabe que su borde no importa, sacarlos deja ver
    /// los que sí.
    /// </param>
    public static MapShiftReport Of(TileMap map, TileSet tileSet, IReadOnlyCollection<int>? ignored = null)
    {
        TileGrid grid = map.Flatten();

        // Por tile: dónde aparece, y qué posiciones cubre cada relleno. Una posición cuenta para
        // todos los rellenos que valen en ella y no sólo para el primero que se encuentre, que es
        // lo que permite decir cuándo dos opciones dejan la pantalla igual. No cambia las
        // cuentas: cada relleno reproduce un único valor de columna, así que dos de ellos o
        // cubren las mismas posiciones o no comparten ninguna.
        var places = new Dictionary<int, List<MapCell>>();
        var covered = new Dictionary<int, Dictionary<ShiftFill, List<MapCell>>>();
        var impossible = new Dictionary<int, List<MapCell>>();

        for (int row = 0; row < map.Height; row++)
        {
            // Hasta la penúltima: la última columna no tiene vecino a la derecha, así que no
            // ata nada. Un mapa que se repite en bucle sí lo tendría, pero eso es del juego.
            for (int column = 0; column < map.Width - 1; column++)
            {
                if (grid[column, row] is not int tile || grid[column + 1, row] is not int right)
                    continue;

                if (ignored?.Contains(tile) == true || !Inside(tile) || !Inside(right))
                    continue;

                int needed = LeftColumn(tileSet, right);
                var cell = new MapCell(column, row);

                Add(places, tile, cell);

                bool any = false;

                foreach (ShiftFill fill in Fills)
                {
                    if (!Reproduces(tileSet, tile, fill, needed))
                        continue;

                    any = true;

                    if (!covered.TryGetValue(tile, out Dictionary<ShiftFill, List<MapCell>>? byFill))
                        covered[tile] = byFill = [];

                    if (!byFill.TryGetValue(fill, out List<MapCell>? cells))
                        byFill[fill] = cells = [];

                    cells.Add(cell);
                }

                if (!any)
                    Add(impossible, tile, cell);
            }
        }

        List<ShiftTileReport> tiles = [.. Reports(places, covered, impossible).OrderBy(report => report.Tile)];

        return new MapShiftReport(
            tiles,
            tiles.Sum(report => report.Places),
            tiles.Sum(report => report.Broken.Count + report.Impossible.Count));
    }

    private static IEnumerable<ShiftTileReport> Reports(
        Dictionary<int, List<MapCell>> places,
        Dictionary<int, Dictionary<ShiftFill, List<MapCell>>> covered,
        Dictionary<int, List<MapCell>> impossible)
    {
        foreach ((int tile, List<MapCell> all) in places)
        {
            Dictionary<ShiftFill, List<MapCell>> byFill =
                covered.TryGetValue(tile, out Dictionary<ShiftFill, List<MapCell>>? found) ? found : [];

            List<ShiftDemand> counted =
            [
                .. byFill
                    .Select(pair => new ShiftDemand(pair.Key, pair.Value.Count))
                    .OrderByDescending(demand => demand.Times)
                    .ThenBy(demand => demand.Fill == ShiftFill.FromNextTile ? 1 : 0),
            ];

            // El que más sitios cubre, y en un empate el liso. El del tile siguiente ata dos
            // tiles del juego para siempre —retocar el de al lado cambia el borde de éste— y
            // cuando los ceros o los unos hacen lo mismo, ese amarre no compra nada.
            //
            // Con todo imposible no hay nada que elegir y se deja el del siguiente, que es el
            // cero de la tabla: así los tiles que no salen en el informe y éstos van igual.
            ShiftFill suggested = counted.Count > 0 ? counted[0].Fill : ShiftFill.FromNextTile;

            HashSet<MapCell> good = counted.Count > 0 ? [.. byFill[suggested]] : [];

            yield return new ShiftTileReport(
                tile,
                all.Count,
                counted,
                suggested,
                [.. byFill.Keys.Where(fill => fill != suggested && good.SetEquals(byFill[fill])).Order()],
                [.. all.Where(cell => !good.Contains(cell)).Except(Bad(impossible, tile))],
                Bad(impossible, tile));
        }
    }

    private static IReadOnlyList<MapCell> Bad(Dictionary<int, List<MapCell>> impossible, int tile) =>
        impossible.TryGetValue(tile, out List<MapCell>? cells) ? cells : [];

    /// <summary>Los tres rellenos, para recorrerlos.</summary>
    private static readonly ShiftFill[] Fills =
        [ShiftFill.FromNextTile, ShiftFill.Zeros, ShiftFill.Ones];

    /// <summary>
    /// Si <paramref name="fill"/> deja el borde derecho de <paramref name="tile"/> como pide el
    /// vecino.
    /// </summary>
    private static bool Reproduces(TileSet tileSet, int tile, ShiftFill fill, int needed) => fill switch
    {
        ShiftFill.Zeros => needed == 0x00,
        ShiftFill.Ones => needed == 0xFF,
        _ => tile + 1 < TileSet.TileCount && LeftColumn(tileSet, tile + 1) == needed,
    };

    /// <summary>
    /// La columna izquierda de un tile, con un bit por fila.
    /// </summary>
    /// <remarks>
    /// En la máscara de una línea el bit más alto es el pixel de más a la izquierda, que es el
    /// que va a entrar por la derecha del tile de al lado al desplazar.
    /// </remarks>
    private static int LeftColumn(TileSet tileSet, int tile)
    {
        int column = 0;

        for (int row = 0; row < Tile.Rows; row++)
        {
            if ((tileSet.ListOfTiles[tile].ArrayTileRows[row].PatternByte & 0x80) != 0)
                column |= 1 << row;
        }

        return column;
    }

    private static bool Inside(int tile) => (uint)tile < (uint)TileSet.TileCount;

    private static void Add(Dictionary<int, List<MapCell>> to, int tile, MapCell cell)
    {
        if (!to.TryGetValue(tile, out List<MapCell>? cells))
            to[tile] = cells = [];

        cells.Add(cell);
    }
}
