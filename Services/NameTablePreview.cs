using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Dibuja la tabla de nombres de un volcado de VRAM, para ver cuál es.
/// </summary>
/// <remarks>
/// <para>
/// Un volcado no dice qué tabla estaba enseñando el juego. R#2 lo diría, pero hay juegos que la
/// cambian a media pantalla con la interrupción de línea —una para el marcador y otra para el
/// terreno— y entonces el registro sólo cuenta la mitad de la historia. Space Manbow es de
/// esos: leyéndolo una vez por fotograma sale la del marcador, y la captura del mapa acaba
/// siendo cientos de pantallas idénticas.
/// </para>
/// <para>
/// Con esto se prueban direcciones y se ve cuál es, que es una pregunta que se contesta de un
/// vistazo y no analizando bytes. La que salga es la que hay que darle al capturador.
/// </para>
/// </remarks>
public static class NameTablePreview
{
    /// <summary>Las celdas de la pantalla, que son las mismas en SCREEN 1, 2 y 4.</summary>
    public const int Columns = 32;

    /// <summary>Y sus filas.</summary>
    public const int Rows = 24;

    /// <summary>Los sitios donde puede empezar una tabla de nombres: cada 1 KB.</summary>
    public const int Step = 0x400;

    /// <summary>
    /// La pantalla que sale de esa tabla, o <c>null</c> si ahí no cabe.
    /// </summary>
    /// <remarks>
    /// Se dibuja con los juegos de tiles que trae <paramref name="tileSets"/>, así que sigue a
    /// las direcciones de patrones y colores que se hayan puesto: cambiando una, la vista
    /// cambia con ella. En GRAPHIC 2 y 3 cada tercio de la pantalla usa el suyo.
    /// </remarks>
    public static ImageMini? Draw(
        byte[] vram, int names, IReadOnlyList<TileSet> tileSets, ColorPalette palette)
    {
        if (tileSets.Count == 0 || names < 0 || names + (Columns * Rows) > vram.Length)
            return null;

        var image = new ImageMini(Columns * TileRow.Columns, Rows * Tile.Rows);

        for (int row = 0; row < Rows; row++)
        {
            // Con un solo juego de tiles los tres tercios son el mismo, que es lo que pasa en
            // SCREEN 1 y en las pantallas de SCREEN 2 que repiten los tres.
            TileSet third = tileSets[(row / 8) % tileSets.Count];

            for (int column = 0; column < Columns; column++)
            {
                Tile tile = third.ListOfTiles[vram[names + (row * Columns) + column]];

                Paint(image, tile, palette, column * TileRow.Columns, row * Tile.Rows);
            }
        }

        return image;
    }

    private static void Paint(ImageMini image, Tile tile, ColorPalette palette, int left, int top)
    {
        for (int row = 0; row < Tile.Rows; row++)
        {
            TileRow line = tile.ArrayTileRows[row];

            for (int column = 0; column < TileRow.Columns; column++)
            {
                int color = line.ArrayPattern[column] ? line.ForeColor : line.BackColor;

                image.SetPixel(left + column, top + row, palette.GetColor(color));
            }
        }
    }
}
