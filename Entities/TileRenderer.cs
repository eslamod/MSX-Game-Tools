using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Pinta un tile en su miniatura.
/// </summary>
/// <remarks>
/// A diferencia de un sprite, aquí no hay color transparente que resolver contra un
/// fondo: cada línea trae sus dos colores y cada pixel es uno de los dos. El color 0 en
/// un tile es el de la paleta, no un hueco.
/// </remarks>
public static class TileRenderer
{
    public static void Render(Tile tile, ColorPalette palette, ImageMini target)
    {
        for (int row = 0; row < Tile.Rows; row++)
            RenderRow(tile, row, palette, target);
    }

    public static void RenderRow(Tile tile, int rowIndex, ColorPalette palette, ImageMini target)
    {
        TileRow row = tile.ArrayTileRows[rowIndex];

        Color foreground = palette.GetColor(row.ForeColor);
        Color background = palette.GetColor(row.BackColor);

        for (int column = 0; column < TileRow.Columns; column++)
            target.SetPixel(column, rowIndex, row.ArrayPattern[column] ? foreground : background);
    }

    /// <summary>Pincel con el que se ve un pixel concreto.</summary>
    public static IBrush BrushAt(Tile tile, ColorPalette palette, int column, int rowIndex)
    {
        TileRow row = tile.ArrayTileRows[rowIndex];

        return palette.GetBrush(row.ArrayPattern[column] ? row.ForeColor : row.BackColor);
    }
}
