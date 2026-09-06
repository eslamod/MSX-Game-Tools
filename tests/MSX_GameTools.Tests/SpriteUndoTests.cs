using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Deshacer y rehacer lo que se dibuja en un banco de sprites.
/// </summary>
/// <remarks>
/// La misma pila que el editor de tiles, con los mismos veinte pasos. Lo propio de aquí es
/// que un patrón lo usan los grupos: al deshacer hay que recomponerlos, porque enseñan ese
/// dibujo.
/// </remarks>
public class SpriteUndoTests
{
    /// <summary>Un trazo se deshace entero, y se rehace.</summary>
    [AvaloniaFact]
    public void Un_trazo_se_deshace_entero_y_se_rehace()
    {
        SpritesEditorViewModel editor = NewEditor();

        Stroke(editor, (1, 1), (2, 1), (3, 1));

        Assert.True(editor.CanUndoDrawing);

        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(1, 1));
        Assert.False(editor.PixelSurface.IsSet(3, 1));
        Assert.True(editor.CanRedoDrawing);

        editor.RedoDrawingCommand.Execute(null);

        Assert.True(editor.PixelSurface.IsSet(2, 1));
    }

    /// <summary>
    /// Y el grupo que usa ese patrón se entera.
    /// </summary>
    /// <remarks>
    /// La composición de un grupo se dibuja aparte, así que devolver el patrón sin
    /// recomponerla dejaría el grupo enseñando lo que ya no está.
    /// </remarks>
    [AvaloniaFact]
    public void El_grupo_que_usa_el_patron_se_repinta_al_deshacer()
    {
        SpritesEditorViewModel editor = NewEditor();

        editor.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.SelectedGroup!;

        Stroke(editor, (0, 0));

        int painted = PixelReader.At(group.Preview, 0, 0);

        editor.UndoDrawingCommand.Execute(null);

        Assert.NotEqual(painted, PixelReader.At(group.Preview, 0, 0));
    }

    /// <summary>Cada trazo es un paso, y la pila se queda en veinte.</summary>
    [AvaloniaFact]
    public void La_historia_se_queda_en_veinte_pasos()
    {
        SpritesEditorViewModel editor = NewEditor();

        for (int step = 0; step < PixelUndoStack.MaxSteps + 3; step++)
            Stroke(editor, (step % 16, step / 16));

        for (int step = 0; step < PixelUndoStack.MaxSteps; step++)
            editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.CanUndoDrawing);

        // Los tres primeros trazos se cayeron de la pila y siguen dibujados.
        Assert.True(editor.PixelSurface.IsSet(0, 0));
    }

    /// <summary>
    /// Lo que cambia el banco sin pasar por la pila la deja sin valer.
    /// </summary>
    /// <inheritdoc cref="TileUndoTests.Reajustar_los_colores_tira_la_historia" path="/remarks"/>
    [AvaloniaFact]
    public void Reajustar_los_colores_tira_la_historia()
    {
        SpritesEditorViewModel editor = NewEditor();

        Stroke(editor, (2, 2));

        Assert.True(editor.CanUndoDrawing);

        var swaps = new PaletteSwaps();
        swaps.Swap(3, 10);

        editor.RemapColors(swaps.Table());

        Assert.False(editor.CanUndoDrawing);
    }

    /// <summary>
    /// Vaciar un patrón también se deshace.
    /// </summary>
    /// <remarks>
    /// Es el botón más destructivo del editor y está justo al lado de deshacer: que ése no se
    /// pudiera deshacer sería la peor de las sorpresas. Se sigue preguntando antes, porque la
    /// pila se vacía al pasar por cosas que no son suyas y no siempre habrá paso al que
    /// volver.
    /// </remarks>
    [AvaloniaFact]
    public async Task Vaciar_un_patron_se_deshace()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        var editor = new SpritesEditorViewModel(
            bank, ColorPalette.CreateMsxStandard(), new TestDialogService { ConfirmAnswer = true });

        Stroke(editor, (5, 5), (6, 5));

        await editor.ClearSpriteCommand.ExecuteAsync(null);

        Assert.False(editor.PixelSurface.IsSet(5, 5));

        editor.UndoDrawingCommand.Execute(null);

        Assert.True(editor.PixelSurface.IsSet(5, 5));
        Assert.True(editor.PixelSurface.IsSet(6, 5));

        // Y el trazo de antes sigue estando: vaciar es un paso más, no un borrón y cuenta
        // nueva.
        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(5, 5));
    }

    // ------------------------------------------------------------------ los andamios

    private static SpritesEditorViewModel NewEditor() =>
        new(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"), ColorPalette.CreateMsxStandard());

    /// <summary>Un trazo: se encienden esos pixeles y se suelta el ratón.</summary>
    private static void Stroke(SpritesEditorViewModel editor, params (int X, int Y)[] pixels)
    {
        foreach ((int x, int y) in pixels)
            editor.PixelSurface.Set(x, y, true);

        editor.PixelSurface.EndStroke();
    }
}
