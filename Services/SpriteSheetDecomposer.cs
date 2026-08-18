using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Un plano de una celda: a qué color va y qué pixeles pinta.
/// </summary>
/// <param name="Color">
/// El índice de paleta del plano, siempre una potencia de dos. Es lo que hace que el OR del
/// VDP reconstruya el índice de cada pixel sin más cuentas.
/// </param>
/// <param name="Rows">Los pixeles que pinta, por líneas.</param>
public sealed record SheetPlane(int Color, IReadOnlyList<bool[]> Rows);

/// <summary>
/// Parte una celda de la hoja en los sprites que hay que superponer.
/// </summary>
/// <remarks>
/// <para>
/// Es la parte mecánica, y lo es porque el reparto de índices ya se hizo antes. Con los planos
/// coloreados a potencias de dos, el plano del bit <c>i</c> pinta exactamente los pixeles cuyo
/// índice tiene ese bit, y el OR del hardware devuelve el índice entero. No hay nada que
/// buscar: se lee el bit y ya está.
/// </para>
/// <para>
/// Cada línea es independiente —en modo 2 tanto los bits del patrón como el color van por
/// línea—, así que esto son 16 problemas de 16 pixeles y no uno de 256.
/// </para>
/// <para>
/// El primer plano va sin CC y los demás con CC, que es lo que encadena el OR. El orden es la
/// prioridad, y aquí sale de menor a mayor bit para que sea el mismo siempre: dos celdas con
/// los mismos colores dan planos en el mismo orden y sus patrones se pueden comparar.
/// </para>
/// </remarks>
public static class SpriteSheetDecomposer
{
    /// <summary>
    /// Saca los planos de una celda.
    /// </summary>
    /// <param name="colors">Los colores de la hoja, tal como los dio el análisis.</param>
    /// <param name="masks">El índice de paleta de cada uno de esos colores.</param>
    /// <returns>
    /// Un plano por cada bit que use la celda, de menor a mayor. Vacío si la celda está entera
    /// en transparente, que es un hueco de la hoja y no gasta ningún patrón.
    /// </returns>
    public static IReadOnlyList<SheetPlane> Decompose(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        IReadOnlyList<Color> colors,
        IReadOnlyList<int> masks,
        int column,
        int row,
        SpriteBank.SpriteType type = SpriteBank.SpriteType.MSX2)
    {
        int left = column * cellSize;
        int top = row * cellSize;

        // Qué índice tiene cada pixel de la celda. Se lee una vez: leerlo por plano seria
        // recorrer la celda cuatro veces para sacar cuatro bits del mismo número.
        int[,] indices = new int[cellSize, cellSize];
        int used = 0;

        for (int y = 0; y < cellSize; y++)
        {
            for (int x = 0; x < cellSize; x++)
            {
                int color = SpriteSheetPixels.ColorAt(
                    pixels, size, left + x, top + y, transparent, colors);

                int mask = color >= 0 && color < masks.Count ? masks[color] : 0;

                indices[y, x] = mask;
                used |= mask;
            }
        }

        var planes = new List<SheetPlane>();

        // En MSX1 un plano es un color entero, no un bit: los sprites que se solapan no mezclan
        // nada, así que cada color se pinta con su propio sprite y ahí se acaba la historia.
        // De menor a mayor índice, para que dos celdas con los mismos colores den los planos en
        // el mismo orden y sus patrones se puedan comparar.
        if (type == SpriteBank.SpriteType.MSX)
        {
            foreach (int color in Used(indices, cellSize).Order())
                planes.Add(new SheetPlane(color, Mask(indices, cellSize, value => value == color)));

            return planes;
        }

        for (int bit = 0; bit < SpritePlaneAssignment.MaxPlanes; bit++)
        {
            int color = 1 << bit;

            // Sólo los bits que la celda usa: un plano en blanco gastaría un hueco del banco
            // y un sprite de los que caben en la línea de barrido, para no pintar nada.
            if ((used & color) == 0)
                continue;

            planes.Add(new SheetPlane(color, Mask(indices, cellSize, value => (value & color) != 0)));
        }

        return planes;
    }

    /// <summary>Los índices que la celda usa de verdad, sin el transparente.</summary>
    private static IEnumerable<int> Used(int[,] indices, int cellSize)
    {
        var seen = new HashSet<int>();

        for (int y = 0; y < cellSize; y++)
        {
            for (int x = 0; x < cellSize; x++)
            {
                if (indices[y, x] != 0)
                    seen.Add(indices[y, x]);
            }
        }

        return seen;
    }

    /// <summary>Los pixeles de la celda que cumplen algo, que es lo que dibuja un plano.</summary>
    private static List<bool[]> Mask(int[,] indices, int cellSize, Func<int, bool> paints)
    {
        var rows = new List<bool[]>(cellSize);

        for (int y = 0; y < cellSize; y++)
        {
            bool[] line = new bool[cellSize];

            for (int x = 0; x < cellSize; x++)
                line[x] = paints(indices[y, x]);

            rows.Add(line);
        }

        return rows;
    }

    /// <summary>
    /// Vuelve a montar la celda a partir de sus planos, como haría el VDP.
    /// </summary>
    /// <remarks>
    /// El OR de los colores de los planos que pintan cada pixel. Sirve para comprobar que lo
    /// descompuesto se ve como el original, que es lo único que de verdad importa de todo esto
    /// y lo que no se puede dar por bueno leyendo el código.
    /// </remarks>
    public static int[,] Compose(IReadOnlyList<SheetPlane> planes, int cellSize)
    {
        int[,] indices = new int[cellSize, cellSize];

        foreach (SheetPlane plane in planes)
        {
            for (int y = 0; y < cellSize; y++)
            {
                for (int x = 0; x < cellSize; x++)
                {
                    if (plane.Rows[y][x])
                        indices[y, x] |= plane.Color;
                }
            }
        }

        return indices;
    }
}
