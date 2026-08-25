using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Coser el mapa de un juego a partir de sus pantallas.
/// </summary>
/// <remarks>
/// Las pantallas se sacan recortando un mapa inventado, que es lo que hace un juego al
/// scrollear: si cosiendo los recortes vuelve a salir el mapa de partida, el algoritmo hace lo
/// que dice. Y el mapa lleva números distintos en cada celda a propósito, que uno lleno de
/// ceros encajaría en cualquier sitio y no probaría nada.
/// </remarks>
public class MapStitcherTests
{
    private const int Columns = 32;
    private const int Rows = 24;

    /// <summary>La cámara moviéndose a la derecha sale como un desplazamiento positivo.</summary>
    /// <remarks>
    /// El signo importa: si sale al revés, el mapa se cose hacia atrás y las pantallas se
    /// pisan unas a otras en vez de crecer.
    /// </remarks>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    [InlineData(3, 2)]      // en diagonal, que es lo que no sale mirando la direccion
    [InlineData(0, 0)]
    public void Se_encuentra_cuanto_se_ha_movido(int columns, int rows)
    {
        int[,] world = World(64, 48);

        int[,] before = Cut(world, 10, 10);
        int[,] after = Cut(world, 10 + columns, 10 + rows);

        MapStitcher.Shift shift = MapStitcher.Between(before, after)!;

        Assert.Equal((columns, rows), (shift.Columns, shift.Rows));
        Assert.Equal(1.0, shift.Match, 3);
    }

    /// <summary>Dos pantallas que no tienen nada que ver no encajan.</summary>
    /// <remarks>
    /// Es lo que pasa al cambiar de sala o al morir, y hay que poder distinguirlo de un scroll
    /// grande: pegarlas sería mezclar dos zonas que no se tocan.
    /// </remarks>
    [Fact]
    public void Dos_pantallas_distintas_no_encajan()
    {
        int[,] one = Cut(World(64, 48), 0, 0);
        int[,] other = Cut(World(64, 48, seed: 7777), 0, 0);

        Assert.Null(MapStitcher.Between(one, other));
    }

    /// <summary>
    /// Una pantalla de un solo tile se queda quieta en vez de irse a cualquier parte.
    /// </summary>
    /// <remarks>
    /// El cielo raso encaja igual de bien con todos los desplazamientos. Sin preferir el que
    /// menos mueve, el mapa se estiraría por donde le diera la gana cada vez que el juego
    /// enseña una zona lisa.
    /// </remarks>
    [Fact]
    public void Una_pantalla_lisa_se_queda_quieta()
    {
        var flat = new int[Columns, Rows];

        MapStitcher.Shift shift = MapStitcher.Between(flat, flat)!;

        Assert.Equal((0, 0), (shift.Columns, shift.Rows));
    }

    /// <summary>
    /// Un encaje que apenas solapa no se da por bueno, por bien que case lo poco que solapa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dos pantallas distintas de un juego de verdad tienen kilómetros de cielo raso. Corriendo
    /// una lo bastante sobre la otra, lo único que queda en común es cielo contra cielo: casa
    /// al cien por cien y manda la pantalla lejísimos. Es el fallo que deja el mapa con islas
    /// sueltas en vez de decir que ahí se cambió de sala.
    /// </para>
    /// <para>
    /// Con el alcance de partida esto no puede pasar —cuatro celdas dejan solapado el 87%—,
    /// así que hay que pedir un alcance grande para llegar a ejercerlo. Que es justamente el
    /// caso de quien capture cada pocos fotogramas en vez de en todos.
    /// </para>
    /// </remarks>
    [Fact]
    public void Un_encaje_que_apenas_solapa_no_cuenta()
    {
        // Cielo raso, y una franja distinta en cada una por el medio.
        int[,] one = Banded(1);
        int[,] other = Banded(2);

        // A veinte celdas sólo quedan en común los bordes vacíos, que casan perfectos.
        Assert.Null(MapStitcher.Between(one, other, reach: 20));
    }

    /// <summary>Cielo raso con una franja de marcas por el medio.</summary>
    private static int[,] Banded(int mark)
    {
        var screen = new int[Columns, Rows];

        for (int column = 12; column < 20; column++)
        {
            for (int row = 0; row < Rows; row++)
                screen[column, row] = mark;
        }

        return screen;
    }

