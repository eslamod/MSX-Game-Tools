using Avalonia;
using Avalonia.Media;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.Services;

/// <summary>Un problema que impide importar una imagen.</summary>
/// <param name="TileIndex">Tile donde está, o -1 si es de la imagen entera.</param>
/// <param name="Row">Línea dentro del tile, o -1.</param>
public sealed record TileSetImportProblem(string Message, int TileIndex = -1, int Row = -1);

/// <summary>Lo que sale de analizar una imagen: o los tiles, o lo que impide traerlos.</summary>
public sealed record TileSetImportResult(TileSet? TileSet, IReadOnlyList<TileSetImportProblem> Problems)
{
    public bool Ok => TileSet is not null;
}

/// <summary>
/// Convierte entre un juego de tiles y una imagen de pixeles, para poder llevarse el
/// trabajo a GIMP y traerlo de vuelta.
/// </summary>
/// <remarks>
/// <para>
/// Exportar no pierde nada; importar sí puede, y por eso se comprueba antes en vez de
/// aproximar en silencio. En GRAPHIC 2 una línea de ocho pixeles admite exactamente dos
/// colores, y sólo de los dieciséis de la paleta.
/// </para>
/// <para>
/// El orden de comprobación importa: primero se lleva cada pixel a su índice de paleta y
/// <b>después</b> se cuentan los colores por línea. La limitación es sobre índices, no
/// sobre RGB: dos rojos casi iguales del png caen en el mismo índice y esa línea es
/// perfectamente legal, aunque en la imagen fueran dos colores distintos.
/// </para>
/// </remarks>
public static class TileSetPngConverter
{
    /// <summary>Tiles por fila en la imagen, la misma disposición que la rejilla del editor.</summary>
    public const int TilesPerRow = 32;

    /// <summary>Colores que caben en una línea de ocho pixeles.</summary>
    public const int ColorsPerLine = 2;

    /// <summary>Cuántos problemas se enseñan antes de resumir. Una imagen mala da cientos.</summary>
    public const int MaxReportedProblems = 10;

    /// <summary>Tamaño exacto de la imagen que representa un juego entero.</summary>
    public static PixelSize FullSize => new(TilesPerRow * TileRow.Columns, Rows * Tile.Rows);

    private static int Rows => TileSet.TileCount / TilesPerRow;

    /// <summary>
    /// Los pixeles del juego entero, en BGRA y en el orden de la rejilla.
    /// </summary>
    /// <remarks>
    /// Un pixel con el código 0 sale transparente, no del color del borde: en la máquina
    /// ese pixel deja ver el fondo, y así al reimportar vuelve al 0 solo.
    /// </remarks>
    public static int[] ToPixels(TileSet tileSet, ColorPalette palette)
    {
        PixelSize size = FullSize;
        int[] pixels = new int[size.Width * size.Height];

        for (int index = 0; index < tileSet.ListOfTiles.Count; index++)
        {
            Tile tile = tileSet.ListOfTiles[index];

            int left = (index % TilesPerRow) * TileRow.Columns;
            int top = (index / TilesPerRow) * Tile.Rows;

            for (int row = 0; row < Tile.Rows; row++)
            {
                TileRow line = tile.ArrayTileRows[row];

                for (int column = 0; column < TileRow.Columns; column++)
                {
                    int colorIndex = line.ArrayPattern[column] ? line.ForeColor : line.BackColor;

                    pixels[((top + row) * size.Width) + left + column] =
                        colorIndex == 0 ? 0 : ToBgra(palette.GetColor(colorIndex));
                }
            }
        }

        return pixels;
    }

    /// <summary>Los colores distintos de una imagen, sin contar los transparentes.</summary>
    public static IReadOnlyList<Color> DistinctColors(int[] pixels)
    {
        var seen = new List<Color>();

        foreach (int pixel in pixels)
        {
            if (IsTransparent(pixel))
                continue;

            Color color = FromBgra(pixel);

            if (!seen.Contains(color))
                seen.Add(color);
        }

        return seen;
    }

    /// <summary>
    /// Construye una paleta con los colores de la imagen.
    /// </summary>
    /// <remarks>
    /// Empieza a repartir en el índice 1: el 0 es el transparente del VDP y usarlo como
    /// color haría que esos pixeles enseñaran el borde en la máquina. Por eso caben
    /// quince colores y no dieciséis.
    /// </remarks>
    public static ColorPalette BuildPalette(string name, IReadOnlyList<Color> colors)
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard().Clone(name);

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            Color color = index >= 1 && index - 1 < colors.Count ? colors[index - 1] : Colors.Black;

