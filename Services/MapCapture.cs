using System.Globalization;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Lee lo que captura <c>map_grabber.tcl</c> y lo cose en mapas.
/// </summary>
/// <remarks>
/// <para>
/// El fichero es una cabecera y una línea por pantalla: el sello del juego de tiles que había
/// puesto y los números de la tabla de nombres. El script sólo apunta lo que vio; el cosido,
/// que es donde están las decisiones, se hace aquí con <see cref="MapStitcher"/>.
/// </para>
/// <para>
/// <b>Se corta cuando una pantalla no encaja con la anterior</b>, que es lo que pasa al cambiar
/// de sala: entonces se cierra el mapa y se empieza otro, porque pegarlos sería mezclar zonas
/// que no se tocan.
/// </para>
/// <para>
/// <b>Y no se corta por el sello del juego de tiles</b>, aunque el fichero lo traiga. Se hizo
/// así al principio y estaba mal: el sello cambia con cualquier tile animado —una cascada, un
/// objeto parpadeando— y con los patrones que muchos juegos redefinen para el scroll suave, así
/// que cortaba cada pocos fotogramas y una partida entera salía en miles de trozos. El cambio
/// de nivel ya lo caza la propia pantalla, que al cargar otro no se parece en nada.
/// </para>
/// <para>
/// El sello se sigue leyendo y guardando: dice con qué juego de tiles se dibujaba cada trozo, y
/// eso no se puede recuperar después sin volver a jugar la partida.
/// </para>
/// <para>
/// <b>El marcador hay que dejarlo fuera.</b> Casi todos los juegos tienen una franja que no
/// scrollea —la puntuación, las vidas—, y si entra en el cosido pasan dos cosas malas: no
/// coincide nunca, así que baja el porcentaje y hace que dos pantallas seguidas dejen de
/// encajar; y se estampa otra vez en cada posición nueva, dejando un reguero de marcadores por
/// todo el mapa. Por eso se cosen sólo las celdas de <see cref="Region"/>.
/// </para>
/// <para>
/// La zona se dice al importar y no al capturar, a propósito: acertarla a la primera es difícil
/// y cambiarla aquí no obliga a volver a jugarse la partida.
/// </para>
/// </remarks>
public static class MapCapture
{
    /// <summary>Lo que el script escribe en la primera línea, por si el formato cambia.</summary>
    public const string Magic = "msxmap";

    /// <summary>Pantallas que hay que juntar para dar por bueno un mapa.</summary>
    /// <remarks>
    /// Pasar por una sala un instante deja un mapa de una pantalla que no dice nada y llena la
    /// lista de estorbo. Se guardan los que se han recorrido.
    /// </remarks>
    public const int LeastScreens = 2;

    /// <summary>Una pantalla capturada, con el juego de tiles que había puesto.</summary>
    public sealed record Screen(long Stamp, int[,] Cells);

    /// <summary>Las celdas de la pantalla que son mapa, sin el marcador.</summary>
    public sealed record Region(int Left, int Top, int Columns, int Rows)
    {
        /// <summary>La pantalla entera, que es lo que vale mientras no se sepa qué recortar.</summary>
        public static Region Whole(Capture capture) => new(0, 0, capture.Columns, capture.Rows);

        /// <summary>Dentro de la pantalla y con algo dentro.</summary>
        public bool FitsIn(Capture capture) =>
            Left >= 0 && Top >= 0 && Columns > 0 && Rows > 0
            && Left + Columns <= capture.Columns
            && Top + Rows <= capture.Rows;
    }

    /// <summary>Lo que trae el fichero.</summary>
    public sealed record Capture(
        int Columns,
        int Rows,
        int Mode,
        int Names,
        int Patterns,
        int Colors,
        IReadOnlyList<Screen> Screens);

    public static Capture Read(string text)
    {
        var header = new Dictionary<string, int>();
        var screens = new List<Screen>();

        // Con el retorno de carro quitado: Tcl en Windows escribe CRLF, y partiendo solo
        // por el salto de linea el retorno se queda pegado al ultimo trozo de cada linea.
        // Entonces ni la marca de screens se reconoce ni los numeros de la cabecera se
        // dejan leer, y la captura sale vacia sin decir nada.
        string[] lines =
        [
            .. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()),
        ];

