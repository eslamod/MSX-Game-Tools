using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Copiar y estampar tiles dentro del juego.
/// </summary>
/// <remarks>
/// Lo que viaja son los dibujos, no los números: al estampar, los tiles de destino pasan a
/// dibujar lo que dibujaban los de origen y siguen siendo los números que eran. Es al revés
/// que el trozo del mapa, que lleva números y no dibujos.
/// </remarks>
public class TileCopyTests
{
    // ------------------------------------------------------------------ el modelo

    [AvaloniaFact]
    public void Estampar_lleva_el_dibujo_y_los_colores()
    {
        var tileSet = new TileSet("Bosque");

        Draw(tileSet, 5, pattern: 0b1010_0000, fore: 7, back: 3);

        TileSetPatch copied = tileSet.Copy(5, 0, 1, 1);
        tileSet.Stamp(9, 2, copied);

        Tile stamped = At(tileSet, 9, 2);

        Assert.Equal(0b1010_0000, stamped.ArrayTileRows[0].PatternByte);
        Assert.Equal(7, stamped.ArrayTileRows[0].ForeColor);
        Assert.Equal(3, stamped.ArrayTileRows[0].BackColor);
    }

    /// <summary>
    /// La copia es una copia: retocar el original después no cambia lo copiado.
    /// </summary>
    /// <remarks>
    /// Guardando referencias en vez de copias, seguir dibujando en el tile de origen
    /// cambiaría por debajo lo que se va a estampar, y peor todavía, el tile estampado y el
    /// original quedarían atados para siempre.
    /// </remarks>
    [AvaloniaFact]
    public void Lo_copiado_no_cambia_si_se_retoca_el_original()
    {
        var tileSet = new TileSet("Bosque");

        Draw(tileSet, 5, pattern: 0b1000_0000, fore: 7, back: 3);

        TileSetPatch copied = tileSet.Copy(5, 0, 1, 1);

        Draw(tileSet, 5, pattern: 0b0000_0001, fore: 1, back: 1);

        tileSet.Stamp(9, 0, copied);

        Assert.Equal(0b1000_0000, At(tileSet, 9, 0).ArrayTileRows[0].PatternByte);
        Assert.Equal(7, At(tileSet, 9, 0).ArrayTileRows[0].ForeColor);
    }

    /// <summary>Y el estampado tampoco queda atado al original.</summary>
    [AvaloniaFact]
    public void El_tile_estampado_no_queda_atado_al_de_origen()
    {
        var tileSet = new TileSet("Bosque");

        Draw(tileSet, 5, pattern: 0b1000_0000, fore: 7, back: 3);

        tileSet.Stamp(9, 0, tileSet.Copy(5, 0, 1, 1));

        Draw(tileSet, 5, pattern: 0b0000_1111, fore: 2, back: 2);

        Assert.Equal(0b1000_0000, At(tileSet, 9, 0).ArrayTileRows[0].PatternByte);
    }

    /// <summary>
    /// Estampar devuelve lo que había, que es de donde sale el deshacer.
    /// </summary>
    [AvaloniaFact]
    public void Lo_que_devuelve_estampar_repone_lo_machacado()
    {
        var tileSet = new TileSet("Bosque");

        Draw(tileSet, 5, pattern: 0b1111_0000, fore: 7, back: 3);
        Draw(tileSet, 70, pattern: 0b0000_1111, fore: 2, back: 9);

        TileSetPatch before = tileSet.Stamp(6, 2, tileSet.Copy(5, 0, 1, 1));

        Assert.Equal(0b1111_0000, At(tileSet, 6, 2).ArrayTileRows[0].PatternByte);

        tileSet.Stamp(6, 2, before);

        Assert.Equal(0b0000_1111, At(tileSet, 6, 2).ArrayTileRows[0].PatternByte);
        Assert.Equal(2, At(tileSet, 6, 2).ArrayTileRows[0].ForeColor);
    }

