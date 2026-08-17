using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// La tabla de desplazamiento, para el ensamblador.
/// </summary>
/// <remarks>
/// Un byte por tile, en el orden de los tiles, con el código de lo que entra por su borde
/// derecho. Es lo que come la rutina que va construyendo las ocho copias desplazadas del juego
/// de tiles: recorre la tabla a la vez que los patrones y por cada uno mira si mete ceros, unos
/// o el bit que sale del tile siguiente.
/// </remarks>
public static class MapShiftExporter
{
    private const int BytesPerLine = 8;

    /// <summary>Un byte por tile, del 0 al 255.</summary>
    public static byte[] ToBinary(MapShiftReport report) =>
        [.. report.Table.Select(fill => (byte)fill)];

    /// <summary>
    /// La tabla con los códigos explicados arriba.
    /// </summary>
    /// <remarks>
    /// Con las <c>equ</c> por lo mismo que en los atributos: un comentario hay que traducirlo a
    /// mano cada vez que se escribe código, y ahí es donde se cuela el número cambiado.
    /// </remarks>
    public static string ToAssembler(MapShiftReport report, string name)
    {
        var text = new StringBuilder();
        string label = SpriteBankExporter.LabelOf(name);

        text.AppendLine($"; Shift table - {name}");
        text.AppendLine($"; One byte per tile, {TileSet.TileCount} bytes: what enters from the right");
        text.AppendLine("; when the tile set is shifted one pixel to the left.");
        text.AppendLine($"; Size: {label}_shift_end - {label}_shift");
        text.AppendLine();

        text.AppendLine($"{label}_shift_next:".PadRight(32) + "equ 0   ; the next tile's left column");
        text.AppendLine($"{label}_shift_zeros:".PadRight(32) + "equ 1");
        text.AppendLine($"{label}_shift_ones:".PadRight(32) + "equ 2");
        text.AppendLine();

        text.AppendLine($"{label}_shift:");

        byte[] bytes = ToBinary(report);

        for (int start = 0; start < bytes.Length; start += BytesPerLine)
        {
            IEnumerable<string> line = bytes.Skip(start).Take(BytesPerLine).Select(value => $"{value}");

            text.AppendLine(
                $"    {SpriteBankExporter.DataDirective}  {string.Join(",", line)}"
                + $"   ; {start}-{Math.Min(start + BytesPerLine, bytes.Length) - 1}");
        }

        text.AppendLine($"{label}_shift_end:");

        return text.ToString();
    }
}
