using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Qué hace falta para desplazar un mapa un pixel a la izquierda sin redibujarlo.
/// </summary>
/// <remarks>
/// <para>
/// Lo que se decide por tile es qué entra por su borde derecho: la columna izquierda del tile
/// siguiente del juego, ceros o unos. Y quien lo dicta es el mapa: debería entrar la columna
/// izquierda del tile que tenga a su derecha.
/// </para>
/// <para>
/// De ahí que esto sea un informe y no una comprobación. Un tile colocado junto a vecinos
/// distintos no tiene respuesta buena para todos, y lo que interesa no es que falle sino saber
/// cuántos sitios cuesta la opción mayoritaria. Sale de mirar una tabla hecha a mano en un juego
/// de verdad: en todos los tiles conflictivos su autor había elegido la mayoría.
/// </para>
/// </remarks>
public class MapShiftAnalysisTests
{
    /// <summary>Un juego donde cada tile se pinta con la máscara que se le diga, ocho veces.</summary>
    private static TileSet Tiles(params (int Tile, byte Mask)[] drawn)
    {
        var tileSet = new TileSet("Bosque");

        foreach ((int tile, byte mask) in drawn)
            Draw(tileSet, tile, [mask, mask, mask, mask, mask, mask, mask, mask]);

        return tileSet;
    }

    /// <summary>
    /// Pinta un tile fila a fila.
    /// </summary>
    /// <remarks>
    /// Hace falta para los casos que de verdad distinguen. Con las ocho filas iguales, la
    /// columna izquierda de un tile sólo puede salir toda a cero o toda a uno, así que nunca se
    /// llega a un vecino que no sea ni liso ni macizo —que es justo donde el relleno del tile
    /// siguiente es la única salida, y donde puede no haber ninguna—.
    /// </remarks>
    private static void Draw(TileSet tileSet, int tile, byte[] rows)
    {
        for (int row = 0; row < Tile.Rows; row++)
        {
            for (int column = 0; column < TileRow.Columns; column++)
            {
                tileSet.ListOfTiles[tile].ArrayTileRows[row].ArrayPattern[column] =
                    (rows[row] & (1 << (TileRow.Columns - 1 - column))) != 0;
            }
        }
    }