    /// <summary>
    /// Lo que se sale por la derecha se recorta y no aparece en la fila de abajo.
    /// </summary>
    /// <remarks>
    /// La rejilla son 32 columnas y los 256 tiles van seguidos, así que pasarse de la
    /// columna 31 escribiría en el primer tile de la fila siguiente, que es donde no se
    /// está mirando.
    /// </remarks>
    [AvaloniaFact]
    public void Un_trozo_pegado_al_borde_se_recorta()
    {
        var tileSet = new TileSet("Bosque");

        Draw(tileSet, 0, pattern: 0b1111_1111, fore: 7, back: 3);
        Draw(tileSet, 1, pattern: 0b1111_1111, fore: 7, back: 3);
        Draw(tileSet, 2, pattern: 0b1111_1111, fore: 7, back: 3);

        tileSet.Stamp(30, 0, tileSet.Copy(0, 0, 3, 1));

        Assert.Equal(0b1111_1111, At(tileSet, 30, 0).ArrayTileRows[0].PatternByte);
        Assert.Equal(0b1111_1111, At(tileSet, 31, 0).ArrayTileRows[0].PatternByte);

        // El tercero no cabía: la fila de abajo se queda intacta.
        Assert.Equal(0, At(tileSet, 0, 1).ArrayTileRows[0].PatternByte);
    }

    // ------------------------------------------------------------------ el editor

    [AvaloniaFact]
    public void Los_tres_modos_son_uno_u_otro()
    {
        TileSetEditorViewModel editor = NewEditor();

        Assert.True(editor.IsEditTool);

        editor.Tool = TileTool.Select;

        Assert.False(editor.IsEditTool);
        Assert.True(editor.IsSelectTool);

        editor.Tool = TileTool.Stamp;

        Assert.True(editor.IsStampTool);
    }

    [AvaloniaFact]
    public void Marcar_y_estampar_cambia_los_tiles_y_deja_sin_guardar()
    {
        TileSetEditorViewModel editor = NewEditor();

        Draw(editor.TileSet, 5, pattern: 0b1100_0000, fore: 7, back: 3);
        editor.MarkClean();

        editor.SelectRegion(5, 0, 1, 1);
        editor.StampAt(9, 3);

        Assert.Equal(0b1100_0000, At(editor.TileSet, 9, 3).ArrayTileRows[0].PatternByte);
        Assert.True(editor.IsModified);
    }

    [AvaloniaFact]
    public void Deshacer_el_estampado_devuelve_lo_que_habia()
    {
        TileSetEditorViewModel editor = NewEditor();

        Draw(editor.TileSet, 5, pattern: 0b1100_0000, fore: 7, back: 3);
        Draw(editor.TileSet, 100, pattern: 0b0011_0011, fore: 4, back: 5);

        Assert.False(editor.UndoDrawingCommand.CanExecute(null));

        editor.SelectRegion(5, 0, 1, 1);
        editor.StampAt(4, 3);

        Assert.True(editor.UndoDrawingCommand.CanExecute(null));

        editor.UndoDrawingCommand.Execute(null);

        Assert.Equal(0b0011_0011, At(editor.TileSet, 4, 3).ArrayTileRows[0].PatternByte);
        Assert.Equal(4, At(editor.TileSet, 4, 3).ArrayTileRows[0].ForeColor);

        // Y no se deshace dos veces: no hay más pasos que ése.
        Assert.False(editor.UndoDrawingCommand.CanExecute(null));

        // Pero se rehace, que ahora estampar va a la misma pila que lo que se dibuja.
        Assert.True(editor.RedoDrawingCommand.CanExecute(null));

        editor.RedoDrawingCommand.Execute(null);

        Assert.Equal(0b1100_0000, At(editor.TileSet, 4, 3).ArrayTileRows[0].PatternByte);
    }

