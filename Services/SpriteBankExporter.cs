using System.Text;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.Services;

/// <summary>
/// Vuelca un banco a las tablas que espera el VDP, en dos ficheros: patrones por un
/// lado, y grupos con sus colores y atributos por otro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Patrones.</b> 32 bytes por sprite de 16x16. El manual del V9938 coloca los cuatro
/// cuartos como #0 arriba izquierda, #1 abajo izquierda, #2 arriba derecha y #3 abajo
/// derecha, así que salen los 16 bytes de la mitad izquierda y luego los de la derecha.
/// </para>
/// <para>
/// <b>Grupos.</b> Los desplazamientos y los números de patrón son relativos al grupo:
/// esto no es una tabla de atributos lista para volcar, es la receta con la que el
/// juego la construye allá donde coloque al personaje. Cada grupo empieza por un byte
/// con cuántos sprites lo forman, porque un grupo puede tener de uno a cuatro y sin ese
/// byte el fichero no se puede recorrer.
/// </para>
/// <para>
/// <b>Etiquetas.</b> La salida en ensamblador cierra cada bloque con una etiqueta
/// <c>_end</c>. Hace falta porque ninguno de los dos ficheros dice cuántos elementos
/// trae: quien los consume recorre de <c>_patterns</c> a <c>_patterns_end</c> y de
/// <c>_groups</c> a <c>_groups_end</c>, y de paso puede calcular el tamaño para
/// volcarlo a VRAM sin escribir la cifra a mano.
/// </para>
/// </remarks>
public static class SpriteBankExporter
{
    /// <summary>32 bytes por patrón de 16x16.</summary>
    public const int PatternBytes = 32;

    /// <summary>Un miembro MSX2: 16 bytes de color más Y, X y patrón.</summary>
    public const int Msx2MemberBytes = Sprite.Rows + 3;

    /// <summary>Un miembro MSX1: Y, X, patrón y color, como la tabla de atributos.</summary>
    public const int Msx1MemberBytes = 4;

    /// <summary>
    /// Prefijo hexadecimal de la salida en ensamblador. <c>0x</c> porque es el que
    /// documenta sass y el único sin ambigüedad: <c>$</c> también vale, pero ahí mismo
    /// significa la dirección actual, y <c>#</c> es prefijo de directiva, no de número.
    /// </summary>
    public const string HexPrefix = "0x";

    /// <summary>
    /// Directiva de datos. Con el punto delante porque funciona siempre; el <c>db</c>
    /// pelado sólo lo acepta sass con el modo asMSX activado.
    /// </summary>
    public const string DataDirective = ".db";

    private const int BytesPerLine = 8;

    public static byte[] PatternsToBinary(SpriteBank bank)
    {
        var bytes = new List<byte>(bank.SpritesList.Count * PatternBytes);

        foreach (Sprite pattern in bank.SpritesList)
            bytes.AddRange(PatternBytesOf(pattern));

        return [.. bytes];
    }

    public static byte[] GroupsToBinary(SpriteBank bank)
    {
        var bytes = new List<byte>();

        foreach (SpriteGroup group in bank.Groups)
        {
            bytes.Add((byte)group.Members.Count);

            foreach (SpriteGroupMember member in group.Members)
                bytes.AddRange(MemberBytesOf(member, bank.Type));
        }

        return [.. bytes];
    }

    public static string PatternsToAssembler(SpriteBank bank)
    {
        var text = new StringBuilder();
        string label = LabelOf(bank.Name);

        text.AppendLine($"; Sprite pattern table - {bank.Name}");
        text.AppendLine($"; {bank.SpritesList.Count} patterns, {PatternBytes} bytes each");
        text.AppendLine("; 16x16 layout: left half rows 0-15, then right half rows 0-15");
        text.AppendLine($"; Size: {label}_patterns_end - {label}_patterns");
        text.AppendLine();
        text.AppendLine($"{label}_patterns:");

        for (int index = 0; index < bank.SpritesList.Count; index++)
        {
            text.AppendLine($"{label}_pattern_{index}:");
            AppendBytes(text, PatternBytesOf(bank.SpritesList[index]));
        }

        text.AppendLine($"{label}_patterns_end:");

        return text.ToString();
    }

