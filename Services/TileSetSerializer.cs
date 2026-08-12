using System.Text.Json;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Formato de fichero de un juego de tiles: sus patrones y la paleta con la que se
/// dibujaron.
/// </summary>
/// <remarks>
/// <para>
/// Cada tile va con sus dos tablas en dos cadenas de 16 dígitos hexadecimales: ocho
/// bytes de máscara de bits y ocho de color, exactamente como van al VDP. Así lo que se
/// guarda y lo que se exporta son los mismos números y no hay conversión que revisar.
/// </para>
/// <para>
/// Sólo se escriben los tiles que se han tocado. Los 256 existen siempre, y guardar los
/// que están vacíos serían veinte kilobytes de ceros por juego; con el índice delante,
/// un tileset a medio hacer ocupa lo que ocupa lo dibujado.
/// </para>
/// <para>
/// La paleta va embebida por lo mismo que en un banco de sprites: los tiles guardan
/// índices, no colores, y sin ella el juego se abriría con los colores que hubiera
/// puestos en ese momento.
/// </para>
/// <para>
/// Los bloques van aquí dentro y no en un fichero aparte: son números de tile, y esos
/// números sólo significan algo con este juego delante. Cada uno se escribe como una
/// lista de filas, dos dígitos por celda y <c>..</c> donde no hay nada; así el tamaño del
/// bloque es la forma de la lista y no hace falta guardarlo aparte.
/// </para>
/// </remarks>
public static class TileSetSerializer
{
    /// <summary>La 2 añade los bloques. Un fichero de la 1 se sigue abriendo, sin ellos.</summary>
    public const int FormatVersion = 2;

    /// <summary>Ocho bytes por tabla y dos dígitos por byte.</summary>
    private const int Digits = Tile.Rows * 2;

    public static string Serialize(TileSet tileSet, ColorPalette palette, int borderColorIndex = 1) =>
        JsonSerializer.Serialize(ToFile(tileSet, palette, borderColorIndex), PaletteSerializer.Options);

    /// <exception cref="FileFormatException">El contenido no es un juego de tiles válido.</exception>
    public static LoadedTileSet Deserialize(string json)
    {
        TileSetFile? file;

        try
        {
            file = JsonSerializer.Deserialize<TileSetFile>(json, PaletteSerializer.Options);
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
                $"El juego de tiles usa la versión {file.Version} del formato y esta versión del editor sólo entiende hasta la {FormatVersion}.");
        }

        if (file.Palette is null)
            throw new FileFormatException("El fichero no trae la paleta del juego de tiles.");

        var tileSet = new TileSet(string.IsNullOrWhiteSpace(file.Name) ? "Tiles sin nombre" : file.Name);

        foreach (TileFile tile in file.Tiles ?? [])
            ReadTile(tile, tileSet);

        foreach (BlockFile block in file.Blocks ?? [])
            tileSet.Blocks.Add(ReadBlock(block));

        int border = file.BorderColor is >= 1 and < ColorPalette.Size ? file.BorderColor : 1;

