using Avalonia;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Reparte una imagen de referencia en las celdas que se ofrecen como fondo.
/// </summary>
/// <remarks>
/// Separado de la carga a propósito: aquí no hay ficheros ni decodificación, sólo la
/// geometría, que es lo único que tiene reglas que puedan salir mal.
/// </remarks>
public static class ReferenceImageSlicer
{
    /// <summary>
    /// Hasta este tamaño la imagen entra de una pieza y no se pregunta nada. Es el lado
    /// del lienzo del grupo: una imagen que ya cabe ahí no gana nada troceándose.
    /// </summary>
    public const int SingleTileMax = SpriteGroupRenderer.PreviewSize;

    /// <summary>
    /// Celdas en las que se parte una imagen, de izquierda a derecha y de arriba abajo.
    /// </summary>
    /// <param name="size">Tamaño de la imagen en pixeles.</param>
    /// <param name="cellSize">
    /// Lado de la celda. Se ignora si la imagen cabe entera en <see cref="SingleTileMax"/>.
    /// </param>
    /// <remarks>
    /// Cuando el tamaño no es múltiplo del de la celda, las celdas del borde derecho e
    /// inferior salen recortadas en vez de descartarse: en una hoja de sprites el último
    /// trozo suele ser justo el que falta.
    /// </remarks>
    public static IReadOnlyList<PixelRect> Slice(PixelSize size, int cellSize)
    {
        if (size.Width <= 0 || size.Height <= 0)
            return [];

        if (size.Width <= SingleTileMax && size.Height <= SingleTileMax)
            return [new PixelRect(0, 0, size.Width, size.Height)];

        ArgumentOutOfRangeException.ThrowIfLessThan(cellSize, 1);

        var cells = new List<PixelRect>();

        for (int y = 0; y < size.Height; y += cellSize)
        {
            for (int x = 0; x < size.Width; x += cellSize)
            {
                cells.Add(new PixelRect(
                    x,
                    y,
                    Math.Min(cellSize, size.Width - x),
                    Math.Min(cellSize, size.Height - y)));
            }
        }

        return cells;
    }
}
