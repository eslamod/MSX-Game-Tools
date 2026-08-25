using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Lo que dicen los registros del VDP volcados por openMSX.
/// </summary>
/// <remarks>
/// <para>
/// El volcado son los bytes crudos, uno por registro y por su número. openMSX los escribe
/// todos —64— aunque la máquina sea un MSX1 y los de más arriba no se usen.
/// </para>
/// <para>
/// <b>Para qué.</b> Un volcado de VRAM a secas obliga a saber dónde puso sus tablas la ROM que
/// lo generó, y no todas usan las de siempre: en un volcado de Knightmare los patrones están
/// en <c>2000H</c> y los colores en <c>0000H</c>, justo al revés de lo corriente. Los
/// registros lo dicen sin preguntar, y de paso dicen el modo y el tamaño de los sprites.
/// </para>
/// <para>
/// <b>Los bits altos están en otro sitio.</b> En el V9938 la dirección no cabe en un solo
/// registro: R#10 lleva los tres bits de arriba de la tabla de colores y R#11 los dos de la de
/// atributos. openMSX vuelca 128 KB de VRAM aunque la máquina sea un MSX1, así que una tabla
/// puede estar perfectamente por encima de los 16 KB y hay que sumarlos.
/// </para>
/// <para>
/// <b>Los bits bajos no son dirección.</b> En GRAPHIC 2 y 3, de R#4 sólo el bit 2 dice dónde
/// empieza la tabla de patrones y de R#3 sólo el bit 7 dice dónde la de colores: los demás son
/// una máscara sobre los tercios de la pantalla. Por eso la base sólo puede ser
/// <c>0000H</c> o <c>2000H</c>, y por eso un <c>R#4 = 0x07</c> da <c>2000H</c> y no
/// <c>3800H</c>.
/// </para>
/// </remarks>
public static class VdpRegisters
{
    /// <summary>Los que escribe openMSX, aunque de un MSX1 sólo signifiquen los primeros.</summary>
    public const int Count = 64;

    /// <summary>Modos de pantalla que sabe leer el importador.</summary>
    public enum ScreenMode
    {
        /// <summary>Screen 1: 32 bytes de color para los 256 tiles.</summary>
        Graphic1,

        /// <summary>Screen 2: dos colores por línea y tres tercios.</summary>
        Graphic2,

        /// <summary>Screen 4: como el 2 para los tiles, y sprites de modo 2.</summary>
        Graphic3,

        /// <summary>Cualquier otro: texto, multicolor, los de mapa de bits del V9938.</summary>
        Other,
    }

    /// <summary>Lo que hace falta saber para leer un volcado de VRAM.</summary>
    public sealed record Layout(
        ScreenMode Mode,
        bool BigSprites,
        int Patterns,
        int Colors,
        int Names,
        int SpritePatterns,
        int SpriteAttributes);

    /// <summary>
    /// Lee el volcado. <c>null</c> si no tiene pinta de serlo.
    /// </summary>
    /// <remarks>
    /// Se pide el fichero entero y no sólo los primeros bytes: uno más corto no es un volcado
    /// de registros, y aceptarlo a medias sería inventarse direcciones con basura.
    /// </remarks>
    public static Layout? Read(byte[] bytes)
    {
        if (bytes.Length < Count)
            return null;

        ScreenMode mode = ModeOf(bytes);

        return new Layout(
            mode,
            BigSprites: (bytes[1] & 0x02) != 0,
            Patterns: PatternsOf(bytes, mode),
            Colors: ColorsOf(bytes, mode),
            Names: bytes[2] * 0x400,
            SpritePatterns: bytes[6] * 0x800,
            SpriteAttributes: ((bytes[11] & 0x03) << 15) | (bytes[5] * 0x80));
    }

    /// <summary>El juego de tiles que le corresponde, para el importador.</summary>
    public static TileSet.GraphicMode TileModeOf(ScreenMode mode) =>
        mode == ScreenMode.Graphic1 ? TileSet.GraphicMode.Graphic1 : TileSet.GraphicMode.Graphic2;

    /// <summary>
    /// El modo, por los cinco bits repartidos entre R#0 y R#1.
    /// </summary>
    /// <remarks>
    /// M1 y M2 están en R#1 —bits 4 y 3— y M3, M4 y M5 en R#0 —bits 1, 2 y 3—, que es como los
    /// reparte el manual del V9938 por compatibilidad con el TMS9918.
    /// </remarks>
    private static ScreenMode ModeOf(byte[] bytes)
    {
        bool m1 = (bytes[1] & 0x10) != 0;
        bool m2 = (bytes[1] & 0x08) != 0;
        bool m3 = (bytes[0] & 0x02) != 0;
        bool m4 = (bytes[0] & 0x04) != 0;
        bool m5 = (bytes[0] & 0x08) != 0;

        if (m1 || m2 || m5)
            return ScreenMode.Other;

        return (m4, m3) switch
        {
            (false, false) => ScreenMode.Graphic1,
            (false, true) => ScreenMode.Graphic2,
            (true, false) => ScreenMode.Graphic3,
            _ => ScreenMode.Other,
        };
    }

    /// <inheritdoc cref="VdpRegisters"/>
    private static int PatternsOf(byte[] bytes, ScreenMode mode) => mode switch
    {
        ScreenMode.Graphic2 or ScreenMode.Graphic3 => (bytes[4] & 0x04) * 0x800,
        _ => bytes[4] * 0x800,
    };

    /// <inheritdoc cref="VdpRegisters"/>
    private static int ColorsOf(byte[] bytes, ScreenMode mode)
    {
        int high = (bytes[10] & 0x07) << 14;

        return high | (mode is ScreenMode.Graphic2 or ScreenMode.Graphic3
            ? (bytes[3] & 0x80) * 0x40
            : bytes[3] * 0x40);
    }
}
