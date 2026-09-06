using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Deshacer y rehacer lo que se dibuja en un juego de tiles.
/// </summary>
/// <remarks>
/// Los mismos veinte pasos que el mapa. Un paso es un trazo entero —lo que va de pulsar a
/// soltar—, no cada pixel: pintar una línea de veinte pixeles y que deshacer los devolviera
/// de uno en uno sería veinte veces deshacer para una sola cosa.
/// </remarks>
public class TileUndoTests
{
    /// <summary>Un trazo se deshace entero, y se rehace.</summary>
    [AvaloniaFact]
    public void Un_trazo_se_deshace_entero_y_se_rehace()
    {
        TileSetEditorViewModel editor = NewEditor();

        Stroke(editor, (1, 1), (2, 1), (3, 1));

        Assert.True(editor.CanUndoDrawing);
        Assert.True(editor.PixelSurface.IsSet(2, 1));

        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(1, 1));
        Assert.False(editor.PixelSurface.IsSet(2, 1));
        Assert.False(editor.PixelSurface.IsSet(3, 1));

        Assert.False(editor.CanUndoDrawing);
        Assert.True(editor.CanRedoDrawing);

        editor.RedoDrawingCommand.Execute(null);

        Assert.True(editor.PixelSurface.IsSet(2, 1));
        Assert.False(editor.CanRedoDrawing);
    }

    /// <summary>Cada trazo es un paso, y se deshacen del último al primero.</summary>
    [AvaloniaFact]
    public void Cada_trazo_es_un_paso()
    {
        TileSetEditorViewModel editor = NewEditor();

        Stroke(editor, (0, 0));
        Stroke(editor, (7, 7));

        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(7, 7));
        Assert.True(editor.PixelSurface.IsSet(0, 0));

        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(0, 0));
        Assert.False(editor.CanUndoDrawing);
    }

    /// <summary>
    /// La historia se queda en veinte pasos.
    /// </summary>
    /// <remarks>
    /// El más viejo se cae para dejar sitio, así que el primer trazo ya no se puede
    /// deshacer: es lo mismo que hace el mapa y por lo mismo.
    /// </remarks>
    [AvaloniaFact]
    public void La_historia_se_queda_en_veinte_pasos()
    {
        TileSetEditorViewModel editor = NewEditor();

        for (int step = 0; step < PixelUndoStack.MaxSteps + 5; step++)
            Stroke(editor, (step % 8, step / 8));

        for (int step = 0; step < PixelUndoStack.MaxSteps; step++)
        {
            Assert.True(editor.CanUndoDrawing);

            editor.UndoDrawingCommand.Execute(null);
        }

        Assert.False(editor.CanUndoDrawing);

        // Y lo que se cayó de la pila se queda dibujado: deshacer no llega hasta allí.
        Assert.True(editor.PixelSurface.IsSet(0, 0));
    }

    /// <summary>
    /// Un trazo se deshace aunque se esté mirando otro tile.
    /// </summary>
    /// <remarks>
    /// El paso se guarda el tile al que pertenece, no «el de delante»: navegar por el juego
    /// entre trazo y trazo es lo normal, y deshacer tiene que devolver lo que se hizo,
    /// no estropear lo que se está mirando.
    /// </remarks>
    [AvaloniaFact]
    public void Se_deshace_el_tile_en_el_que_se_dibujo()
    {
        TileSetEditorViewModel editor = NewEditor();

        Stroke(editor, (4, 4));

        editor.GoTo(30);

        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.TileSet.ListOfTiles[0].ArrayTileRows[4].ArrayPattern[4]);
        Assert.Equal(30, editor.CurrentTileIndex);
    }

    /// <summary>
    /// Lo que cambia el juego sin pasar por la pila la deja sin valer.
    /// </summary>
    /// <remarks>
    /// Mover los colores de la paleta reajusta los 256 tiles por su cuenta. Deshacer después
    /// devolvería la foto de un tile de antes del reajuste, con los índices viejos, y eso se
    /// vería como si deshacer cambiara colores que nadie tocó. Sin historia se está mejor que
    /// con una que miente.
    /// </remarks>
    [AvaloniaFact]
    public void Reajustar_los_colores_tira_la_historia()
    {
        TileSetEditorViewModel editor = NewEditor();

        Stroke(editor, (2, 2));

        Assert.True(editor.CanUndoDrawing);

        var swaps = new PaletteSwaps();
        swaps.Swap(3, 10);

        editor.RemapColors(swaps.Table());

        Assert.False(editor.CanUndoDrawing);
        Assert.False(editor.CanRedoDrawing);
    }

    /// <summary>Un trazo que no llega a cambiar nada no deja paso.</summary>
    [AvaloniaFact]
    public void Un_trazo_que_no_pinta_nada_no_deja_paso()
    {
        TileSetEditorViewModel editor = NewEditor();

        editor.PixelSurface.EndStroke();

        Assert.False(editor.CanUndoDrawing);
    }

    // ------------------------------------------------------------------ los andamios

    private static TileSetEditorViewModel NewEditor() =>
        new(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

    /// <summary>Un trazo: se encienden esos pixeles y se suelta el ratón.</summary>
    private static void Stroke(TileSetEditorViewModel editor, params (int X, int Y)[] pixels)
    {
        foreach ((int x, int y) in pixels)
            editor.PixelSurface.Set(x, y, true);

        editor.PixelSurface.EndStroke();
    }
}
