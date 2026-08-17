using Avalonia;
using Avalonia.Media;

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
        int row)
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

        for (int bit = 0; bit < SpritePlaneAssignment.MaxPlanes; bit++)
        {
            int color = 1 << bit;

            // Sólo los bits que la celda usa: un plano en blanco gastaría un hueco del banco
            // y un sprite de los que caben en la línea de barrido, para no pintar nada.
            if ((used & color) == 0)
                continue;

            var rows = new List<bool[]>(cellSize);

            for (int y = 0; y < cellSize; y++)
            {
                bool[] line = new bool[cellSize];

                for (int x = 0; x < cellSize; x++)
                    line[x] = (indices[y, x] & color) != 0;

                rows.Add(line);
            }

            planes.Add(new SheetPlane(color, rows));
        }

        return planes;
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
