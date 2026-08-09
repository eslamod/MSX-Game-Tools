using System.Text.Json;
using System.Text.Json.Serialization;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.Services;

/// <summary>
/// Formato de fichero de un banco de sprites: sus patrones, sus grupos y la paleta con
/// la que se dibujó.
/// </summary>
/// <remarks>
/// <para>
/// Los patrones van como máscaras de bits en hexadecimal, una por línea y cuatro
/// dígitos cada una, que es la forma de la tabla de patrones del VDP. El bit más
/// significativo es la columna 0, la de la izquierda.
/// </para>
/// <para>
/// La paleta va embebida a propósito. Los sprites guardan índices, no colores, así que
/// un banco sin su paleta se puede abrir con unos colores completamente distintos. Al
/// cargarlo, si en la biblioteca ya hay una paleta idéntica se reutiliza.
/// </para>
/// </remarks>
public static class SpriteBankSerializer
{
    public const int FormatVersion = 1;

    private const int PatternDigits = 4; // 16 columnas, cuatro dígitos hexadecimales

    public static string Serialize(SpriteBank bank, ColorPalette palette, int backgroundColorIndex) =>
        JsonSerializer.Serialize(ToFile(bank, palette, backgroundColorIndex), PaletteSerializer.Options);

    /// <exception cref="FileFormatException">El contenido no es un banco válido.</exception>
    public static LoadedSpriteBank Deserialize(string json)
    {
        BankFile? file;

        try
        {
            file = JsonSerializer.Deserialize<BankFile>(json, PaletteSerializer.Options);
        }
        catch (JsonException exception)
        {
            throw new FileFormatException("El fichero no contiene JSON válido.", exception);
        }

        if (file is null)
            throw new FileFormatException("El fichero está vacío.");

        if (file.Version > FormatVersion)
        {
            throw new FileFormatException(
                $"El banco usa la versión {file.Version} del formato y esta versión del editor sólo entiende hasta la {FormatVersion}.");
        }

        if (file.Palette is null)
            throw new FileFormatException("El fichero no trae la paleta del banco.");

        SpriteBank bank = BuildBank(file);
        ColorPalette palette = PaletteSerializer.FromFile(file.Palette);

        int background = file.BackgroundColor is >= 1 and < ColorPalette.Size ? file.BackgroundColor : 1;

        return new LoadedSpriteBank(bank, palette, background);
    }

    private static BankFile ToFile(SpriteBank bank, ColorPalette palette, int backgroundColorIndex) => new(
        FormatVersion,
        bank.Name,
        bank.Type.ToString(),
        backgroundColorIndex,
        PaletteSerializer.ToFile(palette),
        [.. bank.SpritesList.Select(ToFile)],
        [.. bank.Groups.Select(ToFile)]);

    private static PatternFile ToFile(Sprite pattern) => new(
        [.. pattern.ArraySpriteRows.Select(row => RowToHex(row.ArrayColumns))],
        string.Concat(pattern.ArraySpriteRows.Select(row => row.Color.ToString("X1"))));

    private static GroupFile ToFile(SpriteGroup group) => new(
        group.Name,
        [.. group.Members.Select(ToFile)]);

    private static MemberFile ToFile(SpriteGroupMember member) => new(
        member.PatternIndex,
        member.OffsetX,
        member.OffsetY,
        [.. member.Rows.Select(row => new LineFile(
            row.Color.ToString("X1"),
            row.CombineColor,
            row.InhibitCollision,
            row.EarlyClock))]);

    private static SpriteBank BuildBank(BankFile file)
    {
        SpriteBank.SpriteType type = ParseType(file.Type);

        int patternCount = file.Patterns?.Count ?? 0;
        if (patternCount is 0 or > SpriteBank.MaxSprites)
        {
            throw new FileFormatException(
                $"Un banco tiene entre 1 y {SpriteBank.MaxSprites} patrones, y el fichero trae {patternCount}.");
        }

        var bank = new SpriteBank(type, string.IsNullOrWhiteSpace(file.Name) ? "Banco sin nombre" : file.Name);

        // El constructor ya deja uno creado.
        while (bank.SpritesList.Count < patternCount)
            bank.NewSprite();

        for (int index = 0; index < patternCount; index++)
            ReadPattern(file.Patterns![index], bank.SpritesList[index], index);

        foreach (GroupFile group in file.Groups ?? [])
            ReadGroup(group, bank);

        return bank;
    }

    private static void ReadPattern(PatternFile file, Sprite pattern, int patternIndex)
    {
        int rowCount = file.Rows?.Count ?? 0;
        if (rowCount != Sprite.Rows)
        {
            throw new FileFormatException(
                $"El patrón {patternIndex} debe traer {Sprite.Rows} líneas, y trae {rowCount}.");
        }

        if ((file.Colors?.Length ?? 0) != Sprite.Rows)
        {
            throw new FileFormatException(
                $"El patrón {patternIndex} debe traer un dígito de color por línea, {Sprite.Rows} en total.");
        }

        for (int row = 0; row < Sprite.Rows; row++)
        {
            HexToRow(file.Rows![row], pattern.ArraySpriteRows[row].ArrayColumns, patternIndex, row);
            pattern.ArraySpriteRows[row].Color = ParseColor(file.Colors![row], $"el patrón {patternIndex}, línea {row}");
        }
    }

