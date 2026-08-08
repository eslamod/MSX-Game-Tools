using Avalonia.Media;
using MSX_SpritesEditor.Entities;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// La paleta estándar del MSX (TMS9918), comprobada en los dos formatos: las
/// componentes nativas de 3 bits y su expansión a 8 bits para pantalla.
/// </summary>
public class ColorPaletteTests
{
    private static readonly ColorPalette Palette = ColorPalette.CreateMsxStandard();

    [Fact]
    public void La_paleta_tiene_16_colores_indexados_de_0_a_15()
    {
        Assert.Equal(16, Palette.Count);
        Assert.Equal(16, Palette.Colors.Count);

        for (int i = 0; i < Palette.Count; i++)
            Assert.Equal(i, Palette.Colors[i].Index);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(9, "9")]
    [InlineData(10, "A")]
    [InlineData(15, "F")]
    public void El_hex_es_el_indice_en_un_digito(int index, string expected)
        => Assert.Equal(expected, Palette[index].Hex);

    [Fact]
    public void Solo_el_color_0_es_transparente()
    {
        Assert.True(Palette[0].IsTransparent);
        Assert.Equal(0, Palette[0].Color.A);

        for (int i = 1; i < Palette.Count; i++)
        {
            Assert.False(Palette[i].IsTransparent);
            Assert.Equal(255, Palette[i].Color.A);
        }
    }

    // Componentes nativas del VDP, 3 bits por canal.
    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(2, 1, 6, 1)]
    [InlineData(3, 3, 7, 3)]
    [InlineData(4, 1, 1, 7)]
    [InlineData(5, 2, 3, 7)]
    [InlineData(6, 5, 1, 1)]
    [InlineData(7, 2, 6, 7)]
    [InlineData(8, 7, 1, 1)]
    [InlineData(9, 7, 3, 3)]
    [InlineData(10, 6, 6, 1)]
    [InlineData(11, 6, 6, 4)]
    [InlineData(12, 1, 4, 1)]
    [InlineData(13, 6, 2, 5)]
    [InlineData(14, 5, 5, 5)]
    [InlineData(15, 7, 7, 7)]
    public void Las_componentes_nativas_son_las_del_msx(int index, int r, int g, int b)
    {
        PaletteColor color = Palette[index];

        Assert.Equal(r, color.Red);
        Assert.Equal(g, color.Green);
        Assert.Equal(b, color.Blue);
    }

    // La expansión canónica a 8 bits. Ojo con los 73: truncar en vez de redondear
    // daba 72, que es el valor que arrastraba la versión WPF.
    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(2, 36, 219, 36)]
    [InlineData(3, 109, 255, 109)]
    [InlineData(4, 36, 36, 255)]
    [InlineData(5, 73, 109, 255)]
    [InlineData(6, 182, 36, 36)]
    [InlineData(7, 73, 219, 255)]
    [InlineData(8, 255, 36, 36)]
    [InlineData(9, 255, 109, 109)]
    [InlineData(10, 219, 219, 36)]
    [InlineData(11, 219, 219, 146)]
    [InlineData(12, 36, 146, 36)]
    [InlineData(13, 219, 73, 182)]
    [InlineData(14, 182, 182, 182)]
    [InlineData(15, 255, 255, 255)]
    public void La_expansion_a_8_bits_es_la_canonica(int index, byte r, byte g, byte b)
    {
        Color color = Palette[index].Color;

        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void El_fondo_se_elige_entre_los_colores_1_a_F()
    {
        Assert.Equal(15, Palette.BackgroundChoices.Count);
        Assert.Equal(1, Palette.BackgroundChoices[0].Index);
        Assert.Equal(15, Palette.BackgroundChoices[^1].Index);
        Assert.DoesNotContain(Palette.BackgroundChoices, c => c.IsTransparent);
    }

    [Fact]
    public void Los_brushes_se_reutilizan_entre_llamadas()
    {
        // El lienzo son 256 celdas y se repinta entero a menudo.
        Assert.Same(Palette.GetBrush(5), Palette.GetBrush(5));
        Assert.Same(Palette[5].Brush, Palette.GetBrush(5));
    }
}
