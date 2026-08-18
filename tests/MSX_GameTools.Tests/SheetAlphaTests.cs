using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El alfa de la hoja, en la lista de colores y elegido por defecto.
/// </summary>
/// <remarks>
/// Antes no salía: la cuenta de colores se saltaba los pixeles con alfa cero, así que en una
/// hoja con transparencia de verdad no había forma de elegirla y se cogía el color opaco más
/// usado. En un dibujo grande ése es parte del dibujo, y los sprites hechos de ese color
/// entraban en blanco.
/// </remarks>
public class SheetAlphaTests
{
    [AvaloniaFact]
    public void El_alfa_de_la_hoja_sale_el_primero_y_elegido()
    {
        // Fondo transparente y dos colores, uno de ellos mucho mas usado que el alfa.
        int[] pixels = new int[8 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unchecked((int)0xFFB95E51);

        pixels[0] = 0;
        pixels[1] = unchecked((int)0xFF00FF00);

        ImportSpriteSheetViewModel form = Form(pixels);

        // El primero aunque salga una vez y el rojizo sesenta y dos: una hoja con alfa lo trae
        // como fondo, y por cuenta acabaria el ultimo.
        Assert.True(form.SheetColors[0].IsAlpha);
        Assert.Same(form.SheetColors[0], form.Transparent);
    }

    /// <summary>Sin alfa en la hoja, la lista es la de siempre.</summary>
    [AvaloniaFact]
    public void Sin_alfa_manda_el_color_mas_usado()
    {
        int[] pixels = new int[8 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unchecked((int)0xFFB95E51);

        pixels[0] = unchecked((int)0xFF00FF00);

        ImportSpriteSheetViewModel form = Form(pixels);

        Assert.False(form.SheetColors[0].IsAlpha);
        Assert.Equal(Color.FromRgb(0xB9, 0x5E, 0x51), form.Transparent!.Color);
    }

    private static ImportSpriteSheetViewModel Form(int[] pixels) => new(
        new MainWindowViewModel(),
        "hoja.png",
        pixels,
        new PixelSize(8, 8),
        new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96)));
}
