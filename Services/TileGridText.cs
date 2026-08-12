using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Cómo se escribe una fila de tiles en un fichero: dos dígitos hexadecimales por celda
/// y <c>..</c> donde no hay nada.
/// </summary>
/// <remarks>
/// Vive aquí y no en cada formato porque la usan los bloques y los mapas, y una
/// convención escrita en dos sitios acaba siendo dos convenciones. Con dos dígitos fijos
/// por celda, la posición dentro de la cadena es la columna, así que el ancho de la fila
/// dice el ancho de la rejilla.
/// </remarks>
public static class TileGridText
{
    /// <summary>Una celda sin tile, que no es lo mismo que el tile 0.</summary>
    public const string EmptyCell = "..";

    /// <summary>Los dígitos que ocupa una celda.</summary>
    public const int CellDigits = 2;

    /// <summary>Escribe una fila de la rejilla.</summary>
    public static string Row(TileGrid grid, int row, int width = 0) =>
        string.Concat(Enumerable.Range(0, width > 0 ? width : grid.Width).Select(column =>
            grid[column, row] is int tile ? tile.ToString("X2") : EmptyCell));

    /// <summary>Si la fila no tiene ni un tile, para poder no guardarla.</summary>
    public static bool IsEmptyRow(TileGrid grid, int row)
    {
        for (int column = 0; column < grid.Width; column++)
        {
            if (grid[column, row] is not null)
                return false;
        }

        return true;
    }

    /// <summary>Lee una fila dentro de la rejilla, en el sitio que se le diga.</summary>
    /// <exception cref="FileFormatException">La fila no está bien escrita.</exception>
    public static void ReadInto(string text, TileGrid grid, int row, string what)
    {
        int width = WidthOf(text, what);

        for (int column = 0; column < width; column++)
            grid[column, row] = Cell(text, column, what);
    }

    /// <summary>Cuántas celdas trae una fila escrita.</summary>
    /// <exception cref="FileFormatException">La fila no mide un número entero de celdas.</exception>
    public static int WidthOf(string text, string what)
    {
        if (text.Length == 0 || text.Length % CellDigits != 0)
        {
            throw new FileFormatException(
                $"En {what}, una fila mide {text.Length} dígitos y tienen que ser dos por celda.");
        }

        return text.Length / CellDigits;
    }

    /// <summary>El tile de una celda de la fila escrita, o <c>null</c> si está vacía.</summary>
    /// <exception cref="FileFormatException">La celda no es un número ni el hueco.</exception>
    public static int? Cell(string text, int column, string what)
    {
        string cell = text.Substring(column * CellDigits, CellDigits);

        if (cell == EmptyCell)
            return null;

        int high = PaletteSerializer.HexDigit(cell[0]) ?? throw NotHex(what, cell);
        int low = PaletteSerializer.HexDigit(cell[1]) ?? throw NotHex(what, cell);

        return (high << 4) | low;
    }

    private static FileFormatException NotHex(string what, string cell) => new(
        $"En {what}, la celda «{cell}» no es un número de tile ni está vacía.");
}
