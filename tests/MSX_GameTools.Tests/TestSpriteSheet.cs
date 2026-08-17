using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Tests;

/// <summary>
/// Un formulario de traer una hoja, hecho sin pasar por disco.
/// </summary>
/// <remarks>
/// El comando de verdad pide un fichero y lo decodifica. Para medir el panel o comprobar lo que
/// enseña no hace falta nada de eso, y montar un png de mentira en cada prueba que lo necesite
/// sería repetir lo mismo en varios sitios.
/// </remarks>
internal static class TestSpriteSheet
{
    private const int Cell = 8;

    public static ImportSpriteSheetViewModel Form(MainWindowViewModel main, int columns = 4)
    {
        var size = new PixelSize(columns * Cell, Cell * 2);
        int[] pixels = new int[size.Width * size.Height];

        // Fondo verde con un aspa blanca y negra en cada celda: tres colores, que es lo que
        // hace que el informe tenga algo que decir.
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                Color color = (x % Cell) == (y % Cell)
                    ? Colors.White
                    : (x % Cell) == Cell - 1 - (y % Cell) ? Colors.Black : Colors.Lime;

                pixels[(y * size.Width) + x] =
                    unchecked((int)0xFF000000) | (color.R << 16) | (color.G << 8) | color.B;
            }
        }

        var source = new WriteableBitmap(
            size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        return new ImportSpriteSheetViewModel(main, "bichos.png", pixels, size, source)
        {
            CellSize = Cell,
        };
    }
}
