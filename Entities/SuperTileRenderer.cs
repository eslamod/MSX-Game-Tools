using Avalonia.Media;

namespace MSX_GameTools.Entities;

/// <summary>
/// Compone la imagen de un supertile juntando los tiles que lo forman.
/// </summary>
/// <remarks>
/// <para>
/// Un supertile es una celda del mapa, así que se pinta como una sola imagen y no como
/// una rejilla de tiles: el lienzo del mapa dibuja una imagen por celda, y con esto sigue
/// haciendo lo mismo aunque la celda mida 2x2 tiles o 4x3.
/// </para>
/// <para>
/// El color 0 se resuelve aquí igual que en un tile suelto: es transparente y deja ver el
/// borde, así que hay que saber cuál es para enseñar lo que se verá en la máquina.
/// </para>
/// </remarks>
public static class SuperTileRenderer
{
    /// <summary>
    /// Pinta el bloque en una imagen del tamaño del supertile.
    /// </summary>
    /// <param name="target">
    /// Dónde pintar. Se reutiliza si ya mide lo que toca, para no rehacer el bitmap ni
    /// soltar el que la vista tiene cogido.
    /// </param>
    public static ImageMini Render(
        TileBlock block, TileSet tileSet, ColorPalette palette, Color border, ImageMini? target = null)
    {
        int width = block.Width * TileRow.Columns;
        int height = block.Height * Tile.Rows;

        ImageMini image = target is { } reused && reused.Width == width && reused.Height == height
            ? reused
            : new ImageMini(width, height);

        for (int row = 0; row < block.Height; row++)
        {
            for (int column = 0; column < block.Width; column++)
                DrawCell(block, tileSet, palette, border, image, column, row);
        }

        return image;
    }

    /// <summary>
    /// Un tile del supertile, o el hueco si esa celda no tiene ninguno.
    /// </summary>
    /// <remarks>
    /// El hueco se pinta del color del borde y no se deja como esté: la imagen se
    /// reutiliza entre repintados, y saltarse la celda dejaría lo que hubiera antes.
    /// </remarks>
    private static void DrawCell(
        TileBlock block,
        TileSet tileSet,
        ColorPalette palette,
        Color border,
        ImageMini image,
        int column,
        int row)
    {
        int left = column * TileRow.Columns;
        int top = row * Tile.Rows;

        Tile? tile = block[column, row] is int index && (uint)index < (uint)tileSet.ListOfTiles.Count
            ? tileSet.ListOfTiles[index]
            : null;

        for (int y = 0; y < Tile.Rows; y++)
        {
            for (int x = 0; x < TileRow.Columns; x++)
            {
                Color color = tile is null
                    ? border
                    : TileRenderer.ColorAt(tile, palette, border, x, y);

                image.SetPixel(left + x, top + y, color);
            }
        }
    }
}
