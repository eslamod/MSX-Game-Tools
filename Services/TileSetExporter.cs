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

    /// <summary>
    /// La tabla de colores: 2048 bytes en GRAPHIC 2 y 32 en GRAPHIC 1.
    /// </summary>
    /// <remarks>
    /// No es la misma tabla más corta, es otra tabla: en GRAPHIC 2 hay un byte por línea de
    /// cada tile y en GRAPHIC 1 uno por cada ocho tiles. Es la diferencia que define el modo,
    /// y es lo que hace que un tile de screen 1 no pueda tener dos colores por línea.
    /// </remarks>
    public static byte[] ColorsToBinary(TileSet tileSet) => tileSet.IsGraphic1
        ? [.. tileSet.ColorGroups.Select(group => group.ColorByte)]
        : ToBinary(tileSet, row => row.ColorByte);

    public static string PatternsToAssembler(TileSet tileSet) => ToAssembler(
        tileSet,
        row => row.PatternByte,
        "patterns",
        "; Un byte por linea: la mascara de bits, con la columna 0 en el bit mas alto.");

    public static string ColorsToAssembler(TileSet tileSet) => tileSet.IsGraphic1
        ? GroupColorsToAssembler(tileSet)
        : ToAssembler(
            tileSet,
            row => row.ColorByte,
            "colors",
            "; Un byte por linea: color de frente en el nibble alto y de fondo en el bajo.");

    /// <summary>
    /// Los 32 bytes de color de GRAPHIC 1, con el rango de tiles de cada uno al lado.
    /// </summary>
    /// <remarks>
    /// El rango va en el comentario porque es la única forma de leer esta tabla: el byte 3 no
    /// dice por sí solo que pinta los tiles 24 a 31, y equivocarse de grupo repinta ocho tiles
    /// que estaban bien.
    /// </remarks>
    private static string GroupColorsToAssembler(TileSet tileSet)
    {
        var text = new StringBuilder();
        string label = SpriteBankExporter.LabelOf(tileSet.Name);

        text.AppendLine($"; Tile colour table - {tileSet.Name} (GRAPHIC 1)");
        text.AppendLine(
            $"; One byte per group of {TileSet.ColorGroupSize} tiles, {TileSet.ColorGroupCount} bytes.");
        text.AppendLine("; Foreground in the high nibble, background in the low one.");
        text.AppendLine("; In GRAPHIC 1 there is a single colour table for the whole screen.");
        text.AppendLine($"; Size: {label}_colors_end - {label}_colors");
        text.AppendLine();
        text.AppendLine($"{label}_colors:");

        foreach (TileColorGroup group in tileSet.ColorGroups)
        {
            text.AppendLine(
                $"    {SpriteBankExporter.DataDirective}  {SpriteBankExporter.HexOf(group.ColorByte)}".PadRight(32)
                + $"; tiles {group.Range}");
        }

        text.AppendLine($"{label}_colors_end:");

        return text.ToString();
    }

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

        // Los tres tercios son de GRAPHIC 2 y 3. En GRAPHIC 1 hay una sola tabla para toda la
        // pantalla, y decirle a alguien que la copie tres veces son 4 KB de VRAM tirados.
        if (tileSet.IsGraphic1)
        {
            text.AppendLine("; In GRAPHIC 1 there is a single pattern table for the whole screen:");
            text.AppendLine("; copy it once.");
        }
        else
        {
            text.AppendLine($"; Copy this table {ScreenThirds} times in VRAM, one per screen third:");
            text.AppendLine("; in GRAPHIC 2 and 3 each third has its own table, and the same tiles in all");
            text.AppendLine("; three is what lets a tile look the same wherever the map puts it.");
        }

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
