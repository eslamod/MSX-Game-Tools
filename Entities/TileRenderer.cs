using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Pinta un tile en su miniatura.
/// </summary>
/// <remarks>
/// Cada línea trae dos colores y cada pixel es uno de los dos, pero el código 0 es
/// transparente igual que en los sprites: la tabla de colores de GRAPHIC 2 usa los
/// mismos códigos, y un pixel con el 0 deja ver el color del borde. Por eso hace falta
/// saber cuál es ese color para poder previsualizar de verdad lo que se vera en la
/// máquina.
/// </remarks>
public static class TileRenderer
{
    public static void Render(Tile tile, ColorPalette palette, Color border, ImageMini target)
    {
        for (int row = 0; row < Tile.Rows; row++)
            RenderRow(tile, row, palette, border, target);
    }

    public static void RenderRow(Tile tile, int rowIndex, ColorPalette palette, Color border, ImageMini target)
    {
        TileRow row = tile.ArrayTileRows[rowIndex];

        Color foreground = palette.Resolve(row.ForeColor, border);
        Color background = palette.Resolve(row.BackColor, border);

        for (int column = 0; column < TileRow.Columns; column++)
            target.SetPixel(column, rowIndex, row.ArrayPattern[column] ? foreground : background);
    }

    /// <summary>Color con el que se ve un pixel concreto.</summary>
    public static Color ColorAt(Tile tile, ColorPalette palette, Color border, int column, int rowIndex)
    {
        TileRow row = tile.ArrayTileRows[rowIndex];

        return palette.Resolve(row.ArrayPattern[column] ? row.ForeColor : row.BackColor, border);
    }

    /// <summary>Pincel con el que se ve un pixel concreto.</summary>
    public static IBrush BrushAt(Tile tile, ColorPalette palette, IBrush border, int column, int rowIndex)
    {
        TileRow row = tile.ArrayTileRows[rowIndex];

        return palette.ResolveBrush(row.ArrayPattern[column] ? row.ForeColor : row.BackColor, border);
    }
}
