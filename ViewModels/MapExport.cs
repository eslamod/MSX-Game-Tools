using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// What comes out of exporting a map: the name table with its size in front of it.
/// </summary>
/// <remarks>
/// One file and not several, so the list of what is coming says less here than for the others.
/// What the panel does bring is the csv into the same question: it was a third menu entry
/// answering what the other two answered, with another format.
/// </remarks>
public sealed class MapExport(MapEditorViewModel map) : IExportDocument
{
    private static Localizer Text => Localizer.Instance;

    public string DocumentName => map.DocumentName;

    public string Stem => MainWindowViewModel.CleanFileName(map.Map.Name);

    public IReadOnlyList<ExportChoice> Formats { get; } =
    [
        new(ExportFormat.Assembler, Text["ExportFormatAsm"], ".asm"),
        new(ExportFormat.Binary, Text["ExportFormatBin"], ".bin"),
        new(ExportFormat.Csv, Text["ExportFormatCsv"], ".csv"),
    ];

    /// <summary>
    /// Any map, super tiles or not.
    /// </summary>
    /// <remarks>
    /// Which program comes out is decided further in: a map of super tiles resolves every cell
    /// through the table as it draws, and that is another template. From here it is the same
    /// question.
    /// </remarks>
    public bool HasExampleRom => true;

    public IEnumerable<ExportPiece> Pieces(ExportRequest request)
    {
        TileMap tileMap = map.Map;

        yield return new ExportPiece(string.Empty, path => request.Format switch
        {
            ExportFormat.Binary => File.WriteAllBytesAsync(path, MapExporter.ToBinary(tileMap)),
            ExportFormat.Csv => File.WriteAllTextAsync(path, MapCsv.Write(tileMap)),
            _ => File.WriteAllTextAsync(path, MapExporter.ToAssembler(tileMap, request.Style)),
        });

        // The ROM, which is the only thing here that knows about the tile set: the map is
        // indices, and what draws them comes out of another document.
        if (request.Rom is { } dialect)
        {
            yield return new ExportPiece(
                ExampleRom.Suffix,
                path => File.WriteAllTextAsync(
                    path,
                    ExampleRom.ForMap(
                        tileMap,
                        Bands(),
                        map.ColorPalette,
                        dialect,
                        request.Stem,
                        request.Format == ExportFormat.Binary)),
                ExampleRom.Extension);
        }
    }

    /// <summary>The tile set of each band, which is one when the map is not banded.</summary>
    private IReadOnlyList<TileSet> Bands() =>
        [.. Enumerable.Range(0, map.BandCount).Select(map.TileSetOfBand)];

    /// <summary>Nothing to ask: a map always exports.</summary>
    public Task<bool> ReadyAsync() => Task.FromResult(true);

    /// <summary>
    /// That the empty cells have come out with the filler tile.
    /// </summary>
    /// <remarks>
    /// The name table of the VDP always draws something and a byte has no room for a hole, so
    /// what was empty in the editor is a tile in the file. Better said than written in silence.
    /// Not for the csv, which does have a way of saying empty and keeps them.
    /// </remarks>
    public (string Title, string Body)? Note(ExportRequest request)
    {
        if (request.Format == ExportFormat.Csv || !HasEmptyCells(map.Map))
            return null;

        return (Text["ExportedMapTitle"], Text.Format("ExportedMapEmptyBody", map.Map.EmptyTile));
    }

    private static bool HasEmptyCells(TileMap map)
    {
        TileGrid flat = map.Flatten();

        for (int row = 0; row < flat.Height; row++)
        {
            for (int column = 0; column < flat.Width; column++)
            {
                if (flat[column, row] is null)
                    return true;
            }
        }

        return false;
    }
}
