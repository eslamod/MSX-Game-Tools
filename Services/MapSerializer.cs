using System.Text.Json;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Formato de fichero de un mapa: su tamaño, sus capas y con qué juego de tiles se dibuja.
/// </summary>
/// <remarks>
/// <para>
/// Cada capa guarda sólo las filas que tienen algún tile, con su número delante. Un mapa
/// recién creado de 512x512 serían medio millón de celdas vacías escritas en el fichero;
/// así ocupa lo que ocupa lo dibujado, igual que hacemos con los tiles de un juego.
/// </para>
/// <para>
/// El juego de tiles va por nombre y no embebido, al revés que la paleta en un tileset.
/// Un mapa son números de tile y el juego puede pesar lo suyo; además se edita aparte y
/// se quiere que el mapa vea los cambios, no una copia congelada del día que se guardó.
/// </para>
/// </remarks>
public static class MapSerializer
{
    /// <summary>La 2 señala el juego de tiles por identidad. Un fichero de la 1 se abre igual.</summary>
    public const int FormatVersion = 2;

    public static string Serialize(TileMap map) =>
        JsonSerializer.Serialize(ToFile(map), PaletteSerializer.Options);

    /// <exception cref="FileFormatException">El contenido no es un mapa válido.</exception>
    public static TileMap Deserialize(string json)
    {
        MapFile? file;

        try
        {
            file = JsonSerializer.Deserialize<MapFile>(json, PaletteSerializer.Options);
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
                $"El mapa usa la versión {file.Version} del formato y esta versión del editor sólo entiende hasta la {FormatVersion}.");
        }

        if (file.Width is < 1 or > TileMap.MaxSide || file.Height is < 1 or > TileMap.MaxSide)
        {
            throw new FileFormatException(
                $"El mapa dice medir {file.Width}x{file.Height}, y un lado tiene que estar entre 1 y {TileMap.MaxSide}.");
        }

        var map = new TileMap(
            string.IsNullOrWhiteSpace(file.Name) ? "Unnamed map" : file.Name,
            file.Width,
            file.Height)
        {
            // Vacío en los ficheros de la versión 1: entonces manda el nombre, y el mapa
            // se queda con la identidad del juego la próxima vez que se guarde.
            TileSetId = file.TileSetId ?? Guid.Empty,
            TileSetName = file.TileSet ?? string.Empty,
        };

        if (file.BackgroundColor is >= 0 and < ColorPalette.Size)
            map.BackgroundColorIndex = file.BackgroundColor;

        if (file.EmptyTile is >= 0 and < TileSet.TileCount)
            map.EmptyTile = file.EmptyTile;

        map.Layers.Clear();

        foreach (LayerFile layer in file.Layers ?? [])
            map.Layers.Add(ReadLayer(layer, map));

        // Un mapa sin capas no se puede editar: al menos una, como al crearlo.
        if (map.Layers.Count == 0)
            map.AddLayer();

        return map;
    }

    private static MapFile ToFile(TileMap map) => new(
        FormatVersion,
        map.Name,
        map.Width,
        map.Height,
        map.BackgroundColorIndex,
        map.TileSetName,
        map.TileSetId == Guid.Empty ? null : map.TileSetId,
        map.EmptyTile,
        [.. map.Layers.Select(ToFile)]);

    private static LayerFile ToFile(MapLayer layer) => new(
        layer.Name,
        layer.IsVisible,
        layer.IsLocked,
        [.. Drawn(layer.Grid)]);

    /// <summary>Las filas con algo puesto, con su número delante.</summary>
    private static IEnumerable<RowFile> Drawn(TileGrid grid)
    {
        for (int row = 0; row < grid.Height; row++)
        {
            if (!TileGridText.IsEmptyRow(grid, row))
                yield return new RowFile(row, TileGridText.Row(grid, row));
        }
    }

    private static MapLayer ReadLayer(LayerFile file, TileMap map)
    {
        var layer = new MapLayer(
            string.IsNullOrWhiteSpace(file.Name) ? "Capa" : file.Name,
            map.Width,
            map.Height)
        {
            IsVisible = file.Visible,
            IsLocked = file.Locked,
        };

        string what = $"la capa «{layer.Name}»";

        foreach (RowFile row in file.Rows ?? [])
        {
            if ((uint)row.Index >= (uint)map.Height)
            {
                throw new FileFormatException(
                    $"En {what}, el fichero trae la fila {row.Index} y el mapa sólo tiene {map.Height}.");
            }

            int width = TileGridText.WidthOf(row.Tiles ?? string.Empty, what);

            if (width != map.Width)
            {
                throw new FileFormatException(
                    $"En {what}, la fila {row.Index} trae {width} celdas y el mapa mide {map.Width} de ancho.");
            }

            TileGridText.ReadInto(row.Tiles!, layer.Grid, row.Index, what);
        }

        return layer;
    }

    private sealed record MapFile(
        int Version,
        string? Name,
        int Width,
        int Height,
        int BackgroundColor,
        string? TileSet,
        Guid? TileSetId,
        int EmptyTile,
        IReadOnlyList<LayerFile>? Layers);

    private sealed record LayerFile(
        string? Name,
        bool Visible,
        bool Locked,
        IReadOnlyList<RowFile>? Rows);

    private sealed record RowFile(int Index, string? Tiles);
}