    public static string GroupsToAssembler(SpriteBank bank)
    {
        var text = new StringBuilder();
        string label = LabelOf(bank.Name);

        text.AppendLine($"; Sprite groups - {bank.Name} ({bank.Type})");
        text.AppendLine("; Per group: 1 byte with the number of sprites, then per sprite:");

        text.AppendLine(bank.Type == SpriteBank.SpriteType.MSX2
            ? ";   16 colour bytes (EC CC IC 0 cccc), offset Y, offset X, pattern number"
            : ";   offset Y, offset X, pattern number, colour byte (EC 0 0 0 cccc)");

        text.AppendLine("; Offsets are relative to the group and stored as two's complement.");
        text.AppendLine("; Pattern numbers are already multiplied by 4, ready for the attribute table.");
        text.AppendLine($"; There is no group count: walk from {label}_groups to {label}_groups_end.");
        text.AppendLine();
        text.AppendLine($"{label}_groups:");

        for (int index = 0; index < bank.Groups.Count; index++)
        {
            SpriteGroup group = bank.Groups[index];

            text.AppendLine($"{label}_group_{index}:               ; {group.Name}");
            text.AppendLine($"    {DataDirective}  {Hex((byte)group.Members.Count)}                 ; sprites");

            for (int member = 0; member < group.Members.Count; member++)
            {
                text.AppendLine($"    ; sprite {member} - pattern {group.Members[member].PatternIndex}");
                AppendBytes(text, MemberBytesOf(group.Members[member], bank.Type));
            }
        }

        text.AppendLine($"{label}_groups_end:");

        return text.ToString();
    }

    /// <summary>Nombre de fichero o etiqueta válida a partir del nombre del banco.</summary>
    public static string LabelOf(string bankName)
    {
        var label = new StringBuilder();

        foreach (char character in bankName.ToLowerInvariant())
            label.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');

        string result = label.ToString().Trim('_');

        if (result.Length == 0)
            return "sprites";

        // Una etiqueta no puede empezar por dígito.
        return char.IsAsciiDigit(result[0]) ? $"s{result}" : result;
    }

    private static IEnumerable<byte> PatternBytesOf(Sprite pattern)
    {
        // Mitad izquierda entera y luego la derecha, que es como quedan los cuatro
        // cuartos #0, #1, #2, #3 del manual.
        for (int half = 0; half < 2; half++)
        {
            for (int row = 0; row < Sprite.Rows; row++)
                yield return HalfRowByte(pattern.ArraySpriteRows[row].ArrayColumns, half * 8);
        }
    }

    private static byte HalfRowByte(bool[] columns, int firstColumn)
    {
        int bits = 0;

        for (int offset = 0; offset < 8; offset++)
        {
            if (columns[firstColumn + offset])
                bits |= 1 << (7 - offset);
        }

        return (byte)bits;
    }

    private static IEnumerable<byte> MemberBytesOf(SpriteGroupMember member, SpriteBank.SpriteType type)
    {
        byte patternNumber = (byte)(member.PatternIndex * 4);

        if (type == SpriteBank.SpriteType.MSX2)
        {
            foreach (SpriteAttributeRow row in member.Rows)
                yield return ColorTableByte(row);

            yield return (byte)member.OffsetY;
            yield return (byte)member.OffsetX;
            yield return patternNumber;

            yield break;
        }

        // MSX1: no hay tabla de colores, el color va en el cuarto byte del atributo.
        yield return (byte)member.OffsetY;
        yield return (byte)member.OffsetX;
        yield return patternNumber;
        yield return AttributeColorByte(member.Rows[0]);
    }

    /// <summary>Byte de la tabla de colores del modo 2: EC CC IC 0 y el color.</summary>
    private static byte ColorTableByte(SpriteAttributeRow row)
    {
        int bits = row.Color & 0x0F;

        if (row.EarlyClock)
            bits |= 0x80;

        if (row.CombineColor)
            bits |= 0x40;

        if (row.InhibitCollision)
            bits |= 0x20;

        return (byte)bits;
    }

    /// <summary>Cuarto byte de la tabla de atributos del modo 1: EC y el color.</summary>
    private static byte AttributeColorByte(SpriteAttributeRow row)
    {
        int bits = row.Color & 0x0F;

        if (row.EarlyClock)
            bits |= 0x80;

        return (byte)bits;
    }

    private static void AppendBytes(StringBuilder text, IEnumerable<byte> bytes)
    {
        byte[] all = [.. bytes];

        for (int start = 0; start < all.Length; start += BytesPerLine)
        {
            IEnumerable<string> line = all
                .Skip(start)
                .Take(BytesPerLine)
                .Select(Hex);

            text.AppendLine($"    {DataDirective}  {string.Join(",", line)}");
        }
    }

    private static string Hex(byte value) => $"{HexPrefix}{value:X2}";
}
