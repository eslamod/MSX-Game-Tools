using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Una pantalla ya recortada del mapa.
/// </summary>
/// <param name="Column">En qué columna y fila de pantallas cae, contando desde cero.</param>
/// <param name="Left">Por qué celda del mapa empieza, que es lo que dice de dónde salió.</param>
/// <param name="Map">
/// El trozo, siempre de una pantalla entera: la del borde se queda corta en el mapa y las
/// celdas que faltan vienen vacías, que es lo que el exportador escribe como relleno.
/// </param>
public sealed record MapScreen(int Column, int Row, int Left, int Top, TileMap Map);

/// <summary>
/// Parte un mapa en las pantallas de las que está hecho.
/// </summary>
/// <remarks>
/// <para>
/// Para los juegos de pantallas fijas, que se dibujan de una sola vez —un mapa entero, con sus
/// pantallas pegadas— y luego se cargan de una en una. Aquí se corta por donde el editor pinta
/// la rejilla, y cada trozo sale como un mapa suyo para que lo escriba el mismo exportador.
/// </para>
/// <para>
/// En celdas y no en tiles: en un mapa de supertiles una celda son varios tiles, y lo que el
/// fichero lleva son los números de las celdas. Quien llame aquí ya ha hecho esa cuenta.
/// </para>
/// </remarks>
public static class MapScreens
{
    /// <summary>Cuántas pantallas de ese lado hacen falta para cubrir el mapa.</summary>
    /// <remarks>
    /// Redondeando hacia arriba: media pantalla al borde sigue siendo una pantalla, y lo que
    /// falta se rellena. Un mapa de 70 celdas con pantallas de 32 son tres, no dos.
    /// </remarks>
    public static int Count(int cells, int screen) => screen > 0 ? ((cells + screen - 1) / screen) : 0;

    /// <summary>Si el mapa se queda corto y hay que rellenar la última pantalla de algún lado.</summary>
    public static bool Pads(TileMap map, int wide, int high) =>
        wide > 0 && high > 0 && (map.Width % wide != 0 || map.Height % high != 0);

    /// <summary>
    /// Las pantallas que llevan algo dibujado, en orden de lectura.
    /// </summary>
    /// <remarks>
    /// Las vacías no salen: en un mapa de pantallas fijas lo normal es que el rectángulo no esté
    /// entero —una L, una cruz, un castillo con sus alas— y un fichero de 768 ceros por cada
    /// hueco del dibujo no es un mapa, es sitio gastado. Las que sí salen conservan su número,
    /// así que saltarse una no corre a las demás.
    /// </remarks>
    /// <param name="stem">De dónde sale el nombre de cada trozo, que es el del fichero.</param>
    public static IReadOnlyList<MapScreen> Of(TileMap map, int wide, int high, string stem)
    {
        var screens = new List<MapScreen>();

        if (wide <= 0 || high <= 0)
            return screens;

        // Una vez y no una por pantalla: aplastar es recorrer el mapa entero por cada capa, y
        // eso multiplicado por cien pantallas se nota.
        TileGrid flat = map.Flatten();

        int columns = Count(map.Width, wide);
        int rows = Count(map.Height, high);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int left = column * wide;
                int top = row * high;

                var cut = new TileMap($"{stem}_{column + 1}_{row + 1}", wide, high)
                {
                    EmptyTile = map.EmptyTile,
                };

                TileGrid grid = cut.Layers[0].Grid;

                for (int y = 0; y < high; y++)
                {
                    // Fuera del mapa la rejilla devuelve vacío, así que la pantalla del borde
                    // se rellena sola sin tener que mirar dónde acaba.
                    for (int x = 0; x < wide; x++)
                        grid[x, y] = flat[left + x, top + y];
                }

                if (!grid.IsEmpty)
                    screens.Add(new MapScreen(column, row, left, top, cut));
            }
        }

        return screens;
    }

    /// <summary>
    /// Las líneas de comentario que dicen de dónde salió esta pantalla.
    /// </summary>
    /// <remarks>
    /// El fichero de una pantalla suelta no dice de qué mapa es ni por dónde iba, y con veinte
    /// en la misma carpeta el nombre es lo único que queda. Esto lo deja escrito dentro.
    /// </remarks>
    public static IReadOnlyList<string> Notes(MapScreen screen, TileMap map)
    {
        int right = screen.Left + screen.Map.Width - 1;
        int bottom = screen.Top + screen.Map.Height - 1;

        var notes = new List<string>
        {
            $"; Screen {screen.Column + 1}-{screen.Row + 1} of {map.Name}"
            + $" - map columns {screen.Left}-{right}, rows {screen.Top}-{bottom}",
        };

        if (right >= map.Width || bottom >= map.Height)
        {
            notes.Add(
                $"; The map ends at column {map.Width - 1}, row {map.Height - 1}:"
                + $" the rest of this screen is tile {map.EmptyTile}.");
        }

        return notes;
    }
}
