using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que captura el script de openMSX, leído y cosido.
/// </summary>
/// <remarks>
/// Las capturas de estas pruebas se escriben con el mismo formato que emite
/// <c>map_grabber.tcl</c>: cabecera, la palabra <c>screens</c>, y una línea por pantalla con
/// el sello del juego de tiles por delante.
/// </remarks>
public class MapCaptureTests
{
    private const int Columns = 8;
    private const int Rows = 24;

    /// <summary>Una captura de una zona que se recorre sale como un mapa más ancho.</summary>
    [Fact]
    public void Una_zona_recorrida_sale_como_un_mapa()
    {
        int[,] world = World(16, Rows);

        string text = Written(1, Cuts(world, 0, 1, 2, 3));

        TileMap map = Assert.Single(MapCapture.Stitch(MapCapture.Read(text), "Zona"));

        Assert.Equal(Columns + 3, map.Width);
        Assert.Equal(Rows, map.Height);

        for (int column = 0; column < map.Width; column++)
        {
            for (int row = 0; row < Rows; row++)
                Assert.Equal(world[column, row], map.Layers[0].Grid[column, row]);
        }
    }

    /// <summary>
    /// Cambiar el sello del juego de tiles no corta el mapa.
    /// </summary>
    /// <remarks>
    /// Al principio sí cortaba, y estaba mal. El sello cambia con cualquier tile animado y con
    /// los patrones que muchos juegos redefinen para el scroll suave: cortaba cada pocos
    /// fotogramas y una partida salía en miles de trozos de dos pantallas. Lo que de verdad
    /// dice que se ha cambiado de sitio es que la pantalla no encaje, y eso ya se mira.
    /// </remarks>
    [Fact]
    public void Cambiar_de_juego_de_tiles_no_corta_el_mapa()
    {
        int[,] world = World(16, Rows);

        // Las mismas pantallas, encajando todas, pero con el sello cambiando a cada una.
        string text = Written(
            [(1, Cut(world, 0)), (2, Cut(world, 1)), (3, Cut(world, 2)), (4, Cut(world, 3))]);

        TileMap map = Assert.Single(MapCapture.Stitch(MapCapture.Read(text), "Zona"));

        Assert.Equal(Columns + 3, map.Width);
    }

    /// <summary>Y cambiar de sala también, aunque el juego de tiles siga siendo el mismo.</summary>
    [Fact]
    public void Cambiar_de_sala_corta_el_mapa()
    {
        int[,] one = World(16, Rows);
        int[,] other = World(16, Rows, seed: 4242);

        string text = Written(
            [(1, Cut(one, 0)), (1, Cut(one, 1)), (1, Cut(other, 0)), (1, Cut(other, 1))]);

        Assert.Equal(2, MapCapture.Stitch(MapCapture.Read(text), "Zona").Count);
    }

    /// <summary>
    /// Una sala por la que sólo se pasó un momento no deja mapa.
    /// </summary>
    /// <remarks>
    /// Un mapa de una sola pantalla no dice nada y llena la lista de estorbo. Se guarda lo que
    /// se ha recorrido.
    /// </remarks>
    [Fact]
    public void Una_sala_de_paso_no_deja_mapa()
    {
        int[,] one = World(16, Rows);
        int[,] other = World(16, Rows, seed: 555);

        string text = Written(
            [(1, Cut(one, 0)), (1, Cut(other, 0)), (1, Cut(one, 0)), (1, Cut(one, 1))]);

        // La del medio se queda en una pantalla suelta; la primera tambien.
        TileMap map = Assert.Single(MapCapture.Stitch(MapCapture.Read(text), "Zona"));

        Assert.Equal(Columns + 1, map.Width);
    }

    /// <summary>
    /// Una captura en la que la cámara nunca se mueve se sabe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es lo que sale cuando la captura lee la tabla de nombres equivocada —hay juegos que la
    /// cambian a media pantalla con la interrupción de línea, una para el marcador y otra para
    /// el terreno— y también cuando el juego scrollea reescribiendo los patrones.
    /// </para>
    /// <para>
    /// Se cuenta aparte de que salgan o no mapas porque no es lo mismo: sin ningún mapa lo
    /// único que se sabe es que no salió nada, y esto dice además que no había nada que sacar.
    /// </para>
    /// </remarks>
    [Fact]
    public void Una_captura_que_no_se_mueve_se_sabe()
    {
        Assert.Equal(0, MapCapture.Moves(MapCapture.Read(Written(Frozen(6)))));
    }

