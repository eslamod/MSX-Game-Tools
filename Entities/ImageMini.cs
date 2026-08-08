using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Miniatura de previsualización de un sprite.
/// </summary>
/// <remarks>
/// La versión WPF usaba <c>System.Drawing.Bitmap</c>, que en .NET moderno sólo existe
/// en Windows y está fuera de soporte, y además serializaba el bitmap a un MemoryStream
/// en cada lectura de la propiedad. Aquí los pixeles viven en un array y el
/// <see cref="WriteableBitmap"/> se construye sólo cuando algo ha cambiado.
/// </remarks>
public sealed class ImageMini : ObservableObject
{
    public enum ImagePreviewType
    {
        ImagePreview8x8 = 0,
        ImagePreview16x16,
    }

    private static readonly Vector Dpi = new(96, 96);

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _pixels; // BGRA, un int por pixel
    private WriteableBitmap? _cache;

    public ImageMini(ImagePreviewType type)
    {
        _width = _height = type == ImagePreviewType.ImagePreview8x8 ? 8 : 16;
        _pixels = new int[_width * _height];

        // Fondo negro con un patrón de puntos blancos, igual que la versión WPF.
        // (Allí el bucle iba fijo hasta 15 y reventaba con las miniaturas de 8x8.)
        Array.Fill(_pixels, ToBgra(Colors.Black));
        for (int y = 0; y < _height; y += 2)
            for (int x = 0; x < _width; x += 2)
                _pixels[(y * _width) + x] = ToBgra(Colors.White);
    }

    public int Width => _width;

    public int Height => _height;

    public void SetPixel(int x, int y, Color color)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
            return;

        int value = ToBgra(color);
        int offset = (y * _width) + x;
        if (_pixels[offset] == value)
            return;

        _pixels[offset] = value;
        _cache = null;
        OnPropertyChanged(nameof(SpritePreview));
    }

    /// <summary>Imagen lista para enlazar a <c>Image.Source</c>.</summary>
    public IImage SpritePreview => _cache ??= CreateBitmap();

    private WriteableBitmap CreateBitmap()
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(_width, _height),
            Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var buffer = bitmap.Lock();
        for (int y = 0; y < _height; y++)
            Marshal.Copy(_pixels, y * _width, buffer.Address + (y * buffer.RowBytes), _width);

        return bitmap;
    }

    private static int ToBgra(Color c) => (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
}
