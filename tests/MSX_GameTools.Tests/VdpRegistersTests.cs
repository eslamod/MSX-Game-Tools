using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Los registros del VDP volcados por openMSX, que dicen dónde están las tablas.
/// </summary>
public class VdpRegistersTests
{
    /// <summary>
    /// Un volcado de verdad: Knightmare, que no usa las direcciones de siempre.
    /// </summary>
    /// <remarks>
    /// Son los bytes tal cual del fichero que exporta openMSX. Vale más que un caso inventado
    /// porque tiene los patrones y los colores al revés de lo corriente —patrones en
    /// <c>2000H</c>, colores en <c>0000H</c>—, que es justo por lo que estos registros hacen
    /// falta: con las direcciones por defecto ese juego se importaría del revés.
    /// </remarks>
    [Fact]
    public void Un_volcado_de_verdad_dice_donde_esta_todo()
    {
        byte[] dump = Dump(0x02, 0xE2, 0x0E, 0x7F, 0x07, 0x76, 0x03, 0xE0);

        VdpRegisters.Layout layout = VdpRegisters.Read(dump)!;

        Assert.Equal(VdpRegisters.ScreenMode.Graphic2, layout.Mode);
        Assert.True(layout.BigSprites);

        Assert.Equal(0x2000, layout.Patterns);
        Assert.Equal(0x0000, layout.Colors);
        Assert.Equal(0x3800, layout.Names);
        Assert.Equal(0x1800, layout.SpritePatterns);
        Assert.Equal(0x3B00, layout.SpriteAttributes);
    }

    /// <summary>
    /// Y los de la ROM de prueba del proyecto, que sí usa las de siempre.
    /// </summary>
    /// <remarks>
    /// El otro extremo del mismo bit: allí <c>R#4 = 0x03</c> deja los patrones en
    /// <c>0000H</c> porque el bit 2 está a cero, y <c>R#3 = 0xFF</c> los colores en
    /// <c>2000H</c> porque el 7 está puesto. Los bits bajos son máscara y no dirección; con
    /// los dos casos, leerlos como dirección no cuela.
    /// </remarks>
    [Fact]
    public void Y_los_de_la_rom_de_prueba_dan_las_de_siempre()
    {
        byte[] dump = Dump(0x02, 0xE0, 0x06, 0xFF, 0x03, 0x36, 0x07, 0x01);

        VdpRegisters.Layout layout = VdpRegisters.Read(dump)!;

        Assert.Equal(VdpRegisters.ScreenMode.Graphic2, layout.Mode);

        Assert.Equal(0x0000, layout.Patterns);
        Assert.Equal(0x2000, layout.Colors);
        Assert.Equal(0x1800, layout.Names);
        Assert.Equal(0x3800, layout.SpritePatterns);
        Assert.Equal(0x1B00, layout.SpriteAttributes);
    }

    /// <summary>El modo sale de cinco bits repartidos entre R#0 y R#1.</summary>
    [Theory]
    [InlineData(0x00, 0x00, VdpRegisters.ScreenMode.Graphic1)]
    [InlineData(0x02, 0x00, VdpRegisters.ScreenMode.Graphic2)]
    [InlineData(0x04, 0x00, VdpRegisters.ScreenMode.Graphic3)]
    [InlineData(0x00, 0x10, VdpRegisters.ScreenMode.Other)]     // M1: texto
    [InlineData(0x00, 0x08, VdpRegisters.ScreenMode.Other)]     // M2: multicolor
    [InlineData(0x08, 0x00, VdpRegisters.ScreenMode.Other)]     // M5: los de mapa de bits
    public void El_modo_sale_de_r0_y_r1(byte zero, byte one, VdpRegisters.ScreenMode expected)
    {
        Assert.Equal(expected, VdpRegisters.Read(Dump(zero, one))!.Mode);
    }

    /// <summary>El tamaño de los sprites es el bit 1 de R#1.</summary>
    [Theory]
    [InlineData(0x00, false)]
    [InlineData(0x02, true)]
    public void El_tamano_de_sprite_es_un_bit_de_r1(byte one, bool expected)
    {
        Assert.Equal(expected, VdpRegisters.Read(Dump(0x02, one))!.BigSprites);
    }

    /// <summary>Un fichero corto no es un volcado de registros.</summary>
    /// <remarks>
    /// Aceptarlo a medias sería sacar direcciones de lo que hubiera detrás, que es peor que
    /// decir que no: saldría una importación con pinta de buena y colocada en cualquier parte.
    /// </remarks>
    [Fact]
    public void Un_fichero_corto_no_vale()
    {
        Assert.Null(VdpRegisters.Read(new byte[VdpRegisters.Count - 1]));
    }

    /// <summary>
    /// Los bits altos de R#10 y R#11 suben la tabla por encima de los 16 KB.
    /// </summary>
    /// <remarks>
    /// openMSX vuelca 128 KB aunque la máquina sea un MSX1, así que hay sitio de sobra ahí
    /// arriba y hay juegos de MSX2 que lo usan. Sin sumar esos bits, la tabla se leería en los
    /// primeros 16 KB, que es donde no está.
    /// </remarks>
    [Fact]
    public void Los_bits_altos_de_r10_y_r11_cuentan()
    {
        byte[] dump = Dump(0x04, 0xE2, 0x0E, 0xFF, 0x03, 0x76, 0x03, 0x00);

        dump[10] = 0x02;        // colores tres bits mas arriba
        dump[11] = 0x01;        // y atributos dos

        VdpRegisters.Layout layout = VdpRegisters.Read(dump)!;

        Assert.Equal((0x02 << 14) | 0x2000, layout.Colors);
        Assert.Equal((0x01 << 15) | 0x3B00, layout.SpriteAttributes);
    }

    private static byte[] Dump(params byte[] first)
    {
        var dump = new byte[VdpRegisters.Count];

        first.CopyTo(dump, 0);

        return dump;
    }
}