    /// <summary>Y una que se recorre cuenta los pares en los que se movió.</summary>
    [Fact]
    public void Una_captura_que_se_recorre_cuenta_sus_pasos()
    {
        Assert.Equal(3, MapCapture.Moves(MapCapture.Read(Written(Falling(4)))));
    }

    /// <summary>Un fichero que no es una captura se rechaza diciendo por qué.</summary>
    [Fact]
    public void Un_fichero_que_no_es_una_captura_se_rechaza()
    {
        Assert.Throws<FileFormatException>(() => MapCapture.Read("hola\nqué tal\n"));
    }

    /// <summary>Y una pantalla con celdas de menos, también.</summary>
    /// <remarks>
    /// Rellenarla o recortarla sería peor: saldría un mapa con pinta de bueno y desplazado a
    /// partir de ahí, sin nada que lo delatara.
    /// </remarks>
    [Fact]
    public void Una_pantalla_a_medias_se_rechaza()
    {
        string text = "msxmap 1\ncolumns 8\nrows 4\nscreens\n7 1 2 3\n";

        FileFormatException failed =
            Assert.Throws<FileFormatException>(() => MapCapture.Read(text));

        Assert.Contains("32", failed.Message);
    }

    /// <summary>
    /// Un marcador que no scrollea deja de estropear el mapa si se recorta.
    /// </summary>
    /// <remarks>
    /// Es lo que pasó con Knightmare: el marcador se queda quieto abajo, así que cada vez que
    /// la cámara baja una celda se vuelve a estampar una fila más abajo y deja un reguero. Un
    /// mapa de 32x239 del que casi todo eran puntuaciones repetidas.
    /// </remarks>
    /// <remarks>
    /// Que llegara a coser es de suerte: son dos filas de veinticuatro, un 8% de la pantalla,
    /// así que el encaje se queda en el 91% y pasa por poco el listón del 90%. Con una fila más
    /// no habría cosido nada y el fallo habría sido otro.
    /// </remarks>
    [Fact]
    public void Recortando_el_marcador_el_mapa_sale_limpio()
    {
        // Scroll vertical, que es donde el marcador hace el estropicio: bajando la camara se
        // estampa una fila mas abajo cada vez.
        MapCapture.Capture capture = MapCapture.Read(Written(Falling(4)));

        TileMap whole = Assert.Single(MapCapture.Stitch(capture, "Zona"));

        // Sin recortar, el marcador va cosido dentro del mapa.
        Assert.Equal(Rows + 3, whole.Height);
        Assert.Equal(Values, whole.Layers[0].Grid[0, whole.Height - 1]);

        // Recortandolo, el mapa es solo terreno y no queda ni rastro del marcador.
        TileMap cut = Assert.Single(
            MapCapture.Stitch(capture, "Zona", new MapCapture.Region(0, 0, Columns, Rows - Marker)));

        Assert.Equal(Rows - Marker + 3, cut.Height);
        Assert.Equal(Columns, cut.Width);

        for (int column = 0; column < cut.Width; column++)
        {
            for (int row = 0; row < cut.Height; row++)
            {
                int cell = cut.Layers[0].Grid[column, row] ?? 0;

                Assert.NotEqual(Labels + column, cell);
                Assert.NotEqual(Values + column, cell);
            }
        }
    }

    /// <summary>Y la zona se propone sola, mirando qué se mueve con la cámara.</summary>
    /// <remarks>
    /// El marcador se delata porque se queda clavado en su sitio mientras el terreno se
    /// desplaza: no porque cambie más o menos.
    /// </remarks>
    [Fact]
    public void La_zona_del_mapa_se_propone_sola()
    {
        MapCapture.Region region =
            MapCapture.Suggest(MapCapture.Read(Written(Falling(4))));

        Assert.Equal(new MapCapture.Region(0, 0, Columns, Rows - Marker), region);
    }

