using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MSX_SpritesEditor.Services;

/// <summary>
/// Leer y escribir imágenes como un array de pixeles en BGRA.
/// </summary>
/// <remarks>
/// Separado de la conversión a tiles para que aquella se pueda probar con arrays a mano,
/// sin escribir ficheros ni depender del decodificador.
/// </remarks>
public static class PngFile
{
    private static readonly Vector Dpi = new(96, 96);

    public static (int[] Pixels, PixelSize Size) Read(string path)
    {
        using var bitmap = new Bitmap(path);

        PixelSize size = bitmap.PixelSize;
        int[] pixels = new int[size.Width * size.Height];

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            bitmap.CopyPixels(
                new PixelRect(size),
                handle.AddrOfPinnedObject(),
                pixels.Length * sizeof(int),
                size.Width * sizeof(int));
        }
        finally
        {
            handle.Free();
        }

        return (pixels, size);
    }

    public static void Write(string path, int[] pixels, PixelSize size)
    {
        var bitmap = new WriteableBitmap(size, Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        using (ILockedFramebuffer buffer = bitmap.Lock())
        {
            for (int y = 0; y < size.Height; y++)
                Marshal.Copy(pixels, y * size.Width, buffer.Address + (y * buffer.RowBytes), size.Width);
        }

        bitmap.Save(path);
    }
}