    private static void ReadGroup(GroupFile file, SpriteBank bank)
    {
        int memberCount = file.Members?.Count ?? 0;
        if (memberCount is 0 or > SpriteGroup.MaxMembers)
        {
            throw new FileFormatException(
                $"El grupo «{file.Name}» debe tener entre 1 y {SpriteGroup.MaxMembers} sprites, y trae {memberCount}.");
        }

        SpriteGroup? group = bank.NewGroup(ClampPattern(file.Members![0].Pattern, bank, file.Name))
                             ?? throw new FileFormatException($"El fichero trae más de {SpriteBank.MaxGroups} grupos.");

        group.Name = string.IsNullOrWhiteSpace(file.Name) ? group.Name : file.Name;

        ReadMember(file.Members[0], group.Members[0], file.Name);

        for (int index = 1; index < memberCount; index++)
        {
            MemberFile memberFile = file.Members[index];
            int patternIndex = ClampPattern(memberFile.Pattern, bank, file.Name);

            var member = new SpriteGroupMember(patternIndex, bank.SpritesList[patternIndex]);
            group.Add(member);
            ReadMember(memberFile, member, file.Name);
        }
    }

    private static void ReadMember(MemberFile file, SpriteGroupMember member, string? groupName)
    {
        member.OffsetX = file.OffsetX;
        member.OffsetY = file.OffsetY;

        int lineCount = file.Lines?.Count ?? 0;
        if (lineCount != Sprite.Rows)
        {
            throw new FileFormatException(
                $"Cada sprite del grupo «{groupName}» debe traer {Sprite.Rows} líneas de atributos, y trae {lineCount}.");
        }

        for (int row = 0; row < Sprite.Rows; row++)
        {
            LineFile line = file.Lines![row];

            member.Rows[row].Color = ParseColor(line.Color?.FirstOrDefault() ?? '\0', $"el grupo «{groupName}», línea {row}");
            member.Rows[row].CombineColor = line.Cc;
            member.Rows[row].InhibitCollision = line.Ic;
            member.Rows[row].EarlyClock = line.Ec;
        }
    }

    private static int ClampPattern(int patternIndex, SpriteBank bank, string? groupName)
    {
        if ((uint)patternIndex >= (uint)bank.SpritesList.Count)
        {
            throw new FileFormatException(
                $"El grupo «{groupName}» apunta al patrón {patternIndex}, y el banco sólo tiene {bank.SpritesList.Count}.");
        }

        return patternIndex;
    }

    private static SpriteBank.SpriteType ParseType(string? type) =>
        Enum.TryParse(type, ignoreCase: true, out SpriteBank.SpriteType parsed)
            ? parsed
            : throw new FileFormatException($"El tipo de banco «{type}» no es MSX ni MSX2.");

    private static int ParseColor(char digit, string where) =>
        PaletteSerializer.HexDigit(digit)
        ?? throw new FileFormatException($"En {where} hay un color que no es un dígito hexadecimal: «{digit}».");

    /// <summary>Los 16 pixeles de una línea, con la columna 0 en el bit más significativo.</summary>
    private static string RowToHex(bool[] columns)
    {
        int bits = 0;

        for (int column = 0; column < SpriteRow.Columns; column++)
        {
            if (columns[column])
                bits |= 1 << (SpriteRow.Columns - 1 - column);
        }

        return bits.ToString("X4");
    }

    private static void HexToRow(string? hex, bool[] columns, int patternIndex, int row)
    {
        if (hex is null || hex.Length != PatternDigits)
        {
            throw new FileFormatException(
                $"La línea {row} del patrón {patternIndex} debe traer {PatternDigits} dígitos hexadecimales; trae «{hex}».");
        }

        int bits = 0;

        foreach (char digit in hex)
        {
            int value = PaletteSerializer.HexDigit(digit)
                        ?? throw new FileFormatException(
                            $"La línea {row} del patrón {patternIndex} tiene un dígito que no es hexadecimal: «{digit}».");

            bits = (bits << 4) | value;
        }

        for (int column = 0; column < SpriteRow.Columns; column++)
            columns[column] = (bits & (1 << (SpriteRow.Columns - 1 - column))) != 0;
    }

    private sealed record BankFile(
        int Version,
        string? Name,
        string? Type,
        int BackgroundColor,
        PaletteSerializer.PaletteFile? Palette,
        IReadOnlyList<PatternFile>? Patterns,
        IReadOnlyList<GroupFile>? Groups);

    private sealed record PatternFile(IReadOnlyList<string>? Rows, string? Colors);

    private sealed record GroupFile(string? Name, IReadOnlyList<MemberFile>? Members);

    private sealed record MemberFile(
        int Pattern,
        int OffsetX,
        int OffsetY,
        IReadOnlyList<LineFile>? Lines);

    /// <summary>Los tres bits sólo se escriben cuando están activos, para no llenar el fichero de falses.</summary>
    private sealed record LineFile(
        string? Color,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Cc,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Ic,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Ec);
}

/// <summary>Lo que sale de leer un fichero de banco.</summary>
public sealed record LoadedSpriteBank(SpriteBank Bank, ColorPalette Palette, int BackgroundColorIndex);
