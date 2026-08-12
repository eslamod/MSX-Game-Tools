using System.Globalization;
using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Un mapa como csv de números de tile, para llevárselo a otra herramienta y traerlo.
/// </summary>
/// <remarks>
/// <para>
/// Sale el mapa <b>aplastado</b>: una sola rejilla, que es lo que hay en la máquina. Las
/// capas son ayuda de edición y en un csv no cabrían sin inventarse un formato propio.
/// </para>
/// <para>
/// La celda vacía se escribe <c>-1</c>, que es lo que usa Tiled, así que sus ficheros se
/// abren aquí y los de aquí se abren allí. En el binario habrá que elegir un tile de
/// relleno porque un byte no tiene sitio para el hueco, pero en texto sí lo hay.
/// </para>
/// </remarks>
public static class MapCsv
{
    /// <summary>Lo que se escribe donde no hay tile en ninguna capa.</summary>
    public const int EmptyCell = -1;

    public static string Write(TileMap map)
    {
        TileGrid flat = map.Flatten();
        var text = new StringBuilder();

        for (int row = 0; row < flat.Height; row++)
        {
            for (int column = 0; column < flat.Width; column++)
            {
                if (column > 0)
                    text.Append(',');

                text.Append(flat[column, row] ?? EmptyCell);
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Lee un csv como un mapa de una sola capa.
    /// </summary>
    /// <remarks>
    /// Se admite la coma al final de cada fila porque Tiled la escribe, y las líneas en
    /// blanco del final porque las deja cualquier editor de texto.
    /// </remarks>
    /// <exception cref="FileFormatException">El contenido no es un mapa válido.</exception>
    public static TileMap Read(string csv, string name)
    {
        string[] lines = csv
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(line => line.Trim().Length > 0)
            .ToArray();

        if (lines.Length == 0)
            throw new FileFormatException("El fichero no tiene ninguna fila.");

        if (lines.Length > TileMap.MaxSide)
        {
            throw new FileFormatException(
                $"El fichero trae {lines.Length} filas y un mapa puede tener {TileMap.MaxSide}.");
        }

        int[][] rows = [.. lines.Select((line, index) => ParseRow(line, index))];
        int width = rows[0].Length;

        for (int row = 1; row < rows.Length; row++)
        {
            if (rows[row].Length != width)
            {
                throw new FileFormatException(
                    $"La fila {row} trae {rows[row].Length} celdas y la primera trae {width}: "
                    + "todas las filas tienen que medir lo mismo.");
            }
        }

        if (width > TileMap.MaxSide)
        {
            throw new FileFormatException(
                $"El fichero trae {width} columnas y un mapa puede tener {TileMap.MaxSide}.");
        }

        var map = new TileMap(name, width, rows.Length);
        TileGrid grid = map.Layers[0].Grid;

        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < width; column++)
                grid[column, row] = rows[row][column] == EmptyCell ? null : rows[row][column];
        }

        return map;
    }

    private static int[] ParseRow(string line, int row)
    {
        string[] cells = line.Trim().Split(',');

        // Tiled cierra cada fila con una coma, y eso deja una celda vacia al final.
        if (cells.Length > 1 && cells[^1].Trim().Length == 0)
            cells = cells[..^1];

        return [.. cells.Select(cell => ParseCell(cell, row))];
    }

    private static int ParseCell(string cell, int row)
    {
        if (!int.TryParse(cell.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int tile))
            throw new FileFormatException($"En la fila {row}, «{cell.Trim()}» no es un número de tile.");

        if (tile < EmptyCell || tile >= TileSet.TileCount)
        {
            throw new FileFormatException(
                $"En la fila {row}, el tile {tile} se sale: van del 0 al {TileSet.TileCount - 1}, "
                + $"y {EmptyCell} es la celda vacía.");
        }

        return tile;
    }
}
