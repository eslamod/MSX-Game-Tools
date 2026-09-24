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

    /// <summary>
    /// En minúsculas y con guiones bajos, como los otros tres.
    /// </summary>
    /// <remarks>
    /// Es además la etiqueta que el exportador escribe dentro del fichero —un mapa llamado
    /// «Nivel 1» sale con <c>nivel_1_map:</c> dentro—, así que el fichero y lo que lleva
    /// dentro se llaman igual. Antes proponía «Nivel 1.bin», con el espacio y la mayúscula.
    /// </remarks>
    public string Stem => AsmLabel.Of(map.Map.Name);

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

    /// <summary>
    /// A map can be cut into screens, and it starts on the one of what is selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the games of fixed screens, which draw the whole map in one go and then load a screen
    /// every time a door is crossed. What a screen measures is in the preferences, the same one
    /// the editor draws the grid by.
    /// </para>
    /// <para>
    /// What is selected and not where the mouse is: the selection is the last thing said on
    /// purpose about where one is working, and it is still there when the panel opens, while the
    /// mouse stays wherever it left the canvas on the way to the menu.
    /// </para>
    /// </remarks>
    public ScreenNumber? FirstScreen
    {
        get
        {
            if (map.Selection is not { } region
                || map.ScreenCellsWide == 0
                || map.ScreenCellsHigh == 0)
            {
                return new ScreenNumber(1, 1);
            }

            return new ScreenNumber(
                (region.Left / map.ScreenCellsWide) + 1,
                (region.Top / map.ScreenCellsHigh) + 1);
        }
    }

    /// <summary>
    /// What stops it from being cut, said before anything is written.
    /// </summary>
    /// <remarks>
    /// Without this, a screen that does not fit the super tile leaves the list of files empty and
    /// without an explanation: it would show that nothing is going out, but not why.
    /// </remarks>
    public string? Problem(ExportRequest request)
    {
        if (request.Screens is null)
            return null;

        if (map.ScreenCellsWide == 0 || map.ScreenCellsHigh == 0)
        {
            return Text.Format(
                "ExportScreensNotSuper",
                map.Preferences.ScreenWidth,
                map.Preferences.ScreenHeight,
                map.CellTilesWidth,
                map.CellTilesHeight);
        }

        // A screen bigger than a page cannot go whole into any segment. It takes an absurd
        // screen —128x96 cells in pages of 8K— but then the index would be wrong, not short.
        if (request.Screens is { Only: null, Index: true, PageSize: > 0 } paged
            && MapScreens.Bytes(map.ScreenCellsWide, map.ScreenCellsHigh, paged.Header) is var bytes
            && bytes > paged.PageSize)
        {
            return Text.Format("ExportScreenTooBig", bytes, paged.PageSize / 1024);
        }

        if (request.Screens is { Only: { } one })
        {
            // Asked for by its number, an empty one does go out: that is the one asked for. The only
            // thing it cannot do is not exist, and that is what the rectangle knows about.
            return Rectangle(one) is null
                ? Text.Format(
                    "ExportScreenMissing",
                    one,
                    MapScreens.Count(map.Map.Width, map.ScreenCellsWide),
                    MapScreens.Count(map.Map.Height, map.ScreenCellsHigh))
                : null;
        }

        // Without cutting anything: if there is not one tile on any layer there is no screen to
        // save, and that sum is much cheaper than cutting the whole map.
        return map.Map.Layers.All(layer => layer.Grid.IsEmpty) ? Text["ExportScreensEmpty"] : null;
    }

    /// <summary>
    /// The files of the index, in whichever of its forms.
    /// </summary>
    /// <remarks>
    /// Only with the whole batch by screens: that is what writes an index, and what can leave the
    /// one of an earlier export behind. A single screen leaves the index in the folder as it was
    /// on purpose —it still holds—, and a whole map has none. With the index box off the table
    /// is in the family as well: then all of it is left over from before.
    /// </remarks>
    public IReadOnlyList<string> Family(ExportRequest request)
    {
        if (request.Screens is not { Only: null })
            return [];

        string stem = request.Stem;
        string extension = MapScreens.IndexExtension;

        return
        [
            $"{stem}{MapScreens.IndexSuffix}{extension}",
            $"{stem}{MapScreens.DataSuffix}{extension}",
            $"{stem}{MapScreens.PageSuffix}*{extension}",
        ];
    }

    /// <summary>
    /// Marks on the map the screen that is about to be written.
    /// </summary>
    /// <remarks>
    /// Only the one of a single screen: of the whole batch what gets written is almost the whole
    /// map, and marking all of it would say nothing.
    /// </remarks>
    public void Preview(ExportRequest? request) =>
        map.ShowScreen(request?.Screens?.Only is { } one ? Rectangle(one) : null);

    /// <summary>
    /// The cells that screen takes, or nothing if there is no screen there.
    /// </summary>
    /// <remarks>
    /// One single place decides which screen exists, and the two things that depend on it come
    /// out of it: what is marked on the map and what is said when it cannot be exported.
    /// </remarks>
    private MapRegion? Rectangle(ScreenNumber one)
    {
        int wide = map.ScreenCellsWide;
        int high = map.ScreenCellsHigh;

        bool exists = wide > 0 && high > 0
                      && one.Column >= 1 && one.Row >= 1
                      && one.Column <= MapScreens.Count(map.Map.Width, wide)
                      && one.Row <= MapScreens.Count(map.Map.Height, high);

        return exists
            ? new MapRegion((one.Column - 1) * wide, (one.Row - 1) * high, wide, high)
            : null;
    }

    public IEnumerable<ExportPiece> Pieces(ExportRequest request)
    {
        TileMap tileMap = map.Map;

        if (request.Screens is { } split)
        {
            if (split.Only is not null)
            {
                if (Picked(request, split) is { } one)
                    yield return ScreenPiece(one, tileMap, request, split);

                yield break;
            }

            IReadOnlyList<MapScreen> screens = Screens(request);

            // The index first, so that it heads the list: with a hundred screens the list is cut
            // after the first dozen, and the two files that are not screens would end up in the
            // count of the ones that do not fit.
            // And only with screens to point at: with none —a size that does not fit the super
            // tile, an empty map— there is nothing to index and the problem is said apart.
            if (split.Index && screens.Count > 0)
            {
                foreach (ExportPiece piece in IndexPieces(screens, request, split))
                    yield return piece;
            }

            foreach (MapScreen screen in screens)
                yield return ScreenPiece(screen, tileMap, request, split);

            // And nothing else: the example ROM loads a map, not a folder of screens.
            yield break;
        }

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

    /// <summary>
    /// The screens with something drawn on them, which are the ones that go out.
    /// </summary>
    /// <remarks>
    /// Cut every time it is asked and not kept: the panel stays open while drawing, and a list
    /// kept would say the files of a while ago.
    /// </remarks>
    private IReadOnlyList<MapScreen> Screens(ExportRequest request) =>
        MapScreens.Of(map.Map, map.ScreenCellsWide, map.ScreenCellsHigh, request.Stem);

    /// <summary>The screen asked for by its number, or nothing if there is no screen there.</summary>
    private MapScreen? Picked(ExportRequest request, ScreenSplit split) =>
        split.Only is { } one
            ? MapScreens.At(
                map.Map,
                map.ScreenCellsWide,
                map.ScreenCellsHigh,
                request.Stem,
                one.Column - 1,
                one.Row - 1)
            : null;

    /// <summary>
    /// A screen, with its column and its row in the name.
    /// </summary>
    /// <remarks>
    /// Column first, like the label of the editor: the screen read there as «3-1» is the file
    /// that ends in <c>_3_1</c>.
    /// </remarks>
    private static ExportPiece ScreenPiece(
        MapScreen screen, TileMap map, ExportRequest request, ScreenSplit split) =>
        new(
            $"_{screen.Column + 1}_{screen.Row + 1}",
            path => request.Format == ExportFormat.Binary
                ? File.WriteAllBytesAsync(path, MapExporter.ToBinary(screen.Map, split.Header))
                : File.WriteAllTextAsync(
                    path,
                    MapExporter.ToAssembler(
                        screen.Map, request.Style, split.Header, MapScreens.Notes(screen, map))));

    /// <summary>
    /// The table of where each screen starts, and the file that brings them in.
    /// </summary>
    /// <remarks>
    /// Two files and not one: in a megaROM the screens are spread over pages, and a table with
    /// the includes underneath would put them all in one place.
    /// </remarks>
    private IEnumerable<ExportPiece> IndexPieces(
        IReadOnlyList<MapScreen> screens, ExportRequest request, ScreenSplit split)
    {
        TileMap tileMap = map.Map;

        if (split.PageSize > 0)
        {
            foreach (ExportPiece piece in PagedPieces(screens, request, split))
                yield return piece;

            yield break;
        }

        yield return new ExportPiece(
            MapScreens.IndexSuffix,
            path => File.WriteAllTextAsync(
                path,
                MapScreens.Index(
                    tileMap,
                    screens,
                    map.ScreenCellsWide,
                    map.ScreenCellsHigh,
                    request.Stem,
                    request.Style,
                    split.Header)),
            MapScreens.IndexExtension);

        yield return new ExportPiece(
            MapScreens.DataSuffix,
            path => File.WriteAllTextAsync(
                path,
                MapScreens.Data(
                    tileMap, screens, request.Stem, request.Style, request.Format == ExportFormat.Binary)),
            MapScreens.IndexExtension);
    }

    /// <summary>
    /// The table with a page and an offset per screen, and a file per page.
    /// </summary>
    /// <remarks>
    /// A file per page and not one with them all: each one goes to its segment, and that is the
    /// piece that gets moved.
    /// </remarks>
    private IEnumerable<ExportPiece> PagedPieces(
        IReadOnlyList<MapScreen> screens, ExportRequest request, ScreenSplit split)
    {
        TileMap tileMap = map.Map;
        int wide = map.ScreenCellsWide;
        int high = map.ScreenCellsHigh;
        int bytes = MapScreens.Bytes(wide, high, split.Header);
        bool binary = request.Format == ExportFormat.Binary;

        IReadOnlyList<IReadOnlyList<MapScreen>> pages = MapScreens.Pages(screens, bytes, split.PageSize);

        yield return new ExportPiece(
            MapScreens.IndexSuffix,
            path => File.WriteAllTextAsync(
                path,
                MapScreens.PagedIndex(
                    tileMap, pages, wide, high, request.Stem, request.Style, split.Header, split.PageSize)),
            MapScreens.IndexExtension);

        for (int page = 0; page < pages.Count; page++)
        {
            int number = page;

            yield return new ExportPiece(
                $"{MapScreens.PageSuffix}{number}",
                path => File.WriteAllTextAsync(
                    path,
                    MapScreens.Page(
                        tileMap,
                        pages[number],
                        number,
                        request.Stem,
                        request.Style,
                        binary,
                        bytes,
                        split.PageSize)),
                MapScreens.IndexExtension);
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
        if (request.Screens is not null)
            return ScreensNote(request);

        if (request.Format == ExportFormat.Csv || !HasEmptyCells(map.Map))
            return null;

        return (Text["ExportedMapTitle"], Text.Format("ExportedMapEmptyBody", map.Map.EmptyTile));
    }

    /// <summary>
    /// How many screens went out, how many did not, and what filled what was missing.
    /// </summary>
    /// <remarks>
    /// The two things that do not show by looking at the folder: that files are missing on
    /// purpose —the holes of the drawing are not written— and that the screens at the edge carry
    /// filler that was not in the map.
    /// </remarks>
    private (string Title, string Body) ScreensNote(ExportRequest request)
    {
        TileMap tileMap = map.Map;

        if (request.Screens is { Only: not null } asked)
            return OneScreenNote(tileMap, request, asked);

        int written = Screens(request).Count;
        int all = MapScreens.Count(tileMap.Width, map.ScreenCellsWide)
                  * MapScreens.Count(tileMap.Height, map.ScreenCellsHigh);

        var lines = new List<string> { Text.Format("ExportedScreensBody", written, all) };

        if (all > written)
            lines.Add(Text["ExportedScreensSkipped"]);

        if (MapScreens.Pads(tileMap, map.ScreenCellsWide, map.ScreenCellsHigh))
            lines.Add(Text.Format("ExportedScreensPadded", tileMap.EmptyTile));

        if (HasEmptyCells(tileMap))
            lines.Add(Text.Format("ExportedMapEmptyBody", tileMap.EmptyTile));

        return (Text["ExportedScreensTitle"], string.Join("\n\n", lines));
    }

    /// <summary>
    /// Of a single screen, what needs saying is whether it carries filler, and of what.
    /// </summary>
    /// <remarks>
    /// How many went out does not need saying —one, the one asked for— and neither does the bit
    /// about the empty ones: none was skipped here.
    /// </remarks>
    private (string Title, string Body) OneScreenNote(
        TileMap map, ExportRequest request, ScreenSplit split)
    {
        MapScreen? one = Picked(request, split);

        var lines = new List<string> { Text.Format("ExportedOneScreenBody", split.Only!) };

        if (one is { Padded: true })
            lines.Add(Text.Format("ExportedScreensPadded", map.EmptyTile));

        if (one is not null && HasEmptyCells(one.Map))
            lines.Add(Text.Format("ExportedMapEmptyBody", map.EmptyTile));

        if (one is not null && IndexMisses(request, one))
            lines.Add(Text["ExportedScreenNotInIndex"]);

        return (Text["ExportedScreensTitle"], string.Join("\n\n", lines));
    }

    /// <summary>
    /// Whether the folder holds an index of these screens that does not have the one just written.
    /// </summary>
    /// <remarks>
    /// It happens with a screen that was empty when all of them went out: it was not written, so
    /// the index has a 0 where it goes, and now that it exists the game would still not find it.
    /// Written alone, the index is not touched —it speaks of the whole batch— so it is said.
    /// </remarks>
    private static bool IndexMisses(ExportRequest request, MapScreen screen)
    {
        string index = Path.Combine(
            request.Folder, $"{request.Stem}{MapScreens.IndexSuffix}{MapScreens.IndexExtension}");

        try
        {
            // By the whole label: the one of 1-1 is inside the one of 11-1 as a piece of text.
            return File.Exists(index)
                   && !System.Text.RegularExpressions.Regex.IsMatch(
                       File.ReadAllText(index),
                       $@"\b{System.Text.RegularExpressions.Regex.Escape(MapScreens.Label(screen))}\b");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An index that cannot be read is not a reason to say anything about it.
            return false;
        }
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