    /// <summary>Un tile cuya columna izquierda no es ni lisa ni maciza: la mitad de arriba sí.</summary>
    private static readonly byte[] MediaColumna = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00];

    /// <summary>Un mapa de una fila con los tiles que se le den, en orden.</summary>
    private static TileMap Map(params int[] row)
    {
        var map = new TileMap("Nivel", row.Length, 1);
        map.AddLayer();

        for (int column = 0; column < row.Length; column++)
            map.Layers[0].Grid[column, 0] = row[column];

        return map;
    }

    private static ShiftTileReport Report(MapShiftReport report, int tile) =>
        report.Tiles.Single(one => one.Tile == tile);

    // ------------------------------------------------------------------ los tres rellenos

    /// <summary>Con hueco a la derecha entran ceros.</summary>
    [Fact]
    public void Con_hueco_a_la_derecha_entran_ceros()
    {
        // 5 es macizo y 9 está vacío, así que a la derecha del 5 no hay nada.
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 9), Tiles((5, 0xFF)));

        Assert.Equal(ShiftFill.Zeros, Report(report, 5).Suggested);
        Assert.True(Report(report, 5).Clean);
    }

    /// <summary>Y con macizo, unos.</summary>
    [Fact]
    public void Con_macizo_a_la_derecha_entran_unos()
    {
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 9), Tiles((5, 0xFF), (9, 0xFF)));

        Assert.Equal(ShiftFill.Ones, Report(report, 5).Suggested);
    }

    /// <summary>
    /// Y si a la derecha va justo el tile siguiente del juego, se conserva el dibujo.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que un objeto de dos tiles de ancho se desplace entero en vez de partirse
    /// por la mitad: el borde de uno son los pixeles del otro, no un relleno inventado.
    /// </remarks>
    [Fact]
    public void Con_el_siguiente_a_la_derecha_se_conserva_el_dibujo()
    {
        TileSet tiles = Tiles((5, 0xFF));

        // El 6 empieza macizo por arriba y liso por abajo: ni una cosa ni la otra, así que
        // el único relleno que reproduce su borde es el suyo propio.
        Draw(tiles, 6, MediaColumna);

        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 6), tiles);

        Assert.Equal(ShiftFill.FromNextTile, Report(report, 5).Suggested);
        Assert.True(Report(report, 5).Clean);
    }

    /// <summary>
    /// Cuando vale un relleno liso, se prefiere al del tile siguiente, pero se dice que empatan.
    /// </summary>
    /// <remarks>
    /// El del siguiente ata dos tiles del juego para siempre —retocar el de al lado le cambia
    /// el borde a éste— y cuando los unos hacen exactamente lo mismo, ese amarre no compra
    /// nada. Que el vecino sea el 6 no basta para que haga falta apuntar al 6.
    /// <para>
    /// Aun así el empate se cuenta, porque el desempate es una opinión y no una obligación: una
    /// tabla que ya exista y traiga el otro no está mal, y sin esto parecería que sí.
    /// </para>
    /// </remarks>
    [Fact]
    public void Cuando_vale_un_relleno_liso_no_se_ata_al_siguiente()
    {
        ShiftTileReport five = Report(MapShiftAnalysis.Of(Map(5, 6), Tiles((5, 0xFF), (6, 0xFF))), 5);

        Assert.Equal(ShiftFill.Ones, five.Suggested);
        Assert.Equal(ShiftFill.FromNextTile, Assert.Single(five.AlsoWork));
    }

    /// <summary>
    /// Cuando los rellenos no coinciden, no hay empate que contar.
    /// </summary>
    /// <remarks>
    /// Cada relleno reproduce un único valor de columna, así que dos de ellos o valen en los
    /// mismos sitios o no comparten ninguno. Aquí el del siguiente y los unos piden columnas
    /// distintas: gana el del siguiente por mayoría y el otro sitio se queda roto, sin
    /// alternativa que ofrecer.
    /// </remarks>
    [Fact]
    public void Cuando_los_rellenos_no_coinciden_no_hay_empate()
    {
        TileSet tiles = Tiles((5, 0xFF), (7, 0xFF));

        // El 6 es el siguiente del 5, y empieza ni liso ni macizo.
        Draw(tiles, 6, MediaColumna);

        // El 5 sale dos veces junto al 6 y una junto al 7, que es macizo.
        ShiftTileReport five = Report(MapShiftAnalysis.Of(Map(5, 6, 5, 6, 5, 7), tiles), 5);

        Assert.Equal(3, five.Places);
        Assert.Equal(2, five.Demands.Single(demand => demand.Fill == ShiftFill.FromNextTile).Times);
        Assert.Equal(1, five.Demands.Single(demand => demand.Fill == ShiftFill.Ones).Times);

        Assert.Equal(ShiftFill.FromNextTile, five.Suggested);
        Assert.Empty(five.AlsoWork);
        Assert.Equal(new MapCell(4, 0), Assert.Single(five.Broken));
    }

    // ------------------------------------------------------------------ el conflicto

    /// <summary>
    /// Un tile con vecinos distintos no tiene una respuesta buena, y se dice lo que cuesta.
    /// </summary>
    /// <remarks>
    /// Es la limitación de la técnica, no un fallo del mapa. Lo que hace falta saber es cuál es
    /// la opción mayoritaria y en cuántos sitios se va a notar la otra.
    /// </remarks>
    [Fact]
    public void Un_tile_con_vecinos_distintos_dice_lo_que_cuesta()
    {
        // El 5 sale tres veces con macizo a la derecha y una con hueco.
        TileSet tiles = Tiles((5, 0xFF), (7, 0xFF), (9, 0x00));
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 7, 5, 7, 5, 7, 5, 9), tiles);

        ShiftTileReport five = Report(report, 5);

        Assert.False(five.Clean);
        Assert.Equal(ShiftFill.Ones, five.Suggested);
        Assert.Equal(3, five.Demands.Single(demand => demand.Fill == ShiftFill.Ones).Times);
        Assert.Equal(1, five.Demands.Single(demand => demand.Fill == ShiftFill.Zeros).Times);

        // Y dónde está el sitio que se va a ver mal, para poder ir a mirarlo.
        Assert.Equal(new MapCell(6, 0), Assert.Single(five.Broken));
    }

    /// <summary>Cuando el vecino no empieza ni liso ni macizo ni como el siguiente, no hay relleno.</summary>
    /// <remarks>
    /// Se cuenta aparte de los conflictos: ahí no es que haya que elegir, es que ninguna de las
    /// tres opciones sirve y hay que mover el tile o retocar el dibujo.
    /// </remarks>
    [Fact]
    public void Sin_relleno_posible_se_cuenta_aparte()
    {
        TileSet tiles = Tiles((5, 0xFF), (7, 0xFF));

        // A la derecha del 5 va el 8, que no empieza ni liso ni macizo. Y no es el 6, así que
        // el relleno del tile siguiente tampoco reproduce su borde: no queda ninguno.
        Draw(tiles, 8, MediaColumna);

        // Y el otro sitio donde sale, junto al 7, sí lo resuelven los unos.
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 8, 5, 7), tiles);

        ShiftTileReport five = Report(report, 5);

        Assert.Equal(new MapCell(0, 0), Assert.Single(five.Impossible));
        Assert.False(five.Clean);

        // Aparte quiere decir aparte: esa celda no engorda además la cuenta de las rotas, que
        // son las que se arreglan moviendo el tile. Ésta no se arregla moviéndolo.
        Assert.Empty(five.Broken);
        Assert.Equal(2, five.Places);
        Assert.Equal(1, report.Broken);
    }

    // ------------------------------------------------------------------ el informe

    /// <summary>La última columna no ata: allí no hay vecino a la derecha.</summary>
    [Fact]
    public void La_ultima_columna_no_ata()
    {
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 9), Tiles((5, 0xFF)));

        Assert.DoesNotContain(report.Tiles, tile => tile.Tile == 9);
    }

    /// <summary>
    /// Los tiles que se digan quedan fuera del informe.
    /// </summary>
    /// <remarks>
    /// El fondo toca con todo y se contradice siempre. Si ya se sabe que su borde da igual,
    /// sacarlo deja ver los que sí importan en vez de enterrarlos bajo su cuenta.
    /// </remarks>
    [Fact]
    public void Los_tiles_que_se_ignoran_no_salen()
    {
        TileSet tiles = Tiles((5, 0xFF), (7, 0xFF), (9, 0x00));
        int[] row = [5, 7, 5, 9];

        Assert.Contains(MapShiftAnalysis.Of(Map(row), tiles).Tiles, tile => tile.Tile == 5);

        MapShiftReport without = MapShiftAnalysis.Of(Map(row), tiles, new ShiftScope { Ignored = [5] });

        Assert.DoesNotContain(without.Tiles, tile => tile.Tile == 5);
    }

    /// <summary>
    /// Las filas que no se desplazan no atan nada.
    /// </summary>
    /// <remarks>
    /// Nace de un caso real: un tile salía con una sola posición rota de diez, y la posición
    /// estaba en la fila de arriba, que en ese juego es el marcador y no se mueve. El informe
    /// decía la verdad y aun así estaba equivocado, porque ahí no se va a ver nada.
    /// </remarks>
    [Fact]
    public void Las_filas_que_no_se_desplazan_no_atan()
    {
        TileSet tiles = Tiles((5, 0xFF), (7, 0xFF), (9, 0x00));

        var map = new TileMap("Nivel", 2, 2);
        map.AddLayer();

        // Arriba el 5 toca macizo y abajo toca hueco: se contradice.
        map.Layers[0].Grid[0, 0] = 5;
        map.Layers[0].Grid[1, 0] = 7;
        map.Layers[0].Grid[0, 1] = 5;
        map.Layers[0].Grid[1, 1] = 9;

        Assert.False(MapShiftAnalysis.Of(map, tiles).Tiles.Single(tile => tile.Tile == 5).Clean);

        // Y dejando fuera la fila del marcador, deja de contradecirse.
        MapShiftReport scrolled = MapShiftAnalysis.Of(map, tiles, new ShiftScope { FirstRow = 1 });

        ShiftTileReport five = scrolled.Tiles.Single(tile => tile.Tile == 5);

        Assert.True(five.Clean);
        Assert.Equal(ShiftFill.Zeros, five.Suggested);
        Assert.Equal(0, scrolled.Broken);
    }

    /// <summary>
    /// Y los tiles que no se desplazan tampoco, ni salen en la tabla.
    /// </summary>
    /// <remarks>
    /// Las ocho copias cuestan VRAM: lo normal es hacerlas de una parte del juego de tiles y
    /// dejar los primeros para el marcador. La tabla que come la rutina es la de ese trozo, con
    /// su primer byte en el primer tile que sí se desplaza.
    /// </remarks>
    [Fact]
    public void Los_tiles_fuera_del_rango_ni_atan_ni_salen_en_la_tabla()
    {
        TileSet tiles = Tiles((5, 0xFF), (13, 0xFF), (20, 0xFF));

        // Del 10 al 15: seis bytes. Y el que se mira es el 13, que cae en el cuarto.
        // Los números están elegidos para que restar el primero y el resto entre seis den
        // sitios distintos: con un rango que empiece donde su tamaño, los dos coinciden y
        // una tabla desplazada pasa por buena.
        var scope = new ShiftScope { FirstTile = 10, LastTile = 15 };
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 13, 20), tiles, scope);

        Assert.DoesNotContain(report.Tiles, tile => tile.Tile == 5);
        Assert.Contains(report.Tiles, tile => tile.Tile == 13);

        Assert.Equal(6, report.Table.Count);
        Assert.Equal(ShiftFill.Ones, report.Table[13 - 10]);
    }

    /// <summary>Los sucios salen primero los que más sitios estropean.</summary>
    [Fact]
    public void Los_sucios_salen_por_lo_que_cuestan()
    {
        TileSet tiles = Tiles((5, 0xFF), (6, 0xFF), (7, 0xFF), (9, 0x00));

        // El 5 sale tres veces junto a macizo y dos junto a hueco, así que falla en dos sitios.
        // El 6, dos y una: falla en uno.
        MapShiftReport report = MapShiftAnalysis.Of(
            Map(5, 7, 5, 7, 5, 7, 5, 9, 5, 9, 6, 7, 6, 7, 6, 9), tiles);

        Assert.Equal(2, Report(report, 5).Broken.Count);
        Assert.Single(Report(report, 6).Broken);

        int[] orden = [.. report.Dirty.Select(tile => tile.Tile)];

        Assert.Equal([5, 6], orden);
    }

    /// <summary>Y el total dice de cuántas celdas hablamos y cuántas no se van a ver bien.</summary>
    [Fact]
    public void El_total_cuenta_celdas_y_no_tiles()
    {
        TileSet tiles = Tiles((5, 0xFF), (7, 0xFF), (9, 0x00));
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 7, 5, 7, 5, 9), tiles);

        // Cinco celdas con vecino: las cinco primeras de las seis.
        Assert.Equal(5, report.Places);
        Assert.Equal(1, report.Broken);
    }

    /// <summary>La tabla que se exporta lleva un relleno por cada uno de los 256.</summary>
    [Fact]
    public void La_tabla_cubre_los_doscientos_cincuenta_y_seis()
    {
        MapShiftReport report = MapShiftAnalysis.Of(Map(5, 9), Tiles((5, 0xFF), (9, 0xFF)));

        Assert.Equal(TileSet.TileCount, report.Table.Count);
        Assert.Equal(ShiftFill.Ones, report.Table[5]);

        // Los que el mapa no usa se quedan con el del siguiente, que es el cero de la tabla:
        // no se decide nada por un tile del que el mapa no dice nada.
        Assert.Equal(ShiftFill.FromNextTile, report.Table[200]);
    }
}
