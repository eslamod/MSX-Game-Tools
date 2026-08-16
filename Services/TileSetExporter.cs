using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Vuelca un juego de tiles a las dos tablas que espera el VDP, en dos ficheros: la de
/// patrones por un lado y la de colores por otro.
/// </summary>
/// <remarks>
/// <para>
/// 2048 bytes cada una: 256 patrones de ocho líneas y un byte por línea. En la de
/// patrones, la máscara de bits con la columna 0 en el bit más significativo; en la de
/// colores, el color de frente en el nibble alto y el de fondo en el bajo.
/// </para>
/// <para>
/// <b>Las dos tablas hay que replicarlas tres veces en VRAM.</b> En GRAPHIC 2 y 3 la
/// pantalla se parte en tres tercios, cada uno con su tabla de patrones y su tabla de
/// colores, o sea 6144 bytes de cada una. Aquí se exporta un solo juego porque la idea
/// es que un tile se vea igual esté en el tercio que esté; copiarlo tres veces es
/// trabajo del cargador del juego. Lo dice también la cabecera del .asm, que es donde
/// lo va a leer quien lo use.
/// </para>
/// </remarks>
public static class TileSetExporter
{
    /// <summary>Bytes de cada tabla: 256 tiles por 8 líneas.</summary>
    public const int TableBytes = TileSet.TileCount * Tile.Rows;

    /// <summary>Veces que hay que copiar cada tabla en VRAM, una por tercio de pantalla.</summary>
    public const int ScreenThirds = 3;

    private const int BytesPerLine = 8;

    public static byte[] PatternsToBinary(TileSet tileSet) => ToBinary(tileSet, row => row.PatternByte);

    public static byte[] ColorsToBinary(TileSet tileSet) => ToBinary(tileSet, row => row.ColorByte);

    public static string PatternsToAssembler(TileSet tileSet) => ToAssembler(
        tileSet,
        row => row.PatternByte,
        "patterns",
        "; Un byte por linea: la mascara de bits, con la columna 0 en el bit mas alto.");

    public static string ColorsToAssembler(TileSet tileSet) => ToAssembler(
        tileSet,
        row => row.ColorByte,
        "colors",
        "; Un byte por linea: color de frente en el nibble alto y de fondo en el bajo.");

    /// <summary>
    /// Un byte por tile con sus ocho banderas. 256 bytes, no 2048: esto es del tile, no de
    /// sus líneas.
    /// </summary>
    public static byte[] AttributesToBinary(TileSet tileSet) =>
        [.. tileSet.ListOfTiles.Select(tile => (byte)tile.Attributes)];

    /// <summary>
    /// La tabla de atributos, con los nombres puestos y las máscaras como constantes.
    /// </summary>
    /// <remarks>
    /// Las <c>equ</c> además de los comentarios porque son lo que se usa de verdad: un
    /// comentario hay que traducirlo a mano a un <c>bit 2, a</c> cada vez que se escribe
    /// código, y ahí es donde se cuelan los errores de un bit.
    /// </remarks>
    public static string AttributesToAssembler(TileSet tileSet)
    {
        var text = new StringBuilder();
        string label = SpriteBankExporter.LabelOf(tileSet.Name);

        text.AppendLine($"; Tile attributes - {tileSet.Name}");
        text.AppendLine($"; One byte per tile, {TileSet.TileCount} bytes. Bit 0 is attribute 0.");
        text.AppendLine($"; Size: {label}_attributes_end - {label}_attributes");
        text.AppendLine();

        foreach (int bit in tileSet.AttributeNames.Defined)
        {
            string name = SpriteBankExporter.LabelOf(tileSet.AttributeNames[bit]).ToUpperInvariant();

            text.AppendLine(
                $"{label}_attr_{name}:".PadRight(32)
                + $"equ %{Convert.ToString(1 << bit, 2).PadLeft(TileAttributeNames.Count, '0')}"
                + $"   ; bit {bit} - {tileSet.AttributeNames[bit]}");
        }

        text.AppendLine();
        text.AppendLine($"{label}_attributes:");

        AppendBytes(text, tileSet.ListOfTiles.Select(tile => (byte)tile.Attributes));

        text.AppendLine($"{label}_attributes_end:");

        return text.ToString();
    }

    private static byte[] ToBinary(TileSet tileSet, Func<TileRow, byte> byteOf)
    {
        byte[] bytes = new byte[TableBytes];
        int position = 0;

        foreach (Tile tile in tileSet.ListOfTiles)
        {
            foreach (TileRow row in tile.ArrayTileRows)
                bytes[position++] = byteOf(row);
        }

        return bytes;
    }

    private static string ToAssembler(TileSet tileSet, Func<TileRow, byte> byteOf, string suffix, string format)
    {
        var text = new StringBuilder();
        string label = SpriteBankExporter.LabelOf(tileSet.Name);

        text.AppendLine($"; Tile {suffix} table - {tileSet.Name}");
        text.AppendLine($"; {TileSet.TileCount} tiles of {Tile.Rows}x{TileRow.Columns}, {TableBytes} bytes");
        text.AppendLine(format);
        text.AppendLine($"; Copy this table {ScreenThirds} times in VRAM, one per screen third:");
        text.AppendLine("; in GRAPHIC 2 and 3 each third has its own table, and the same tiles in all");
        text.AppendLine("; three is what lets a tile look the same wherever the map puts it.");
        text.AppendLine($"; Size: {label}_{suffix}_end - {label}_{suffix}");
        text.AppendLine();
        text.AppendLine($"{label}_{suffix}:");

        for (int index = 0; index < tileSet.ListOfTiles.Count; index++)
        {
            text.AppendLine($"{label}_tile_{index}_{suffix}:");
            AppendBytes(text, tileSet.ListOfTiles[index].ArrayTileRows.Select(byteOf));
        }

        text.AppendLine($"{label}_{suffix}_end:");

        return text.ToString();
    }

    private static void AppendBytes(StringBuilder text, IEnumerable<byte> bytes)
    {
        byte[] all = [.. bytes];

        for (int start = 0; start < all.Length; start += BytesPerLine)
        {
            IEnumerable<string> line = all
                .Skip(start)
                .Take(BytesPerLine)
                .Select(value => $"{SpriteBankExporter.HexPrefix}{value:X2}");

            text.AppendLine($"    {SpriteBankExporter.DataDirective}  {string.Join(",", line)}");
        }
    }
}
