using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Vuelca una paleta al formato que espera el registro de paleta del V9938.
/// </summary>
/// <remarks>
/// <para>
/// Dos bytes por color y dieciséis colores: 32 en total. El primer byte lleva el rojo en
/// el nibble alto y el azul en el bajo, y el segundo lleva el verde. Cada componente son
/// tres bits, que es justamente lo que guarda la paleta del editor, así que no hay
/// conversión de por medio.
/// </para>
/// <para>
/// Se carga poniendo el registro R#16 a cero y escribiendo los 32 bytes seguidos al
/// puerto 0x9A: el índice va avanzando solo. En un MSX1 no hay paleta que cargar, así
/// que quien use esto tiene que mirar antes la versión de la máquina.
/// </para>
/// </remarks>
public static class PaletteExporter
{
    /// <summary>Dos bytes por cada uno de los 16 colores.</summary>
    public const int Bytes = ColorPalette.Size * 2;

    /// <summary>Puerto por el que se escriben los colores, uno detrás de otro.</summary>
    public const int VdpPalettePort = 0x9A;

    /// <summary>Registro con el índice del color que se va a escribir.</summary>
    public const int VdpPaletteRegister = 16;

    private const int BytesPerLine = 8;

    public static byte[] ToBinary(ColorPalette palette)
    {
        byte[] bytes = new byte[Bytes];

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            PaletteColor color = palette[index];

            bytes[index * 2] = (byte)(((color.Red & 0x07) << 4) | (color.Blue & 0x07));
            bytes[(index * 2) + 1] = (byte)(color.Green & 0x07);
        }

        return bytes;
    }

    public static string ToAssembler(ColorPalette palette, AsmStyle? style = null)
    {
        string data = AsmStyle.Of(style?.Data).Data;
        var text = new StringBuilder();
        string label = SpriteBankExporter.LabelOf(palette.Name);
        byte[] bytes = ToBinary(palette);

        text.AppendLine($"; Palette - {palette.Name}");
        text.AppendLine($"; {ColorPalette.Size} colours, 2 bytes each: 0RRR0BBB then 00000GGG");
        text.AppendLine($"; Load with R#{VdpPaletteRegister} = 0 and then the {Bytes} bytes to port "
                        + $"{SpriteBankExporter.HexPrefix}{VdpPalettePort:X2}; the index auto-increments.");
        text.AppendLine("; MSX1 has no palette: check the machine version before loading it.");
        text.AppendLine($"; Size: {label}_palette_end - {label}_palette");
        text.AppendLine();
        text.AppendLine($"{label}_palette:");

        for (int start = 0; start < bytes.Length; start += BytesPerLine)
        {
            IEnumerable<string> line = bytes
                .Skip(start)
                .Take(BytesPerLine)
                .Select(value => $"{SpriteBankExporter.HexPrefix}{value:X2}");

            text.AppendLine($"    {data}  {string.Join(",", line)}");
        }

        text.AppendLine($"{label}_palette_end:");

        return text.ToString();
    }
}
