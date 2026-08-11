using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lee los pixeles reales de una miniatura. Requiere Skia: con el dibujado simulado
/// de headless, WriteableBitmap.Lock() no devuelve memoria persistente.
/// </summary>
internal static class PixelReader
{
    public static int[] Read(ImageMini mini)
    {
        var bitmap = (WriteableBitmap)mini.SpritePreview;
        int[] pixels = new int[mini.Width * mini.Height];

        using ILockedFramebuffer buffer = bitmap.Lock();
        for (int y = 0; y < mini.Height; y++)
            Marshal.Copy(buffer.Address + (y * buffer.RowBytes), pixels, y * mini.Width, mini.Width);

        return pixels;
    }

    public static int At(ImageMini mini, int x, int y) => Read(mini)[(y * mini.Width) + x];

    public static int Bgra(Color c) => (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
}