        return new LoadedTileSet(tileSet, PaletteSerializer.FromFile(file.Palette), border);
    }

    private static TileSetFile ToFile(TileSet tileSet, ColorPalette palette, int borderColorIndex) => new(
        FormatVersion,
        tileSet.Name,
        borderColorIndex,
        PaletteSerializer.ToFile(palette),
        [.. Drawn(tileSet)],
        [.. tileSet.Blocks.Select(ToFile)]);

    private static BlockFile ToFile(TileBlock block) => new(
        block.Name,
        [.. Enumerable.Range(0, block.Height).Select(row => TileGridText.Row(block.Grid, row))]);

    /// <summary>Los tiles que se han tocado, con su número delante.</summary>
    private static IEnumerable<TileFile> Drawn(TileSet tileSet)
    {
        for (int index = 0; index < tileSet.ListOfTiles.Count; index++)
        {
            Tile tile = tileSet.ListOfTiles[index];

            if (IsEmpty(tile))
                continue;

            yield return new TileFile(
                index,
                Hex(tile, row => row.PatternByte),
                Hex(tile, row => row.ColorByte));
        }
    }

    /// <summary>Un tile recién creado: sin bits y con los colores de partida.</summary>
    private static bool IsEmpty(Tile tile)
    {
        var fresh = new TileRow();

        return tile.ArrayTileRows.All(row =>
            row.PatternByte == 0 && row.ColorByte == fresh.ColorByte);
    }

    private static string Hex(Tile tile, Func<TileRow, byte> byteOf) =>
        string.Concat(tile.ArrayTileRows.Select(row => byteOf(row).ToString("X2")));

    private static void ReadTile(TileFile file, TileSet tileSet)
    {
        if ((uint)file.Index >= (uint)tileSet.ListOfTiles.Count)
        {
            throw new FileFormatException(
                $"El fichero trae el tile {file.Index}, y un juego sólo tiene {tileSet.ListOfTiles.Count}.");
        }

        byte[] pattern = ParseBytes(file.Pattern, file.Index, "la máscara de bits");
        byte[] colors = ParseBytes(file.Colors, file.Index, "los colores");

        Tile tile = tileSet.ListOfTiles[file.Index];

        for (int row = 0; row < Tile.Rows; row++)
        {
            TileRow line = tile.ArrayTileRows[row];

            for (int column = 0; column < TileRow.Columns; column++)
                line.ArrayPattern[column] = (pattern[row] & (1 << (TileRow.Columns - 1 - column))) != 0;

            line.ForeColor = colors[row] >> 4;
            line.BackColor = colors[row] & 0x0F;
        }
    }

    private static byte[] ParseBytes(string? hex, int index, string what)
    {
        if (hex is null || hex.Length != Digits)
        {
            throw new FileFormatException(
                $"En el tile {index}, {what} debe traer {Digits} dígitos hexadecimales; trae «{hex}».");
        }

        byte[] bytes = new byte[Tile.Rows];

        for (int row = 0; row < Tile.Rows; row++)
        {
            int high = PaletteSerializer.HexDigit(hex[row * 2])
                       ?? throw new FileFormatException(
                           $"En el tile {index}, {what} tiene un dígito que no es hexadecimal: «{hex[row * 2]}».");

            int low = PaletteSerializer.HexDigit(hex[(row * 2) + 1])
                      ?? throw new FileFormatException(
                          $"En el tile {index}, {what} tiene un dígito que no es hexadecimal: «{hex[(row * 2) + 1]}».");

            bytes[row] = (byte)((high << 4) | low);
        }

        return bytes;
    }

    private static TileBlock ReadBlock(BlockFile file)
    {
        var block = new TileBlock(file.Name ?? string.Empty);
        IReadOnlyList<string> rows = file.Rows ?? [];

        if (rows.Count == 0)
            return block;

        if (rows.Count > TileBlock.MaxSide)
        {
            throw new FileFormatException(
                $"El bloque «{block.Name}» trae {rows.Count} filas y como mucho puede tener {TileBlock.MaxSide}.");
        }

        int width = Width(rows, block.Name);

        // Antes de poner nada, para que el tamaño sea el del fichero y no hasta donde
        // llegue el ultimo tile: un bloque con la esquina vacia sigue midiendo lo suyo.
        block.Width = width;
        block.Height = rows.Count;

        for (int row = 0; row < rows.Count; row++)
        {
            for (int column = 0; column < width; column++)
                block.Set(column, row, TileGridText.Cell(rows[row], column, Describe(block)));
        }

        return block;
    }

    /// <summary>El ancho que dicen las filas, comprobando que todas midan lo mismo.</summary>
    private static int Width(IReadOnlyList<string> rows, string name)
    {
        int width = TileGridText.WidthOf(rows[0], $"el bloque «{name}»");

        if (rows.Any(row => row.Length != rows[0].Length))
            throw new FileFormatException($"En el bloque «{name}», las filas no miden todas lo mismo.");

        if (width > TileBlock.MaxSide)
        {
            throw new FileFormatException(
                $"El bloque «{name}» mide {width} de ancho y como mucho puede medir {TileBlock.MaxSide}.");
        }

        return width;
    }

    private static string Describe(TileBlock block) => $"el bloque «{block.Name}»";

    private sealed record TileSetFile(
        int Version,
        string? Name,
        int BorderColor,
        PaletteSerializer.PaletteFile? Palette,
        IReadOnlyList<TileFile>? Tiles,
        IReadOnlyList<BlockFile>? Blocks);

    private sealed record TileFile(int Index, string? Pattern, string? Colors);

    private sealed record BlockFile(string? Name, IReadOnlyList<string>? Rows);
}

/// <summary>Lo que sale de leer un fichero de tiles.</summary>
public sealed record LoadedTileSet(TileSet TileSet, ColorPalette Palette, int BorderColorIndex);