            palette[index].SetComponents(ToComponent(color.R), ToComponent(color.G), ToComponent(color.B));
        }

        return palette;
    }

    /// <summary>Colores que caben en una paleta generada, sin contar el transparente.</summary>
    public static int MaxGeneratedColors => ColorPalette.Size - 1;

    /// <summary>
    /// Analiza una imagen y devuelve el juego de tiles, o lo que impide traerlo.
    /// </summary>
    public static TileSetImportResult Analyse(int[] pixels, PixelSize size, ColorPalette palette, string name)
    {
        var problems = new List<TileSetImportProblem>();

        if (size.Width % TileRow.Columns != 0 || size.Height % Tile.Rows != 0)
        {
            problems.Add(new TileSetImportProblem(
                $"La imagen mide {size.Width}x{size.Height} y sus dos lados tienen que ser múltiplos de "
                + $"{TileRow.Columns}, que es lo que mide un tile."));

            return new TileSetImportResult(null, problems);
        }

        int columns = size.Width / TileRow.Columns;
        int rows = size.Height / Tile.Rows;
        int count = columns * rows;

        if (count > TileSet.TileCount)
        {
            problems.Add(new TileSetImportProblem(
                $"La imagen trae {count} tiles y un juego sólo tiene {TileSet.TileCount}."));

            return new TileSetImportResult(null, problems);
        }

        var tileSet = new TileSet(name);

        for (int index = 0; index < count; index++)
        {
            int left = (index % columns) * TileRow.Columns;
            int top = (index / columns) * Tile.Rows;

            ReadTile(pixels, size.Width, left, top, palette, tileSet.ListOfTiles[index], index, problems);

            if (problems.Count > MaxReportedProblems)
                break;
        }

        return problems.Count > 0
            ? new TileSetImportResult(null, problems)
            : new TileSetImportResult(tileSet, problems);
    }

    private static void ReadTile(
        int[] pixels,
        int stride,
        int left,
        int top,
        ColorPalette palette,
        Tile tile,
        int index,
        List<TileSetImportProblem> problems)
    {
        for (int row = 0; row < Tile.Rows; row++)
        {
            int[] indices = new int[TileRow.Columns];
            var distinct = new List<int>();

            for (int column = 0; column < TileRow.Columns; column++)
            {
                int pixel = pixels[((top + row) * stride) + left + column];

                // El transparente del png es el codigo 0, que en la maquina es justo eso.
                indices[column] = IsTransparent(pixel) ? 0 : NearestIndex(palette, FromBgra(pixel));

                if (!distinct.Contains(indices[column]))
                    distinct.Add(indices[column]);
            }

            if (distinct.Count > ColorsPerLine)
            {
                problems.Add(new TileSetImportProblem(
                    $"El tile {index}, línea {row}, usa {distinct.Count} colores y sólo caben {ColorsPerLine}.",
                    index,
                    row));

                // El corte va aqui y no solo entre tiles: un tile puede dar ocho
                // problemas el solo, y con la imagen entera mal se pasaria de largo.
                if (problems.Count > MaxReportedProblems)
                    return;

                continue;
            }

            // Con un solo color, frente y fondo son el mismo y no se enciende ningun bit.
            TileRow line = tile.ArrayTileRows[row];

            line.BackColor = distinct[0];
            line.ForeColor = distinct.Count > 1 ? distinct[1] : distinct[0];

            for (int column = 0; column < TileRow.Columns; column++)
                line.ArrayPattern[column] = indices[column] == line.ForeColor && distinct.Count > 1;
        }
    }

    /// <summary>El índice de la paleta cuyo color está más cerca, por distancia en RGB.</summary>
    private static int NearestIndex(ColorPalette palette, Color color)
    {
        int best = 1;
        int bestDistance = int.MaxValue;

        // Desde el 1: el 0 es el transparente y sólo se usa para los pixeles con alfa.
        for (int index = 1; index < ColorPalette.Size; index++)
        {
            Color candidate = palette.GetColor(index);

            int dr = candidate.R - color.R;
            int dg = candidate.G - color.G;
            int db = candidate.B - color.B;
            int distance = (dr * dr) + (dg * dg) + (db * db);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = index;
        }

        return best;
    }

    /// <summary>De 0-255 a los tres bits por componente del MSX2.</summary>
    private static int ToComponent(byte value) => (value * 7) / 255;

    private static bool IsTransparent(int bgra) => (uint)bgra >> 24 == 0;

    private static Color FromBgra(int bgra) => Color.FromRgb(
        (byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF));

    private static int ToBgra(Color color) =>
        (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
}
