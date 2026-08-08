using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_SpritesEditor.Entities;
using Xunit;

namespace MSX_SpritesEditor.Tests;

public class ImageMiniTests
{
    [AvaloniaTheory]
    [InlineData(ImageMini.ImagePreviewType.ImagePreview8x8, 8)]
    [InlineData(ImageMini.ImagePreviewType.ImagePreview16x16, 16)]
    public void Una_miniatura_recien_creada_es_negra(ImageMini.ImagePreviewType type, int expectedSize)
    {
        var mini = new ImageMini(type);

        Assert.Equal(expectedSize, mini.Width);
        Assert.Equal(expectedSize, mini.Height);

        // La versión WPF sembraba blancos en las coordenadas pares, así que un sprite
        // vacío parecía tener contenido.
        Assert.All(ReadPixels(mini), pixel => Assert.Equal(Bgra(Colors.Black), pixel));
    }

    [AvaloniaTheory]
    [InlineData(ImageMini.ImagePreviewType.ImagePreview8x8)]
    [InlineData(ImageMini.ImagePreviewType.ImagePreview16x16)]
    public void SetPixel_pinta_exactamente_la_posicion_indicada(ImageMini.ImagePreviewType type)
    {
        var mini = new ImageMini(type);

        mini.SetPixel(3, 4, Colors.Red);

        int[] pixels = ReadPixels(mini);
        Assert.Equal(Bgra(Colors.Red), pixels[(4 * mini.Width) + 3]);
        Assert.Equal(1, pixels.Count(p => p == Bgra(Colors.Red)));
    }

    [AvaloniaFact]
    public void SetPixel_fuera_de_rango_se_ignora_sin_lanzar()
    {
        var mini = new ImageMini(ImageMini.ImagePreviewType.ImagePreview8x8);

        mini.SetPixel(-1, 0, Colors.Red);
        mini.SetPixel(0, -1, Colors.Red);
        mini.SetPixel(8, 0, Colors.Red);
        mini.SetPixel(0, 8, Colors.Red);

        Assert.All(ReadPixels(mini), pixel => Assert.Equal(Bgra(Colors.Black), pixel));
    }

    [AvaloniaFact]
    public void SpritePreview_se_regenera_al_cambiar_un_pixel()
    {
        var mini = new ImageMini(ImageMini.ImagePreviewType.ImagePreview16x16);

        object before = mini.SpritePreview;
        Assert.Same(before, mini.SpritePreview); // cacheada mientras nada cambie

        mini.SetPixel(0, 0, Colors.Lime);

        Assert.NotSame(before, mini.SpritePreview);
    }

    private static int Bgra(Color c) => (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;

    private static int[] ReadPixels(ImageMini mini)
    {
        var bitmap = (WriteableBitmap)mini.SpritePreview;
        int[] pixels = new int[mini.Width * mini.Height];

        using ILockedFramebuffer buffer = bitmap.Lock();
        for (int y = 0; y < mini.Height; y++)
            Marshal.Copy(buffer.Address + (y * buffer.RowBytes), pixels, y * mini.Width, mini.Width);

        return pixels;
    }
}
