using Avalonia.Media;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// El patrón que se está editando, visto por el lienzo de pintado.
/// </summary>
/// <remarks>
/// Lee siempre el sprite actual del editor, no uno concreto: así navegar por el banco
/// no obliga a rehacer nada. El color de un pixel encendido sale de la línea, y el de
/// uno apagado del color de fondo del banco, que es lo mismo que ve el color 0.
/// </remarks>
internal sealed class SpritePixelSurface(SpritesEditorViewModel editor) : IPixelSurface
{
    public int Size => Sprite.Rows;

    public bool IsSet(int x, int y) => editor.CurrentSprite.ArraySpriteRows[y].ArrayColumns[x];

    public void Set(int x, int y, bool on)
    {
        SpriteRow row = editor.CurrentSprite.ArraySpriteRows[y];

        row.ArrayColumns[x] = on;

        editor.CurrentSprite.ImageMini?.SetPixel(x, y, on
            ? SpriteRenderer.ResolveRowColor(editor.ColorPalette, row.Color, editor.BackgroundColor.Color)
            : editor.BackgroundColor.Color);
    }

    public IBrush BrushAt(int x, int y)
    {
        SpriteRow row = editor.CurrentSprite.ArraySpriteRows[y];
        IBrush background = editor.BackgroundColor.Brush;

        return row.ArrayColumns[x]
            ? SpriteRenderer.ResolveRowBrush(editor.ColorPalette, row.Color, background)
            : background;
    }

    public void EndStroke() => editor.NotifyPatternEdited(editor.CurrentSpritePosition - 1);
}