    /// <summary>
    /// Una partida de verdad tampoco la engaña.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Las tres trampas juntas, que son las que tenía la captura de Knightmare y las que se
    /// llevaron por delante la primera versión de la propuesta.
    /// </para>
    /// <para>
    /// <b>La puntuación sube todo el rato.</b> El marcador cambia más que el terreno, no
    /// menos: la primera versión buscaba las líneas que más cambian y proponía nueve celdas de
    /// una fila, que eran justo los dígitos.
    /// </para>
    /// <para>
    /// <b>La mayoría de los pares están quietos.</b> Se capturó más a menudo de lo que el
    /// juego scrollea, así que en más de la mitad de los pares la cámara no se movió. Contando
    /// esos, todo parece marcador.
    /// </para>
    /// <para>
    /// <b>La fila por donde entra el scroll no tiene de dónde venir</b>, así que nunca puede
    /// votar que se mueve. Como además el mundo tiene dos filas iguales, alguna vez parece
    /// quieta de casualidad, y eso bastaba para recortar terreno bueno.
    /// </para>
    /// </remarks>
    [Fact]
    public void Una_puntuacion_que_sube_no_pasa_por_terreno()
    {
        MapCapture.Region region = MapCapture.Suggest(MapCapture.Read(Written(Climbing())));

        Assert.Equal(new MapCapture.Region(0, 0, Columns, Rows - Marker), region);
    }

    /// <summary>
    /// Un cielo raso no pasa por marcador aunque apenas cambie.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la diferencia entre cambiar y moverse. Una franja casi vacía —un cielo con una
    /// estrella— se queda casi igual de una captura a la siguiente, así que por lo poco que
    /// cambia parecería marcador; pero se parece todavía más a lo desplazado que a lo de su
    /// sitio, y eso la salva.
    /// </para>
    /// <para>
    /// Sin mirarlo, un juego con mucho cielo perdería medio mapa.
    /// </para>
    /// </remarks>
    [Fact]
    public void Un_cielo_raso_no_pasa_por_marcador()
    {
        MapCapture.Region region = MapCapture.Suggest(MapCapture.Read(Written(Sky())));

        Assert.Equal(new MapCapture.Region(0, 0, Wide, Rows - 1), region);
    }

    /// <summary>Lo ancha que es la pantalla del cielo, que con ocho columnas no da la talla.</summary>
    /// <remarks>
    /// Con ocho, una estrella que se mueve ya es la octava parte de la fila y la deja por
    /// debajo del listón de quedarse quieta. Un marcador de verdad son treinta y dos.
    /// </remarks>
    private const int Wide = 32;

    /// <summary>
    /// Un mundo de cielo raso: todo la misma celda menos tres estrellas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Una estrella por fila, cambiando de columna, para que entre dos filas seguidas se muevan
    /// dos celdas de treinta y dos: lo justo para que la fila siga pareciendo quieta y aun así
    /// se sepa cuál es cuál.
    /// </para>
    /// <para>
    /// Y en columnas distintas para que las columnas tampoco sean degeneradas: en un cielo con
    /// las estrellas siempre en el mismo sitio, las columnas vacías no cambian jamás y son
    /// marcador con todas las de la ley.
    /// </para>
    /// </remarks>
    private static (long Stamp, int[,] Cells)[] Sky()
    {
        const int travel = 6;
        const int empty = 1;

        var world = new int[Wide, Rows + travel];

        for (int row = 0; row < Rows + travel; row++)
        {
            for (int column = 0; column < Wide; column++)
                world[column, row] = empty;

            world[row * 7 % Wide, row] = 300 + row;
        }

        // Un marcador de una fila y no de dos: con dos, un cielo encaja mejor quedandose
        // quieto que desplazandose, y entonces el par entero se tira por no decir nada.
        return [.. Enumerable.Range(0, travel + 1).Select(step =>
        {
            int at = travel - step;
            var screen = new int[Wide, Rows];

            for (int column = 0; column < Wide; column++)
            {
                for (int row = 0; row < Rows - 1; row++)
                    screen[column, row] = world[column, at + row];

                screen[column, Rows - 1] = Labels + column;
            }

            return ((long)1, screen);
        })];
    }

