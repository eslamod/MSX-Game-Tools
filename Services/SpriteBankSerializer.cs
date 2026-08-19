using System.Text.Json;
using System.Text.Json.Serialization;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

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
    /// <summary>
    /// La 2 numera los patrones y guarda sólo los dibujados.
    /// </summary>
    /// <remarks>
    /// Un fichero de la 1 se sigue abriendo, y sale igual que estaba: allí los patrones iban
    /// seguidos desde el 0, así que su posición en el fichero <b>era</b> su índice, y
    /// leyéndolos por posición caen exactamente donde estaban. Ni un número se mueve.
    /// </remarks>
    public const int FormatVersion = 4;

    private const int PatternDigits = 4; // 16 columnas, cuatro dígitos hexadecimales

    /// <param name="backgrounds">
    /// Las imágenes de referencia cargadas. Se guarda la ruta y el troceado, no la
    /// imagen: son una ayuda para dibujar, y meter un png en base64 dentro del banco lo
    /// dejaría ilegible y enorme para algo que no forma parte de lo que se exporta.
    /// </param>
    public static string Serialize(
        SpriteBank bank,
        ColorPalette palette,
        int backgroundColorIndex,
        IReadOnlyList<ReferenceImage>? backgrounds = null) =>
        JsonSerializer.Serialize(
            ToFile(bank, palette, backgroundColorIndex, backgrounds ?? []), PaletteSerializer.Options);

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

        List<BackgroundImageRef> backgrounds =
        [
            .. (file.Backgrounds ?? [])
                .Where(image => !string.IsNullOrWhiteSpace(image.Path))
                .Select(image => new BackgroundImageRef(image.Path!, image.CellSize)),
        ];

        return new LoadedSpriteBank(bank, palette, background, backgrounds);
    }

    private static BankFile ToFile(
        SpriteBank bank,
        ColorPalette palette,
        int backgroundColorIndex,
        IReadOnlyList<ReferenceImage> backgrounds) => new(
        FormatVersion,
        bank.Name,
        bank.Type.ToString(),
        backgroundColorIndex,
        bank.Capacity,
        PaletteSerializer.ToFile(palette),
        [.. Drawn(bank)],
        [.. bank.Groups.Select(ToFile)],
        [.. backgrounds.Select(image => new ReferenceFile(image.Path, image.CellSize))],
        [.. bank.Animations.Select(ToFile)]);

    private static AnimationFile ToFile(SpriteAnimation animation) => new(
        animation.Name,
        animation.Kind.ToString(),
        animation.Mode.ToString(),
        [.. animation.Steps.Select(ToFile)]);

    private static StepFile ToFile(AnimationStep step) => step switch
    {
        AnimationLoop loop => new StepFile(
            LoopType, 0, 0, 0, 0, 0, loop.Times, [.. loop.Steps.Select(ToFile)]),

        AnimationFrame frame => new StepFile(
            FrameType, frame.Target, frame.Wait, frame.OffsetX, frame.OffsetY, frame.Event, 0, null),

        _ => throw new FileFormatException($"No se sabe guardar un paso de tipo {step.GetType().Name}."),
    };

    /// <summary>Los patrones que se han tocado, con su número delante.</summary>
    /// <remarks>
    /// Igual que en el juego de tiles: escribir los 64 siempre metería sesenta entradas
    /// idénticas en cada fichero de un banco con cuatro sprites.
    /// </remarks>
    private static IEnumerable<PatternFile> Drawn(SpriteBank bank)
    {
        for (int index = 0; index < bank.SpritesList.Count; index++)
        {
            Sprite pattern = bank.SpritesList[index];

            if (!pattern.IsEmpty)
                yield return ToFile(pattern, index);
        }
    }

    private static PatternFile ToFile(Sprite pattern, int index) => new(
        [.. pattern.ArraySpriteRows.Select(row => RowToHex(row.ArrayColumns))],
        string.Concat(pattern.ArraySpriteRows.Select(row => row.Color.ToString("X1"))),
        ToFile(pattern.Background),
        index);

    private static GroupFile ToFile(SpriteGroup group) => new(
        group.Name,
        [.. group.Members.Select(ToFile)],
        ToFile(group.Background));

    /// <summary>Sin fondo no se escribe nada, para no llenar el fichero de nulos.</summary>
    private static BackgroundFile? ToFile(BackgroundRef reference) =>
        reference.HasValue ? new BackgroundFile(reference.Path, reference.Cell) : null;

    private static BackgroundRef FromFile(BackgroundFile? file) =>
        file?.Path is { Length: > 0 } path && file.Cell >= 0
            ? new BackgroundRef(path, file.Cell)
            : BackgroundRef.None;

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

        // Sin capacidad en el fichero es de 64: es lo que habia antes de la version 3 y lo
        // unico que cabe en la tabla de patrones de la VRAM.
        int capacity = file.Capacity ?? SpriteBank.MaxSprites;

        if (!SpriteBank.Capacities.Contains(capacity))
        {
            throw new FileFormatException(
                $"El fichero dice que el banco tiene {capacity} patrones, y los tamanos que hay "
                + $"son {string.Join(", ", SpriteBank.Capacities)}.");
        }

        int patternCount = file.Patterns?.Count ?? 0;
        if (patternCount > capacity)
        {
            throw new FileFormatException(
                $"Un banco de este tamano tiene {capacity} patrones, y el fichero trae {patternCount}.");
        }

        var bank = new SpriteBank(
            type, string.IsNullOrWhiteSpace(file.Name) ? "Banco sin nombre" : file.Name, capacity);

        // El banco ya viene con sus 64 huecos hechos: aqui solo se rellenan los que traiga
        // el fichero, cada uno en el suyo.
        for (int position = 0; position < patternCount; position++)
        {
            PatternFile pattern = file.Patterns![position];

            // Los ficheros de la version 1 guardaban los patrones seguidos y sin numero, asi
            // que su posicion en la lista era su indice. Leyendolos por posicion salen
            // exactamente donde estaban: ni un solo numero se mueve al abrir un banco viejo.
            int index = pattern.Index ?? position;

            if ((uint)index >= (uint)capacity)
            {
                throw new FileFormatException(
                    $"El fichero trae el patrón {index}, y este banco sólo tiene {capacity}.");
            }

            ReadPattern(pattern, bank.SpritesList[index], index);
        }

        foreach (GroupFile group in file.Groups ?? [])
            ReadGroup(group, bank);

        foreach (AnimationFile animation in file.Animations ?? [])
            bank.Animations.Add(ReadAnimation(animation));

        return bank;
    }

    /// <summary>Los dos tipos de paso, tal y como se escriben en el fichero.</summary>
    private const string FrameType = "frame";

    /// <inheritdoc cref="FrameType"/>
    private const string LoopType = "loop";

    private static SpriteAnimation ReadAnimation(AnimationFile file)
    {
        var animation = new SpriteAnimation(
            string.IsNullOrWhiteSpace(file.Name) ? "Animación sin nombre" : file.Name,
            Parse<AnimationKind>(file.Kind, nameof(AnimationKind)))
        {
            Mode = Parse<AnimationMode>(file.Mode, nameof(AnimationMode)),
        };

        ReadSteps(file.Steps, animation.Steps);

        return animation;
    }

    private static void ReadSteps(IReadOnlyList<StepFile>? files, IList<AnimationStep> steps)
    {
        foreach (StepFile file in files ?? [])
            steps.Add(ReadStep(file));
    }

    private static AnimationStep ReadStep(StepFile file)
    {
        if (file.Type == LoopType)
        {
            var loop = new AnimationLoop { Times = file.Times };

            ReadSteps(file.Steps, loop.Steps);

            return loop;
        }

        if (file.Type != FrameType)
        {
            throw new FileFormatException(
                $"Un paso de animación es «{FrameType}» o «{LoopType}», y el fichero trae «{file.Type}».");
        }

        return new AnimationFrame
        {
            Target = file.Target,
            Wait = file.Wait,
            OffsetX = file.OffsetX,
            OffsetY = file.OffsetY,
            Event = file.Event,
        };
    }

    /// <summary>
    /// Lee un valor de los que el editor entiende, o se planta.
    /// </summary>
    /// <remarks>
    /// Sin distinguir mayúsculas: se escriben tal cual salen del enum, pero un fichero retocado a
    /// mano no tiene por qué respetarlo y eso no es motivo para no abrirlo.
    /// </remarks>
    private static T Parse<T>(string? value, string what)
        where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out T parsed)
            ? parsed
            : throw new FileFormatException($"El fichero trae «{value}» donde esperaba un {what}.");

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

        pattern.Background = FromFile(file.Background);
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
        group.Background = FromFile(file.Background);

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

    /// <param name="Capacity">
    /// Cuántos patrones tiene el banco. Los ficheros anteriores a la versión 3 no lo traen y
    /// son de 64, que era lo único que había.
    /// </param>
    private sealed record BankFile(
        int Version,
        string? Name,
        string? Type,
        int BackgroundColor,
        int? Capacity,
        PaletteSerializer.PaletteFile? Palette,
        IReadOnlyList<PatternFile>? Patterns,
        IReadOnlyList<GroupFile>? Groups,
        IReadOnlyList<ReferenceFile>? Backgrounds,
        IReadOnlyList<AnimationFile>? Animations);

    /// <param name="Kind">Si sus pasos enseñan patrones o grupos.</param>
    /// <param name="Mode">Qué pasa al llegar al final.</param>
    private sealed record AnimationFile(
        string? Name,
        string? Kind,
        string? Mode,
        IReadOnlyList<StepFile>? Steps);

    /// <summary>
    /// Un paso: o un fotograma o un bucle con los suyos dentro.
    /// </summary>
    /// <param name="Type">
    /// Cuál de los dos es. Escrito y no adivinado por qué campos vengan: un fichero se lee a
    /// mano más veces de las que se cree, y adivinarlo obliga a conocer la regla para entenderlo.
    /// </param>
    private sealed record StepFile(
        string? Type,
        int Target,
        int Wait,
        int OffsetX,
        int OffsetY,
        int Event,
        int Times,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<StepFile>? Steps);

    /// <summary>Una imagen de referencia: dónde estaba y cómo se troceó.</summary>
    private sealed record ReferenceFile(string? Path, int CellSize);

    /// <param name="Index">
    /// Qué hueco de los 64 ocupa. Los ficheros de la versión 1 no lo traen y se leen por
    /// posición, que allí era lo mismo: los patrones iban seguidos desde el 0.
    /// </param>
    private sealed record PatternFile(
        IReadOnlyList<string>? Rows,
        string? Colors,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BackgroundFile? Background,
        int? Index = null);

    private sealed record GroupFile(
        string? Name,
        IReadOnlyList<MemberFile>? Members,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BackgroundFile? Background);

    /// <summary>A qué celda de qué imagen apunta un grupo o un patrón.</summary>
    private sealed record BackgroundFile(string? Path, int Cell);

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
public sealed record LoadedSpriteBank(
    SpriteBank Bank,
    ColorPalette Palette,
    int BackgroundColorIndex,
    IReadOnlyList<BackgroundImageRef> Backgrounds);

/// <summary>
/// Una imagen de referencia por cargar. Va aparte del banco porque cargarla toca disco
/// y puede fallar sola, sin que eso impida abrir el banco.
/// </summary>
public sealed record BackgroundImageRef(string Path, int CellSize);
