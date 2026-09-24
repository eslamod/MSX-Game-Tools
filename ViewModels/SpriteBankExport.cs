using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// What comes out of exporting a sprite bank: the patterns, the groups and, if there are any,
/// the animations.
/// </summary>
/// <remarks>
/// There is no png here: a bank is not a picture of anything, its patterns are laid out in
/// groups that only mean something once they are placed.
/// </remarks>
public sealed class SpriteBankExport(SpritesEditorViewModel sprites, IDialogService dialogs)
    : IExportDocument
{
    private static Localizer Text => Localizer.Instance;

    public string DocumentName => sprites.DocumentName;

    public string Stem => AsmLabel.Of(sprites.SpritesBank.Name);

    public IReadOnlyList<ExportChoice> Formats { get; } =
    [
        new(ExportFormat.Assembler, Text["ExportFormatAsm"], ".asm"),
        new(ExportFormat.Binary, Text["ExportFormatBin"], ".bin"),
    ];

    /// <summary>
    /// Only for an MSX2 bank.
    /// </summary>
    /// <remarks>
    /// The ROM puts GRAPHIC 3 with mode 2 sprites, which is what the sixteen colour bytes per
    /// sprite of this bank are for. An MSX1 one would need another program, so rather than
    /// hand out a ROM that shows it wrong, the box does not come out.
    /// </remarks>
    public bool HasExampleRom => sprites.SpritesBank.Type == SpriteBank.SpriteType.MSX2;

    /// <summary>No screens: what goes out is a table and there is no rectangle to cut.</summary>
    public ScreenNumber? FirstScreen => null;

    /// <summary>Nothing gets in the way.</summary>
    public string? Problem(ExportRequest request) => null;

    /// <summary>Nothing to mark: what goes out is a table, not a piece of something else.</summary>
    public void Preview(ExportRequest? request)
    {
    }

    public IEnumerable<ExportPiece> Pieces(ExportRequest request)
    {
        SpriteBank bank = sprites.SpritesBank;
        bool binary = request.Format == ExportFormat.Binary;
        AsmStyle style = request.Style;

        yield return new ExportPiece("_patterns", path => binary
            ? File.WriteAllBytesAsync(path, SpriteBankExporter.PatternsToBinary(bank))
            : File.WriteAllTextAsync(path, SpriteBankExporter.PatternsToAssembler(bank, style)));

        yield return new ExportPiece("_groups", path => binary
            ? File.WriteAllBytesAsync(path, SpriteBankExporter.GroupsToBinary(bank))
            : File.WriteAllTextAsync(path, SpriteBankExporter.GroupsToAssembler(bank, style)));

        // Sólo si hay: un fichero de cero bytes junto a los otros dos parece que algo ha
        // fallado, y quien no anima nada no tiene por qué encontrárselo.
        if (bank.Animations.Count > 0)
        {
            yield return new ExportPiece("_animations", path => binary
                ? File.WriteAllBytesAsync(path, SpriteAnimationExporter.ToBinary(bank))
                : File.WriteAllTextAsync(path, SpriteAnimationExporter.ToAssembler(bank, style)));
        }

        // The ROM and the routine it brings in with an include. Last of all because they are
        // what ties the rest together: they load the files above and put them on screen.
        if (request.Rom is { } dialect)
        {
            yield return new ExportPiece(
                ExampleRom.Suffix,
                path => File.WriteAllTextAsync(
                    path,
                    ExampleRom.ForSpriteBank(
                        bank, sprites.ColorPalette, dialect, request.Stem, binary)),
                ExampleRom.Extension);

            yield return new ExportPiece(
                ExampleRom.PlayerSuffix,
                path => File.WriteAllTextAsync(path, ExampleRom.AnimationPlayer(dialect, bank)),
                ExampleRom.Extension);
        }
    }

    /// <summary>
    /// Warns if some animation asks for a group that is no longer there, and lets it be decided.
    /// </summary>
    /// <remarks>
    /// Deleting a group does not touch the animations, so it is possible to get here with one
    /// pointing at a number that is gone. It exports all the same —stopping the export over this
    /// would be worse— but saying so: in the file it is an 0xFF and on the machine, a sprite that
    /// never shows up.
    /// </remarks>
    public async Task<bool> ReadyAsync()
    {
        IReadOnlyList<SpriteAnimationExporter.MissingGroup> missing =
            SpriteAnimationExporter.MissingGroups(sprites.SpritesBank);

        if (missing.Count == 0)
            return true;

        string what = string.Join(
            Environment.NewLine,
            missing.Select(one => $"{one.Animation}: {one.Group}").Distinct());

        return await dialogs.ConfirmAsync(
            Text["ExportMissingGroupsTitle"],
            Text.Format("ExportMissingGroupsBody", what),
            Text["ExportAnywayLabel"]);
    }

    /// <summary>
    /// Which files came out.
    /// </summary>
    /// <remarks>
    /// The one name that was chosen turns into two or three, so it is worth saying which.
    /// </remarks>
    public (string Title, string Body)? Note(ExportRequest request)
    {
        string extension = request.Format == ExportFormat.Binary ? ".bin" : ".asm";
        string patterns = $"{request.Stem}_patterns{extension}";
        string groups = $"{request.Stem}_groups{extension}";

        string done = sprites.SpritesBank.Animations.Count == 0
            ? Text.Format("ExportedTwoFiles", patterns, groups)
            : Text.Format(
                "ExportedThreeFiles", patterns, groups, $"{request.Stem}_animations{extension}");

        return (Text["ExportedSpriteBankTitle"], done);
    }
}
