using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Vuelca un mapa a la tabla de nombres que espera el VDP, con su tamaño delante.
/// </summary>
/// <remarks>
/// <para>
/// Cuatro bytes de cabecera y luego el mapa por filas, de izquierda a derecha y de arriba
/// abajo, igual que la tabla de nombres. La cabecera son dos bytes para el ancho y dos
/// para el alto, en el orden del Z80 —byte bajo primero—, así que en la máquina se leen
/// con un <c>ld hl,(mapa)</c> y a correr.
/// </para>
/// <para>
/// Dos bytes por lado y no uno porque un mapa horizontal de diez pantallas ya son 320
/// columnas. Con el tope de 1024 sobran seis bits en cada uno, que quedan libres para
/// banderas si algún día hacen falta.
/// </para>
/// <para>
/// El mapa sale <b>aplastado</b> y sin huecos: en la máquina no hay capas, hay una tabla
/// de nombres, y toda celda dibuja algo. Donde el editor no tiene nada se escribe el tile
/// de relleno del mapa.
/// </para>
/// </remarks>
public static class MapExporter
{
    /// <summary>Bytes de cabecera: dos por el ancho y dos por el alto.</summary>
    public const int HeaderBytes = 4;

    public static byte[] ToBinary(TileMap map)
    {
        TileGrid flat = map.Flatten();
        byte[] bytes = new byte[HeaderBytes + (flat.Width * flat.Height)];

        WriteWord(bytes, 0, flat.Width);
        WriteWord(bytes, 2, flat.Height);

        int position = HeaderBytes;

        for (int row = 0; row < flat.Height; row++)
        {
            for (int column = 0; column < flat.Width; column++)
                bytes[position++] = (byte)(flat[column, row] ?? map.EmptyTile);
        }

        return bytes;
    }

    public static string ToAssembler(TileMap map)
    {
        TileGrid flat = map.Flatten();
        string label = SpriteBankExporter.LabelOf(map.Name);

        var text = new StringBuilder();

        text.AppendLine($"; Map name table - {map.Name}");
        text.AppendLine($"; {flat.Width}x{flat.Height} tiles, {flat.Width * flat.Height} bytes after the header");
        text.AppendLine("; Header: width and height, 2 bytes each, low byte first.");
        text.AppendLine($"; Empty cells are written as tile {map.EmptyTile}: the name table always draws something.");
        text.AppendLine($"; Size: {label}_map_end - {label}_map");
        text.AppendLine();
        text.AppendLine($"{label}_map:");

        // La cabecera tambien en .db y no en .dw: asi el fichero no depende de que el
        // ensamblador tenga la directiva, y todos los bytes se leen igual.
        AppendBytes(text, [.. Word(flat.Width), .. Word(flat.Height)], "size");
        text.AppendLine();

        for (int row = 0; row < flat.Height; row++)
        {
            IEnumerable<byte> bytes = Enumerable
                .Range(0, flat.Width)
                .Select(column => (byte)(flat[column, row] ?? map.EmptyTile));

            AppendBytes(text, [.. bytes], $"fila {row}");
        }

        text.AppendLine($"{label}_map_end:");

        return text.ToString();
    }

    /// <summary>
    /// Lee un binario como un mapa de una sola capa.
    /// </summary>
    /// <remarks>
    /// Todas las celdas traen tile, así que no vuelve ninguna vacía: la ida y vuelta
    /// conserva lo que se ve en la máquina, no qué celdas habías dejado sin poner.
    /// </remarks>
    /// <exception cref="FileFormatException">El contenido no es un mapa válido.</exception>
    public static TileMap FromBinary(byte[] bytes, string name)
    {
        if (bytes.Length < HeaderBytes)
        {
            throw new FileFormatException(
                $"El fichero tiene {bytes.Length} bytes y la cabecera con el tamaño ya son {HeaderBytes}.");
        }

        int width = ReadWord(bytes, 0);
        int height = ReadWord(bytes, 2);

        if (width is < 1 or > TileMap.MaxSide || height is < 1 or > TileMap.MaxSide)
        {
            throw new FileFormatException(
                $"La cabecera dice que el mapa mide {width}x{height}, y un lado tiene que estar "
                + $"entre 1 y {TileMap.MaxSide}. ¿Seguro que es un mapa?");
        }

        int expected = HeaderBytes + (width * height);

        if (bytes.Length != expected)
        {
            throw new FileFormatException(
                $"La cabecera dice {width}x{height}, que son {expected} bytes con ella, "
                + $"y el fichero tiene {bytes.Length}.");
        }

        var map = new TileMap(name, width, height);
        TileGrid grid = map.Layers[0].Grid;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
                grid[column, row] = bytes[HeaderBytes + (row * width) + column];
        }

        return map;
    }

    /// <summary>Byte bajo primero, que es como lee las palabras el Z80.</summary>
    private static void WriteWord(byte[] bytes, int position, int value)
    {
        bytes[position] = (byte)(value & 0xFF);
        bytes[position + 1] = (byte)(value >> 8);
    }

    /// <inheritdoc cref="WriteWord"/>
    private static int ReadWord(byte[] bytes, int position) => bytes[position] | (bytes[position + 1] << 8);

    /// <summary>Los dos bytes de una palabra, el bajo primero.</summary>
    private static byte[] Word(int value) => [(byte)(value & 0xFF), (byte)(value >> 8)];

    /// <summary>Una fila por línea, con su número al lado para poder buscarla.</summary>
    private static void AppendBytes(StringBuilder text, byte[] bytes, string what)
    {
        IEnumerable<string> values = bytes.Select(
            value => $"{SpriteBankExporter.HexPrefix}{value:X2}");

        text.AppendLine($"    {SpriteBankExporter.DataDirective}  {string.Join(",", values)}    ; {what}");
    }
}