    /// <summary>
    /// La cámara subiendo por un mundo, como Knightmare.
    /// </summary>
    /// <remarks>
    /// El terreno entra por arriba, el marcador se queda abajo con la puntuación subiendo, y
    /// por cada posición se capturan dos pantallas: en la segunda sólo sube el marcador.
    /// </remarks>
    private static (long Stamp, int[,] Cells)[] Climbing()
    {
        const int travel = 8;

        int[,] world = World(Columns, Rows + travel);

        // Dos filas del mundo iguales: al pasar por ahi, la fila de arriba de la pantalla se
        // repite y parece quieta sin serlo.
        for (int column = 0; column < Columns; column++)
            world[column, 3] = world[column, 4];

        var screens = new List<(long, int[,])>();
        int score = 0;

        for (int at = travel; at >= 0; at--)
        {
            for (int twice = 0; twice < 2; twice++)
            {
                var screen = new int[Columns, Rows];

                for (int column = 0; column < Columns; column++)
                {
                    for (int row = 0; row < Rows - Marker; row++)
                        screen[column, row] = world[column, at + row];

                    screen[column, Rows - 2] = Labels + column;
                    screen[column, Rows - 1] = Values + column;
                }

                screen[0, Rows - 1] = 900 + score++;

                screens.Add((1, screen));
            }
        }

        return [.. screens];
    }

    /// <summary>
    /// La misma pantalla una y otra vez, con sólo el marcador cambiando.
    /// </summary>
    /// <remarks>
    /// Como la captura de Space Manbow leyendo la tabla del marcador: el terreno clavado y dos
    /// celdas de puntuación moviéndose. Cambia lo justo para que el script la dé por pantalla
    /// nueva y la escriba.
    /// </remarks>
    private static (long Stamp, int[,] Cells)[] Frozen(int screens)
    {
        int[,] world = World(Columns, Rows);

        return [.. Enumerable.Range(0, screens).Select(at =>
        {
            var screen = new int[Columns, Rows];

            for (int column = 0; column < Columns; column++)
            {
                for (int row = 0; row < Rows; row++)
                    screen[column, row] = world[column, row];
            }

            screen[0, Rows - 1] = 900 + at;

            return ((long)1, screen);
        })];
    }

    /// <summary>Las dos filas de marcador de abajo: los rotulos y los numeros.</summary>
    private const int Marker = 2;

    private const int Labels = 500;
    private const int Values = 700;

    /// <summary>La camara bajando por un mundo, con el marcador quieto abajo.</summary>
    private static (long Stamp, int[,] Cells)[] Falling(int screens)
    {
        int[,] world = World(Columns, Rows + screens);

        return [.. Enumerable.Range(0, screens).Select(at =>
        {
            var screen = new int[Columns, Rows];

            for (int column = 0; column < Columns; column++)
            {
                for (int row = 0; row < Rows; row++)
                    screen[column, row] = world[column, at + row];

                screen[column, Rows - 2] = Labels + column;
                screen[column, Rows - 1] = Values + column;
            }

            return ((long)1, screen);
        })];
    }

    // ------------------------------------------------------------------ los andamios

    private static int[,] World(int columns, int rows, int seed = 99)
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

    private static int[,] Cut(int[,] world, int at)
    {
        var screen = new int[Columns, Rows];

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
                screen[column, row] = world[at + column, row];
        }

        return screen;
    }

    private static (long Stamp, int[,] Cells)[] Cuts(int[,] world, params int[] at) =>
        [.. at.Select(one => ((long)1, Cut(world, one)))];

    private static string Written(long stamp, (long Stamp, int[,] Cells)[] screens) =>
        Written(screens);

    /// <summary>El mismo formato que escribe el script.</summary>
    private static string Written((long Stamp, int[,] Cells)[] screens)
    {
        int columns = screens[0].Cells.GetLength(0);
        int rows = screens[0].Cells.GetLength(1);

        var text = new System.Text.StringBuilder();

        text.AppendLine("msxmap 1");
        text.AppendLine($"columns {columns}");
        text.AppendLine($"rows {rows}");
        text.AppendLine("mode 2");
        text.AppendLine("names 6144");
        text.AppendLine("patterns 0");
        text.AppendLine("colors 8192");
        text.AppendLine("screens");

        foreach ((long stamp, int[,] cells) in screens)
        {
            var numbers = new List<string> { stamp.ToString() };

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                    numbers.Add(cells[column, row].ToString());
            }

            text.AppendLine(string.Join(' ', numbers));
        }

        return text.ToString();
    }
}
