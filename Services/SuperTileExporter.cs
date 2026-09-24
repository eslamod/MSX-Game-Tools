using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Vuelca la tabla de supertiles de un juego de tiles.
/// </summary>
/// <remarks>
/// <para>
/// Tres bytes de cabecera —ancho, alto y cuántos— y después los números de tile de cada
/// supertile, de izquierda a derecha y de arriba abajo, que es el mismo orden en el que
/// van las celdas de un mapa y las líneas de un patrón. Así el cargador lee la cabecera,
/// multiplica y sabe cuánto ocupa el resto sin más cuentas.
/// </para>
/// <para>
/// La cantidad va en un byte con el convenio de que <b>0 son 256</b>. Una celda del mapa
/// es un byte, así que 256 es justo el tope de supertiles que un mapa puede nombrar; con
/// 256 cabiendo en el cuento, el byte se queda a cero y no hace falta un segundo.
/// </para>
/// <para>
/// Sale con el juego de tiles y no con el mapa aunque sea el mapa quien la indexa: los
/// supertiles son del juego, y todos los mapas dibujados con él comparten la misma tabla.
/// Sacándola con cada mapa se repetiría igual en todos.
/// </para>
/// <para>
/// Una celda de un supertile sin tile puesto sale como el tile 0. En la máquina no hay
/// huecos: toda celda de la tabla de nombres dibuja algo, así que el hueco tiene que
/// convertirse en algún número y el 0 es el que no hay que explicar.
/// </para>
/// </remarks>
public static class SuperTileExporter
{
    /// <summary>Bytes de cabecera: ancho, alto y cuántos.</summary>
    public const int HeaderBytes = 3;

    /// <summary>
    /// Supertiles que un mapa puede nombrar, que es lo que cabe en el byte de una celda.
    /// </summary>
    public const int MaxSuperTiles = 256;

    /// <summary>Lo que se pinta donde un supertile no tiene tile puesto.</summary>
    public const int EmptyTile = 0;

    private const int BytesPerLine = 8;

    /// <summary>Cuántos supertiles van a salir, que son los bloques que caben.</summary>
    public static int CountOf(TileSet tileSet) => Math.Min(tileSet.Blocks.Count, MaxSuperTiles);

    public static byte[] ToBinary(TileSet tileSet)
    {
        int count = CountOf(tileSet);
        int area = Math.Max(1, tileSet.SuperTileArea);
        byte[] bytes = new byte[HeaderBytes + (count * area)];

        bytes[0] = (byte)tileSet.SuperTileWidth;
        bytes[1] = (byte)tileSet.SuperTileHeight;

        // 256 se escribe como 0: es el tope y no cabe en el byte de otra forma.
        bytes[2] = (byte)(count == MaxSuperTiles ? 0 : count);

        int position = HeaderBytes;

        for (int index = 0; index < count; index++)
        {
            foreach (int tile in TilesOf(tileSet, tileSet.Blocks[index]))
                bytes[position++] = (byte)tile;
        }

        return bytes;
    }

    public static string ToAssembler(TileSet tileSet, AsmStyle? style = null)
    {
        string data = AsmStyle.Of(style?.Data).Data;

        int count = CountOf(tileSet);
        string label = AsmLabel.Of(tileSet.Name);
        var text = new StringBuilder();

        text.AppendLine($"; Supertile table - {tileSet.Name}");
        text.AppendLine($"; {count} supertiles of {tileSet.SuperTileWidth}x{tileSet.SuperTileHeight} tiles.");
        text.AppendLine("; Header: width, height and count, one byte each. A count of 0 means 256,");
        text.AppendLine("; which is the most a map can name because a map cell is one byte.");
        text.AppendLine("; Then the tile numbers of each supertile, left to right and top to bottom.");
        text.AppendLine($"; Empty cells are written as tile {EmptyTile}: the name table always draws something.");
        text.AppendLine($"; Size: {label}_supertiles_end - {label}_supertiles");
        text.AppendLine();
        text.AppendLine($"{label}_supertiles:");

        AppendBytes(
            text,
            [
                (byte)tileSet.SuperTileWidth,
                (byte)tileSet.SuperTileHeight,
                (byte)(count == MaxSuperTiles ? 0 : count),
            ],
            "size and count",
            data);

        text.AppendLine();

        for (int index = 0; index < count; index++)
        {
            TileBlock block = tileSet.Blocks[index];

            text.AppendLine($"{label}_supertile_{index}:    ; {block.Name}");
            AppendBytes(text, [.. TilesOf(tileSet, block).Select(tile => (byte)tile)], null, data);
        }

        text.AppendLine($"{label}_supertiles_end:");

        return text.ToString();
    }

    /// <summary>
    /// Los tiles de un supertile, por filas y siempre los que dice el tamaño del juego.
    /// </summary>
    /// <remarks>
    /// Se leen por el tamaño del juego y no por el del bloque: los bloques de un juego de
    /// supertiles miden todos lo mismo, pero uno traído de un fichero viejo podría no
    /// hacerlo, y entonces la tabla saldría descuadrada sin avisar.
    /// </remarks>
    private static IEnumerable<int> TilesOf(TileSet tileSet, TileBlock block)
    {
        for (int row = 0; row < tileSet.SuperTileHeight; row++)
        {
            for (int column = 0; column < tileSet.SuperTileWidth; column++)
                yield return block[column, row] ?? EmptyTile;
        }
    }

    private static void AppendBytes(
        StringBuilder text, IReadOnlyList<byte> bytes, string? what, string data)
    {
        string comment = what is null ? string.Empty : $"    ; {what}";

        for (int start = 0; start < bytes.Count; start += BytesPerLine)
        {
            IEnumerable<string> line = bytes
                .Skip(start)
                .Take(BytesPerLine)
                .Select(value => $"{SpriteBankExporter.HexPrefix}{value:X2}");

            text.AppendLine($"    {data}  {string.Join(",", line)}{comment}");
        }
    }
}