        if (lines.Length == 0 || !lines[0].StartsWith(Magic, StringComparison.Ordinal))
            throw new FileFormatException("Esto no es una captura de pantallas: falta la cabecera.");

        int at = 1;

        for (; at < lines.Length; at++)
        {
            string[] parts = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1 && parts[0] == "screens")
            {
                at++;
                break;
            }

            if (parts.Length == 2 && int.TryParse(parts[1], out int value))
                header[parts[0]] = value;
        }

        int columns = Header(header, "columns");
        int rows = Header(header, "rows");

        for (; at < lines.Length; at++)
        {
            string[] parts = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

            // El sello por delante y las celdas detras, tal como lo escribe el script.
            if (parts.Length != (columns * rows) + 1)
            {
                throw new FileFormatException(
                    $"La pantalla de la línea {at + 1} trae {parts.Length - 1} celdas "
                    + $"y tendría que traer {columns * rows}.");
            }

            var cells = new int[columns, rows];

            for (int cell = 0; cell < columns * rows; cell++)
            {
                cells[cell % columns, cell / columns] =
                    int.Parse(parts[cell + 1], CultureInfo.InvariantCulture);
            }

            screens.Add(new Screen(long.Parse(parts[0], CultureInfo.InvariantCulture), cells));
        }

        return new Capture(
            columns,
            rows,
            header.GetValueOrDefault("mode", 2),
            header.GetValueOrDefault("names"),
            header.GetValueOrDefault("patterns"),
            header.GetValueOrDefault("colors"),
            screens);
    }

    /// <summary>
    /// Cose las pantallas en los mapas que salgan.
    /// </summary>
    /// <remarks>
    /// El nombre lleva el número porque de una captura salen varios y hay que distinguirlos;
    /// cuál es cuál se ve abriéndolos, que es más rápido que cualquier nombre que me invente.
    /// </remarks>
    /// <summary>
    /// Lo quieta que hay que estar para dar una línea por marcador.
    /// </summary>
    /// <remarks>
    /// No es <c>1.0</c> porque los marcadores llevan números que suben: la fila de los dígitos
    /// cambia una o dos celdas de treinta y dos cada vez, y sigue siendo marcador.
    /// </remarks>
    public const double LeastStill = 0.90;

    /// <summary>
    /// La zona que parece mapa, mirando qué se mueve con la cámara y qué se queda clavado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La primera versión miraba <em>cuánto cambia</em> cada línea y daba por marcador las que
    /// menos cambian. Estaba mal, y con Knightmare se veía: proponía nueve celdas de una fila,
    /// que eran justo los dígitos de la puntuación. El marcador cambia <b>más</b> que el
    /// terreno, no menos —la puntuación sube a cada fotograma y el terreno sólo cambia cuando
    /// entra una fila de tiles entera—, así que ese criterio elegía precisamente lo peor.
    /// </para>
    /// <para>
    /// Lo que de verdad separa una cosa de la otra no es cuánto cambia una línea, sino si se
    /// mueve con la cámara. Así que por cada par de pantallas se busca hacia dónde fue la
    /// cámara, y después se le pregunta a cada fila y a cada columna si se parece más a lo que
    /// había <em>desplazado</em> o a lo que había <em>en el mismo sitio</em>. El terreno se
    /// parece a lo desplazado; el marcador, a lo de su sitio.
    /// </para>
    /// <para>
    /// Los pares en los que la cámara no se movió se tiran: ahí todo se parece a lo de su
    /// sitio, marcador y terreno, y contarlos sería dar por marcador la pantalla entera. Y
    /// cada eje sólo cuenta cuando la cámara se movió en ese eje, que en un juego de scroll
    /// vertical ninguna columna se mueve de sitio y preguntárselo sale que todas están
    /// quietas.
    /// </para>
    /// <para>
    /// Es una propuesta, no una certeza: una franja de cielo raso que nunca cambia se parece a
    /// las dos cosas por igual, y en el empate gana quedarse fuera, que colar el marcador
    /// estropea el mapa entero y perder una fila rasa no. Por eso se enseña en el formulario y
    /// se puede corregir.
    /// </para>
    /// </remarks>
    public static Region Suggest(Capture capture)
    {
        if (capture.Screens.Count < 2)
            return Region.Whole(capture);

        var columns = new Votes(capture.Columns);
        var rows = new Votes(capture.Rows);

        foreach ((int[,] before, int[,] after, MapStitcher.Shift shift) in Moving(capture))
        {
            // Cada eje solo se puede juzgar si la camara se movio en ese eje: con un scroll
            // vertical no hay manera de saber si una columna se mueve, porque ninguna se ha
            // movido de sitio. Preguntarselo igual da que todas estan quietas y recorta el
            // ancho entero.
            if (shift.Columns != 0)
            {
                columns.Seen++;

                Vote(before, after, shift, byColumn: true, columns);
            }

            if (shift.Rows != 0)
            {
                rows.Seen++;

                Vote(before, after, shift, byColumn: false, rows);
            }
        }

        (int first, int count) wide = columns.Band();
        (int first, int count) high = rows.Band();

        return new Region(wide.first, high.first, wide.count, high.count);
    }

    /// <summary>
    /// En cuántos pares de pantallas seguidas se movió la cámara.
    /// </summary>
    /// <remarks>
    /// Cero quiere decir que no hay nada que recomponer, y conviene decirlo con esas palabras:
    /// pasa cuando la captura está leyendo la tabla de nombres equivocada —hay juegos que la
    /// cambian a media pantalla con la interrupción de línea, una para el marcador y otra para
    /// el terreno— y también cuando el juego scrollea reescribiendo los patrones en vez de
    /// mover la tabla. Sin esto, lo único que se ve es que no sale ningún mapa.
    /// </remarks>
    public static int Moves(Capture capture) => Moving(capture).Count();

    /// <summary>Los pares de pantallas en los que la cámara se movió, y hacia dónde.</summary>
    private static IEnumerable<(int[,] Before, int[,] After, MapStitcher.Shift Shift)> Moving(
        Capture capture)
    {
        for (int screen = 1; screen < capture.Screens.Count; screen++)
        {
            int[,] before = capture.Screens[screen - 1].Cells;
            int[,] after = capture.Screens[screen].Cells;

            if (MapStitcher.Travelled(before, after) is not { } shift)
                continue;

            // Un par en el que la camara no se movio no dice nada de nadie.
            if (MapStitcher.Match(before, after, 0, 0) is { } still && still >= shift.Match)
                continue;

            yield return (before, after, shift);
        }
    }

    /// <summary>Cuántas veces se quedó clavada cada línea, de las veces que se pudo mirar.</summary>
    private sealed class Votes(int lines)
    {
        public int[] Stills { get; } = new int[lines];

        /// <summary>Los pares de pantallas en los que la cámara se movió, que son los que valen.</summary>
        public int Seen { get; set; }

        /// <summary>
        /// El trozo seguido más grande que no se ha ganado la fama de marcador.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Por mayoría y no por un voto suelto. La fila por donde entra el scroll no tiene de
        /// dónde venir, así que nunca puede votar que se mueve: sus únicos votos posibles son
        /// los de quedarse quieta, y con dos casualidades de doscientas la primera versión daba
        /// la fila de arriba de Knightmare por marcador y recortaba terreno bueno.
        /// </para>
        /// <para>
        /// Marcador es lo que se queda clavado <b>la mayoría</b> de las veces que la cámara se
        /// movió. Una línea que no se pudo mirar nunca cuenta como terreno.
        /// </para>
        /// </remarks>
        public (int First, int Count) Band()
        {
            int bestFirst = 0;
            int best = 0;
            int runFirst = 0;
            int run = 0;

            for (int line = 0; line < Stills.Length; line++)
            {
                if (Stills[line] * 2 <= Seen)
                {
                    if (run++ == 0)
                        runFirst = line;

                    if (run > best)
                    {
                        best = run;
                        bestFirst = runFirst;
                    }
                }
                else
                {
                    run = 0;
                }
            }

            return best == 0 ? (0, Stills.Length) : (bestFirst, best);
        }
    }

    /// <summary>Le pregunta a cada línea si se parece más a lo desplazado o a lo de su sitio.</summary>
    private static void Vote(
        int[,] before, int[,] after, MapStitcher.Shift shift, bool byColumn, Votes votes)
    {
        int columns = before.GetLength(0);
        int rows = before.GetLength(1);

        int many = byColumn ? columns : rows;
        int across = byColumn ? rows : columns;

        for (int line = 0; line < many; line++)
        {
            int moved = 0;
            int still = 0;
            int counted = 0;

            for (int at = 0; at < across; at++)
            {
                int column = byColumn ? line : at;
                int row = byColumn ? at : line;

                if (after[column, row] == before[column, row])
                    still++;

                int fromColumn = column + shift.Columns;
                int fromRow = row + shift.Rows;

                if (fromColumn < 0 || fromColumn >= columns || fromRow < 0 || fromRow >= rows)
                    continue;

                counted++;

                if (after[column, row] == before[fromColumn, fromRow])
                    moved++;
            }

            double stillRate = (double)still / across;

            // La linea del borde por donde entra el scroll no tiene de donde venir, asi que
            // no se le puede preguntar si se movio. Ahi solo se la condena si es identica: un
            // cielo raso se queda casi igual al scrollear sin ser marcador, y con el liston de
            // las demas se recortaria la fila de arriba de medio catalogo.
            if (counted == 0)
            {
                if (still == across)
                    votes.Stills[line]++;

                continue;
            }

            // Parecerse mas a lo desplazado que a lo de su sitio la salva aunque este quieta:
            // una franja de un solo tile se parece a las dos cosas y no delata a nadie.
            if ((double)moved / counted <= stillRate && stillRate >= LeastStill)
                votes.Stills[line]++;
        }
    }

    /// <param name="leastMatch"><inheritdoc cref="MapStitcher.LeastMatch" path="/summary"/></param>
    public static IReadOnlyList<TileMap> Stitch(
        Capture capture,
        string name,
        Region? region = null,
        double leastMatch = MapStitcher.LeastMatch)
    {
        Region cut = region is not null && region.FitsIn(capture)
            ? region
            : Region.Whole(capture);

        var maps = new List<TileMap>();

        MapStitcher stitcher = new(leastMatch);

        foreach (Screen screen in capture.Screens)
        {
            int[,] cells = Crop(screen.Cells, cut);

            if (stitcher.Feed(cells))
                continue;

            Keep(maps, stitcher, name);

            stitcher = new MapStitcher(leastMatch);
            stitcher.Feed(cells);
        }

        Keep(maps, stitcher, name);

        return maps;
    }

    private static int[,] Crop(int[,] cells, Region region)
    {
        var cut = new int[region.Columns, region.Rows];

        for (int column = 0; column < region.Columns; column++)
        {
            for (int row = 0; row < region.Rows; row++)
                cut[column, row] = cells[region.Left + column, region.Top + row];
        }

        return cut;
    }

    private static void Keep(List<TileMap> maps, MapStitcher stitcher, string name)
    {
        if (stitcher.Screens < LeastScreens)
            return;

        int?[,] grid = stitcher.ToGrid();

        var map = new TileMap($"{name} {maps.Count + 1}", grid.GetLength(0), grid.GetLength(1));

        // Directo a la rejilla, como hace la importación de csv: aquí no hay nada que deshacer,
        // que el mapa se acaba de crear.
        TileGrid cells = map.Layers[0].Grid;

        for (int column = 0; column < grid.GetLength(0); column++)
        {
            for (int row = 0; row < grid.GetLength(1); row++)
                cells[column, row] = grid[column, row];
        }

        maps.Add(map);
    }

    private static int Header(Dictionary<string, int> header, string key) =>
        header.TryGetValue(key, out int value) && value > 0
            ? value
            : throw new FileFormatException($"A la captura le falta «{key}» en la cabecera.");
}
