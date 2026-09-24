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
    /// Un mapa sí se puede partir en pantallas, y arranca por la de lo seleccionado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Para los juegos de pantallas fijas, que dibujan el mapa entero de una vez y luego cargan
    /// una pantalla cada vez que se cruza una puerta. Lo que mide una pantalla está en la
    /// configuración, que es la misma por la que el editor pinta la rejilla.
    /// </para>
    /// <para>
    /// Lo seleccionado y no por donde esté el ratón: la selección es lo último que se dijo a
    /// propósito sobre dónde se está trabajando y sigue ahí al abrir el panel, mientras que el
    /// ratón se queda por donde saliera del lienzo camino del menú.
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
    /// Lo que impide partirlo, dicho antes de escribir nada.
    /// </summary>
    /// <remarks>
    /// Sin esto, una pantalla que no cuadra con el supertile deja la lista de ficheros vacía y
    /// sin explicación: se vería que no va a salir nada, pero no por qué.
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
            // Pedida por su número, una vacía sí sale: es ésa la que se ha pedido. Lo único que
            // no puede es no existir, y de eso entiende el rectángulo.
            return Rectangle(one) is null
                ? Text.Format(
                    "ExportScreenMissing",
                    one,
                    MapScreens.Count(map.Map.Width, map.ScreenCellsWide),
                    MapScreens.Count(map.Map.Height, map.ScreenCellsHigh))
                : null;
        }

        // Sin recortar nada: si no hay un tile puesto en ninguna capa no hay pantalla que
        // salvar, y esa cuenta es mucho más barata que partir el mapa entero.
        return map.Map.Layers.All(layer => layer.Grid.IsEmpty) ? Text["ExportScreensEmpty"] : null;
    }

    /// <summary>
    /// Señala en el mapa la pantalla que se va a escribir.
    /// </summary>
    /// <remarks>
    /// Sólo la de una sola: de la tanda entera lo que se escribe es casi todo el mapa, y
    /// señalarlo entero no diría nada.
    /// </remarks>
    public void Preview(ExportRequest? request) =>
        map.ShowScreen(request?.Screens?.Only is { } one ? Rectangle(one) : null);

    /// <summary>
    /// Las celdas que ocupa esa pantalla, o nada si ahí no hay pantalla.
    /// </summary>
    /// <remarks>
    /// Un solo sitio decide qué pantalla existe, y de él salen las dos cosas que dependen de
    /// eso: lo que se señala en el mapa y lo que se dice cuando no se puede exportar.
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

            // Y nada más: la ROM de ejemplo carga un mapa, no una carpeta de pantallas.
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
    /// Las pantallas que llevan algo dibujado, que son las que salen.
    /// </summary>
    /// <remarks>
    /// Se recorta cada vez que se pregunta y no se guarda: el panel sigue abierto mientras se
    /// dibuja, y una lista guardada diría los ficheros de hace un rato.
    /// </remarks>
    private IReadOnlyList<MapScreen> Screens(ExportRequest request) =>
        MapScreens.Of(map.Map, map.ScreenCellsWide, map.ScreenCellsHigh, request.Stem);

    /// <summary>La pantalla pedida por su número, o nada si ahí no hay pantalla.</summary>
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
    /// Una pantalla, con su columna y su fila en el nombre.
    /// </summary>
    /// <remarks>
    /// Columna primero, como la etiqueta del editor: la pantalla que ahí se lee «3-1» es el
    /// fichero que acaba en <c>_3_1</c>.
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
    /// Cuántas pantallas han salido, cuántas no, y con qué se ha rellenado lo que faltaba.
    /// </summary>
    /// <remarks>
    /// Las dos cosas que no se ven mirando la carpeta: que faltan ficheros a propósito —los
    /// huecos del dibujo no se escriben— y que las pantallas del borde llevan relleno que no
    /// estaba en el mapa.
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
    /// De una sola pantalla lo que hace falta decir es de qué lleva relleno, si lleva.
    /// </summary>
    /// <remarks>
    /// Cuántas han salido no hace falta decirlo —una, la que se ha pedido— y lo de las vacías
    /// tampoco: aquí no se ha saltado ninguna.
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
