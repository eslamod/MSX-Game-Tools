using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

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
/// con cuántos sprites lo forman, porque un grupo puede tener de uno a ocho y sin ese
/// byte el fichero no se puede recorrer. Un byte llega de sobra para el tope de hoy y
/// para el que venga.
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
    /// <summary>Con la que se exporta si nadie ha elegido otra en las preferencias.</summary>
    public const string DataDirective = AsmStyle.Dotted;

    private const int BytesPerLine = 8;

    /// <summary>
    /// Cuántos patrones entran en la tabla: hasta el último dibujado, ése incluido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No los 64 siempre: son 2 KB de tabla aunque se usen cuatro sprites, y eso pesa en
    /// una ROM de 32K. Y no «los que tengan algo», que dejaría fuera los huecos vacíos de
    /// en medio y correría todo lo de atrás: se corta por el final, que es lo único que no
    /// mueve ningún índice.
    /// </para>
    /// <para>
    /// Un banco entero en blanco exporta un patrón, no cero: una tabla vacía no se puede
    /// cargar en el VDP y un fichero de cero bytes parece un error de la exportación.
    /// </para>
    /// </remarks>
    public static int TableLength(SpriteBank bank) => Math.Max(1, bank.LastDrawn() + 1);

    public static byte[] PatternsToBinary(SpriteBank bank) =>
        PatternsToBinary(bank, 0, TableLength(bank) - 1);

    /// <summary>Sólo un trozo de la tabla, del <paramref name="first"/> al <paramref name="last"/>.</summary>
    /// <inheritdoc cref="PatternsToAssembler(SpriteBank, int, int)"/>
    public static byte[] PatternsToBinary(SpriteBank bank, int first, int last)
    {
        (first, last) = Clamp(bank, first, last);

        var bytes = new List<byte>((last - first + 1) * PatternBytes);

        for (int index = first; index <= last; index++)
            bytes.AddRange(PatternBytesOf(bank.SpritesList[index]));

        return [.. bytes];
    }

    /// <summary>
    /// Deja el rango dentro del banco y con los extremos en orden.
    /// </summary>
    /// <remarks>
    /// Ordenados y no rechazados: quien escribe un rango a mano pasa por estados a medias, y
    /// cortarle la exportación por eso sería antipático. Lo que no se deja es salirse del banco.
    /// </remarks>
    private static (int First, int Last) Clamp(SpriteBank bank, int first, int last)
    {
        int top = bank.SpritesList.Count - 1;

        return (Math.Clamp(Math.Min(first, last), 0, top), Math.Clamp(Math.Max(first, last), 0, top));
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

    public static string PatternsToAssembler(SpriteBank bank, AsmStyle? style = null) =>
        PatternsToAssembler(bank, 0, TableLength(bank) - 1, style);

    /// <summary>
    /// Sólo un trozo de la tabla, del <paramref name="first"/> al <paramref name="last"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Para cargar los patrones por partes, que es lo que hace un juego cuando no le caben los
    /// 64 en VRAM a la vez o cuando cada pantalla trae sus bichos. Se piden por índice del banco
    /// y no por «los que hagan falta»: los grupos apuntan a números concretos, y renumerar al
    /// exportar dejaría los grupos mintiendo.
    /// </para>
    /// <para>
    /// Y por eso las etiquetas guardan el índice del banco aunque el fichero empiece por el
    /// medio: el <c>_pattern_37</c> se llama 37 aquí y en los grupos. Lo que cambia es dónde cae
    /// dentro del fichero, y eso lo dice la cabecera, que es donde hay que mirarlo una vez.
    /// </para>
    /// </remarks>
    public static string PatternsToAssembler(
        SpriteBank bank, int first, int last, AsmStyle? style = null)
    {
        string data = AsmStyle.Of(style?.Data).Data;

        var text = new StringBuilder();
        string label = AsmLabel.Of(bank.Name);

        (first, last) = Clamp(bank, first, last);

        int count = last - first + 1;

        text.AppendLine($"; Sprite pattern table - {bank.Name}");
        text.AppendLine($"; {count} patterns, {PatternBytes} bytes each: bank patterns {first} to {last}.");
        text.AppendLine($"; The bank holds {bank.Capacity} slots. Pattern N of the bank is at");
        text.AppendLine($"; (N - {first}) * {PatternBytes} bytes from here, blank slots included.");
        text.AppendLine("; 16x16 layout: left half rows 0-15, then right half rows 0-15");
        text.AppendLine($"; Size: {label}_patterns_end - {label}_patterns");
        text.AppendLine();
        text.AppendLine($"{label}_patterns:");

        for (int index = first; index <= last; index++)
        {
            text.AppendLine($"{label}_pattern_{index}:");
            AppendBytes(text, PatternBytesOf(bank.SpritesList[index]), data);
        }

        text.AppendLine($"{label}_patterns_end:");

        return text.ToString();
    }

    public static string GroupsToAssembler(SpriteBank bank, AsmStyle? style = null)
    {
        string data = AsmStyle.Of(style?.Data).Data;

        var text = new StringBuilder();
        string label = AsmLabel.Of(bank.Name);

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
            text.AppendLine($"    {data}  {Hex((byte)group.Members.Count)}                 ; sprites");

            for (int member = 0; member < group.Members.Count; member++)
            {
                text.AppendLine($"    ; sprite {member} - pattern {group.Members[member].PatternIndex}");
                AppendBytes(text, MemberBytesOf(group.Members[member], bank.Type), data);
            }
        }

        text.AppendLine($"{label}_groups_end:");

        return text.ToString();
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

    private static void AppendBytes(StringBuilder text, IEnumerable<byte> bytes, string data)
    {
        byte[] all = [.. bytes];

        for (int start = 0; start < all.Length; start += BytesPerLine)
        {
            IEnumerable<string> line = all
                .Skip(start)
                .Take(BytesPerLine)
                .Select(Hex);

            text.AppendLine($"    {data}  {string.Join(",", line)}");
        }
    }

    private static string Hex(byte value) => $"{HexPrefix}{value:X2}";

    /// <summary>Un byte con el prefijo hexadecimal del ensamblador, para quien lo necesite fuera.</summary>
    public static string HexOf(byte value) => Hex(value);
}
