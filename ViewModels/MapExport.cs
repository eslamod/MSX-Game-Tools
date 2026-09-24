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

        if (request.Screens is { Only: { } one })
        {
            int columns = MapScreens.Count(map.Map.Width, map.ScreenCellsWide);
            int rows = MapScreens.Count(map.Map.Height, map.ScreenCellsHigh);

            // Pedida por su número, una vacía sí sale: es ésa la que se ha pedido. Lo único que
            // no puede es no existir.
            return one.Column < 1 || one.Row < 1 || one.Column > columns || one.Row > rows
                ? Text.Format("ExportScreenMissing", one, columns, rows)
                : null;
        }

        // Sin recortar nada: si no hay un tile puesto en ninguna capa no hay pantalla que
        // salvar, y esa cuenta es mucho más barata que partir el mapa entero.
        return map.Map.Layers.All(layer => layer.Grid.IsEmpty) ? Text["ExportScreensEmpty"] : null;
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

            foreach (MapScreen screen in Screens(request))
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

        return (Text["ExportedScreensTitle"], string.Join("\n\n", lines));
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
