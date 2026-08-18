using Avalonia;
using Avalonia.Media;

namespace MSX_GameTools.Services;

/// <summary>
/// Leer los pixeles de una hoja de sprites: lo que comparten el análisis y la descomposición.
/// </summary>
/// <remarks>
/// Las dos etapas recorren la misma hoja con el mismo criterio de qué es transparente y de qué
/// número de color es cada pixel. Teniéndolo en dos sitios, cambiar el criterio en uno dejaría
/// un informe que no se parece a lo que luego se trae.
/// </remarks>
internal static class SpriteSheetPixels
{
    /// <summary>El número de color de un pixel de la hoja, o -1 si no pinta nada.</summary>
    public static int ColorAt(
        int[] pixels,
        PixelSize size,
        int x,
        int y,
        Color? transparent,
        IReadOnlyList<Color> colors)
    {
        int pixel = pixels[(y * size.Width) + x];

        return IsClear(pixel, transparent) ? -1 : IndexOf(colors, FromBgra(pixel));
    }

    /// <summary>
    /// Si un pixel no pinta nada.
    /// </summary>
    /// <remarks>
    /// El alfa a cero siempre, y además el color que se haya elegido como transparente: muchas
    /// hojas vienen con el fondo de un color liso en vez de con alfa, y sin esto ese fondo se
    /// llevaría un índice de paleta y un plano entero para nada.
    /// </remarks>
    public static bool IsClear(int bgra, Color? transparent) =>
        (uint)bgra >> 24 == 0 || (transparent is { } clear && FromBgra(bgra) == clear);

    /// <summary>Si una celda de la hoja no trae ni un pixel que pintar.</summary>
    /// <remarks>
    /// Una celda así no se lleva ningún patrón cuando se pide saltarse las vacías, y no
    /// hacen falta sus colores para saberlo: basta con que ningún pixel sea transparente.
    /// </remarks>
    public static bool IsBlankCell(
        int[] pixels, PixelSize size, int cellSize, Color? transparent, int column, int row)
    {
        int left = column * cellSize;
        int top = row * cellSize;

        for (int y = 0; y < cellSize; y++)
        {
            for (int x = 0; x < cellSize; x++)
            {
                if (!IsClear(pixels[((top + y) * size.Width) + left + x], transparent))
                    return false;
            }
        }

        return true;
    }

    public static Color FromBgra(int bgra) => Color.FromRgb(
        (byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF));

    /// <summary>Qué número de color es, o -1. A mano porque la lista es de sólo lectura.</summary>
    public static int IndexOf(IReadOnlyList<Color> colors, Color color)
    {
        for (int index = 0; index < colors.Count; index++)
        {
            if (colors[index] == color)
                return index;
        }

        return -1;
    }
}
