using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// What comes out of exporting a tile set: the two tables, and what it has on top of them.
/// </summary>
/// <remarks>
/// The super tiles travel with the set and not with the map: they are the set's, and every map
/// drawn with it shares the same table. With each map it would come out the same, repeated.
/// </remarks>
public sealed class TileSetExport(TileSetEditorViewModel tiles) : IExportDocument
{
    private static Localizer Text => Localizer.Instance;

    public string DocumentName => tiles.DocumentName;

    public string Stem => AsmLabel.Of(tiles.TileSet.Name);

    public IReadOnlyList<ExportChoice> Formats { get; } =
    [
        new(ExportFormat.Assembler, Text["ExportFormatAsm"], ".asm"),
        new(ExportFormat.Binary, Text["ExportFormatBin"], ".bin"),
        new(ExportFormat.Png, Text["ExportFormatPng"], ".png"),
    ];

    public bool HasExampleRom => true;

    /// <summary>No screens: what goes out is a table and there is no rectangle to cut.</summary>
    public ScreenNumber? FirstScreen => null;

    /// <summary>Nothing gets in the way.</summary>
    public string? Problem(ExportRequest request) => null;

    public IEnumerable<ExportPiece> Pieces(ExportRequest request)
    {
        TileSet tileSet = tiles.TileSet;

        if (request.Format == ExportFormat.Png)
        {
            yield return new ExportPiece(string.Empty, path =>
            {
                PngFile.Write(
                    path,
                    TileSetPngConverter.ToPixels(tileSet, tiles.ColorPalette),
                    TileSetPngConverter.FullSize);

                return Task.CompletedTask;
            });

            yield break;
        }

        bool binary = request.Format == ExportFormat.Binary;
        AsmStyle style = request.Style;

        yield return new ExportPiece("_patterns", path => binary
            ? File.WriteAllBytesAsync(path, TileSetExporter.PatternsToBinary(tileSet))
            : File.WriteAllTextAsync(path, TileSetExporter.PatternsToAssembler(tileSet, style)));

        yield return new ExportPiece("_colors", path => binary
            ? File.WriteAllBytesAsync(path, TileSetExporter.ColorsToBinary(tileSet))
            : File.WriteAllTextAsync(path, TileSetExporter.ColorsToAssembler(tileSet, style)));

        if (tileSet.HasSuperTiles)
        {
            yield return new ExportPiece("_supertiles", path => binary
                ? File.WriteAllBytesAsync(path, SuperTileExporter.ToBinary(tileSet))
                : File.WriteAllTextAsync(path, SuperTileExporter.ToAssembler(tileSet, style)));
        }

        // Y la de atributos sólo si se han definido: quien no los usa no tiene por qué
        // encontrarse un fichero de 256 ceros que no sabe para qué es.
        if (tileSet.AttributeNames.Any)
        {
            yield return new ExportPiece("_attributes", path => binary
                ? File.WriteAllBytesAsync(path, TileSetExporter.AttributesToBinary(tileSet))
                : File.WriteAllTextAsync(path, TileSetExporter.AttributesToAssembler(tileSet, style)));
        }

        // Last of all because it is the one that ties the rest together: it loads them and
        // puts them on screen, and it is named after them.
        if (request.Rom is { } dialect)
        {
            yield return new ExportPiece(
                ExampleRom.Suffix,
                path => File.WriteAllTextAsync(
                    path,
                    ExampleRom.ForTileSet(tileSet, tiles.ColorPalette, dialect, request.Stem, binary)),
                ExampleRom.Extension);
        }
    }

    /// <summary>Nothing to ask: a tile set always exports.</summary>
    public Task<bool> ReadyAsync() => Task.FromResult(true);

    /// <summary>
    /// The three copies in VRAM and the ceiling of super tiles a map can name.
    /// </summary>
    /// <remarks>
    /// Two things that are nowhere in what was exported, and without which nobody knows what to
    /// do with it.
    /// </remarks>
    public (string Title, string Body)? Note(ExportRequest request)
    {
        if (request.Format == ExportFormat.Png)
            return null;

        TileSet tileSet = tiles.TileSet;
        string extension = ExtensionOf(request.Format);

        string done = Text.Format(
            "ExportedTileSetBody",
            $"{request.Stem}_patterns{extension}",
            $"{request.Stem}_colors{extension}",
            TileSetExporter.ScreenThirds);

        if (tileSet.HasSuperTiles)
        {
            done += " " + Text.Format(
                "ExportedSuperTiles",
                $"{request.Stem}_supertiles{extension}",
                SuperTileExporter.CountOf(tileSet));

            // Una celda del mapa es un byte, asi que de 256 para arriba hay supertiles que
            // ningun mapa puede nombrar. Mejor decirlo que dejar una tabla que no cuadra.
            if (tileSet.Blocks.Count > SuperTileExporter.MaxSuperTiles)
            {
                done += " " + Text.Format(
                    "ExportedSuperTilesTooMany", tileSet.Blocks.Count, SuperTileExporter.MaxSuperTiles);
            }
        }

        return (Text["ExportedTileSetTitle"], done);
    }

    private static string ExtensionOf(ExportFormat format) =>
        format == ExportFormat.Binary ? ".bin" : ".asm";
}