    /// <summary>Cosiendo los recortes vuelve a salir el mapa de partida.</summary>
    [Fact]
    public void El_mapa_cosido_es_el_de_partida()
    {
        int[,] world = World(64, 24);
        var stitcher = new MapStitcher();

        // La camara cruzando el mundo de dos en dos celdas.
        for (int at = 0; at + Columns <= 64; at += 2)
            Assert.True(stitcher.Feed(Cut(world, at, 0)));

        Assert.Equal(64, stitcher.Width);
        Assert.Equal(Rows, stitcher.Height);

        int?[,] grid = stitcher.ToGrid();

        for (int column = 0; column < 64; column++)
        {
            for (int row = 0; row < Rows; row++)
                Assert.Equal(world[column, row], grid[column, row]);
        }
    }

    /// <summary>
    /// Lo que no se ha visto se queda sin poner, y no en el tile 0.
    /// </summary>
    /// <remarks>
    /// El tile 0 es un tile de verdad. Una zona en L deja esquinas por las que no se ha pasado,
    /// y decir que ahí hay un tile 0 sería inventárselo.
    /// </remarks>
    [Fact]
    public void Lo_que_no_se_ha_visto_se_queda_vacio()
    {
        int[,] world = World(64, 48);
        var stitcher = new MapStitcher();

        stitcher.Feed(Cut(world, 0, 0));
        stitcher.Feed(Cut(world, 0, 2));        // baja, sin ir a los lados

        int?[,] grid = stitcher.ToGrid();

        Assert.Equal(Columns, stitcher.Width);
        Assert.Equal(Rows + 2, stitcher.Height);

        // Todo lo visto tiene tile, y no hay nada mas: no se ha inventado ancho.
        Assert.All(
            Enumerable.Range(0, Columns).SelectMany(c => Enumerable.Range(0, Rows + 2).Select(r => grid[c, r])),
            tile => Assert.NotNull(tile));
    }

    /// <summary>Una pantalla que no encaja no se pega, y deja el mapa como estaba.</summary>
    [Fact]
    public void Una_pantalla_que_no_encaja_no_se_pega()
    {
        var stitcher = new MapStitcher();

        Assert.True(stitcher.Feed(Cut(World(64, 48), 0, 0)));
        Assert.False(stitcher.Feed(Cut(World(64, 48, seed: 999), 0, 0)));

        Assert.Equal(1, stitcher.Screens);
        Assert.Equal(Columns, stitcher.Width);
    }

    /// <summary>
    /// Una franja que no scrollea —el marcador— no impide encajar el resto.
    /// </summary>
    /// <remarks>
    /// Casi todos los juegos dejan una o dos filas quietas con la puntuación. Esas filas no
    /// coinciden con el desplazamiento del resto, así que bajan el porcentaje: si el listón
    /// estuviera en el 100%, no encajaría ni una pantalla de un juego de verdad.
    /// </remarks>
    [Fact]
    public void Un_marcador_quieto_no_impide_encajar()
    {
        int[,] world = World(64, 48);

        int[,] before = Cut(world, 10, 10);
        int[,] after = Cut(world, 12, 10);

        // Las dos primeras filas, iguales en las dos y sin moverse.
        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < 2; row++)
                before[column, row] = after[column, row] = 900 + column;
        }

        MapStitcher.Shift shift = MapStitcher.Between(before, after)!;

        Assert.Equal((2, 0), (shift.Columns, shift.Rows));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un mundo con un número distinto en cada celda, para que no encaje por azar.</summary>
    private static int[,] World(int columns, int rows, int seed = 1234)
    {
        var world = new int[columns, rows];
        var random = new Random(seed);

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
                world[column, row] = random.Next(1, 256);
        }

        return world;
    }

    /// <summary>Lo que se vería con la cámara ahí, que es lo que trae la tabla de nombres.</summary>
    private static int[,] Cut(int[,] world, int atColumn, int atRow)
    {
        var screen = new int[Columns, Rows];

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                int c = atColumn + column;
                int r = atRow + row;

                screen[column, row] =
                    c < world.GetLength(0) && r < world.GetLength(1) ? world[c, r] : 0;
            }
        }

        return screen;
    }
}
