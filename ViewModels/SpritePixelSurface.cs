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
    /// <summary>El patrón que se está dibujando y cómo estaba al empezar el trazo.</summary>
    private Sprite? _drawn;

    private Sprite? _before;

    public int Size => Sprite.Rows;

    public bool IsSet(int x, int y) => editor.CurrentSprite.ArraySpriteRows[y].ArrayColumns[x];

    public void Set(int x, int y, bool on)
    {
        // La foto, en el primer pixel del trazo: el lienzo no avisa de cuándo empieza, y un
        // clic que no llega a cambiar nada no tiene por qué dejar paso.
        if (_before is null)
        {
            _drawn = editor.CurrentSprite;
            _before = editor.CurrentSprite.Copy();

            editor.Undo.Begin();
        }

        SpriteRow row = editor.CurrentSprite.ArraySpriteRows[y];

        row.ArrayColumns[x] = on;

        editor.Touch();

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

    /// <summary>
    /// Se ha soltado el ratón: se recomponen los grupos y el trazo queda como un paso.
    /// </summary>
    /// <remarks>
    /// Recomponer primero y anotar después. Hoy recomponer no dice que el banco haya
    /// cambiado, así que el orden da igual —se ha comprobado dándole la vuelta y no cae
    /// ninguna prueba—, pero de este lado el aviso caería dentro del trazo, que es donde la
    /// pila lo ignora; del otro se llevaría por delante el paso recién anotado.
    /// </remarks>
    public void EndStroke()
    {
        editor.NotifyPatternEdited(editor.CurrentSpriteIndex);

        if (_drawn is { } sprite && _before is { } before)
            editor.Undo.Push(new SpriteDrawn(sprite, before, sprite.Copy()));
        else
            editor.Undo.Cancel();

        _drawn = null;
        _before = null;
    }
}
