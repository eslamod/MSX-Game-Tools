using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Traduce el estado de un sprite a pixeles, resolviendo el color de cada línea
/// contra el color de fondo elegido.
/// </summary>
public static class SpriteRenderer
{
    /// <summary>
    /// Color con el que se dibuja un pixel encendido de una línea. El índice 0 de la
    /// paleta es transparente: en la máquina real ese pixel deja ver el fondo, así que
    /// aquí se dibuja con el color de fondo.
    /// </summary>
    public static Color ResolveRowColor(ColorPalette palette, int colorIndex, Color background)
        => colorIndex == 0 ? background : palette.GetColor(colorIndex);

    /// <inheritdoc cref="ResolveRowColor"/>
    public static IBrush ResolveRowBrush(ColorPalette palette, int colorIndex, IBrush background)
        => colorIndex == 0 ? background : palette.GetBrush(colorIndex);

    /// <summary>Repinta la miniatura entera a partir del estado del sprite.</summary>
    public static void Render(Sprite sprite, ColorPalette palette, Color background, ImageMini target)
    {
        for (int row = 0; row < Sprite.Rows; row++)
            RenderRow(sprite, row, palette, background, target);
    }

    /// <summary>Repinta en la miniatura sólo una línea del sprite.</summary>
    public static void RenderRow(Sprite sprite, int rowIndex, ColorPalette palette, Color background, ImageMini target)
    {
        SpriteRow row = sprite.ArraySpriteRows[rowIndex];
        Color on = ResolveRowColor(palette, row.Color, background);

        for (int column = 0; column < SpriteRow.Columns; column++)
            target.SetPixel(column, rowIndex, row.ArrayColumns[column] ? on : background);
    }
}