    /// <summary>Sin nada marcado, estampar no hace nada.</summary>
    [AvaloniaFact]
    public void Sin_nada_marcado_estampar_no_toca_el_juego()
    {
        TileSetEditorViewModel editor = NewEditor();

        Draw(editor.TileSet, 100, pattern: 0b0011_0011, fore: 4, back: 5);
        editor.MarkClean();

        editor.StampAt(4, 3);

        Assert.Equal(0b0011_0011, At(editor.TileSet, 4, 3).ArrayTileRows[0].PatternByte);
        Assert.False(editor.IsModified);
        Assert.False(editor.UndoDrawingCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Marcar_deja_el_rectangulo_guardado()
    {
        TileSetEditorViewModel editor = NewEditor();

        Assert.False(editor.HasSelection);

        editor.SelectRegion(4, 1, 3, 2);

        Assert.True(editor.HasSelection);
        Assert.Equal(new MapRegion(4, 1, 3, 2), editor.Selection);
    }

    /// <summary>
    /// Se estampa lo que se ve, no lo que se veía al marcar.
    /// </summary>
    /// <remarks>
    /// Los dibujos se copian al soltar. La vista enseña el trozo bajo el ratón con las
    /// miniaturas de verdad, así que copiándolo al marcar el fantasma diría una cosa y el
    /// estampado haría otra en cuanto se retocara un tile de origen.
    /// </remarks>
    [AvaloniaFact]
    public void Se_estampa_el_dibujo_que_tiene_el_origen_al_soltar()
    {
        TileSetEditorViewModel editor = NewEditor();

        Draw(editor.TileSet, 5, pattern: 0b1000_0000, fore: 7, back: 3);
        editor.SelectRegion(5, 0, 1, 1);

        // Se retoca el origen despues de marcarlo.
        Draw(editor.TileSet, 5, pattern: 0b0001_1000, fore: 2, back: 6);

        editor.StampAt(9, 4);

        Assert.Equal(0b0001_1000, At(editor.TileSet, 9, 4).ArrayTileRows[0].PatternByte);
        Assert.Equal(2, At(editor.TileSet, 9, 4).ArrayTileRows[0].ForeColor);
    }

    /// <summary>Y estampar encima de lo marcado no se pisa a sí mismo.</summary>
    [AvaloniaFact]
    public void Estampar_solapando_el_origen_no_se_pisa()
    {
        TileSetEditorViewModel editor = NewEditor();

        Draw(editor.TileSet, 0, pattern: 0b1000_0000, fore: 7, back: 3);
        Draw(editor.TileSet, 1, pattern: 0b0100_0000, fore: 7, back: 3);
        Draw(editor.TileSet, 2, pattern: 0b0010_0000, fore: 7, back: 3);

        editor.SelectRegion(0, 0, 3, 1);
        editor.StampAt(1, 0);

        // Los tres de origen caen corridos una celda, sin arrastrar el primero.
        Assert.Equal(0b1000_0000, At(editor.TileSet, 1, 0).ArrayTileRows[0].PatternByte);
        Assert.Equal(0b0100_0000, At(editor.TileSet, 2, 0).ArrayTileRows[0].PatternByte);
        Assert.Equal(0b0010_0000, At(editor.TileSet, 3, 0).ArrayTileRows[0].PatternByte);
    }

    /// <summary>El tile número n de la rejilla de 32 columnas.</summary>
    private static Tile At(TileSet tileSet, int column, int row) =>
        tileSet.ListOfTiles[(row * TileSet.Columns) + column];

    /// <summary>Le pone algo reconocible a la primera línea de un tile.</summary>
    private static void Draw(TileSet tileSet, int index, int pattern, int fore, int back)
    {
        TileRow row = tileSet.ListOfTiles[index].ArrayTileRows[0];

        row.ForeColor = fore;
        row.BackColor = back;

        for (int column = 0; column < TileRow.Columns; column++)
            row.ArrayPattern[column] = (pattern & (1 << (TileRow.Columns - 1 - column))) != 0;
    }

    private static TileSetEditorViewModel NewEditor() =>
        new(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());
}
