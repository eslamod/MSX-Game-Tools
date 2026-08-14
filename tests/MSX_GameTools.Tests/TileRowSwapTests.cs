using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Intercambiar el frente y el fondo de una línea del tile.
/// </summary>
/// <remarks>
/// Lo que tiene que cumplirse es que <b>no se note</b>: es la misma línea escrita de la
/// otra forma. Por eso las pruebas comparan el color de los ocho pixeles antes y después
/// y además miran que la línea haya cambiado por dentro. Sin lo segundo, una versión que
/// no hiciera nada las pasaría todas: los pixeles también seguirían igual.
/// </remarks>
public class TileRowSwapTests
{
    private const int Line = 3;

    [AvaloniaFact]
    public void Intercambiar_no_cambia_ni_un_pixel()
    {
        TileSetEditorViewModel editor = NewEditor();
        TileRow row = Prepare(editor, fore: 1, back: 2);

        Color[] before = PixelsOf(editor);

        editor.RowColors[Line].SwapColorsCommand.Execute(null);

        Assert.Equal(before, PixelsOf(editor));

        // Y ha cambiado de verdad, que es lo que separa esto de no hacer nada.
        Assert.Equal(2, row.ForeColor);
        Assert.Equal(1, row.BackColor);
        Assert.Equal([false, false, true, true, true, false, true, true], row.ArrayPattern);
    }

    /// <summary>
    /// Y tampoco con el color 0 de por medio, que es el caso que podría estropearse.
    /// </summary>
    /// <remarks>
    /// El 0 es transparente y se ve como el color del borde. Al intercambiar, los pixeles
    /// que dejaban ver el borde son justo los otros, así que si el intercambio se hiciera
    /// sobre el color ya resuelto en vez de sobre el índice, la línea cambiaría de aspecto.
    /// </remarks>
    [AvaloniaFact]
    public void Con_el_color_transparente_tampoco()
    {
        TileSetEditorViewModel editor = NewEditor();
        TileRow row = Prepare(editor, fore: 8, back: 0);

        editor.BorderColorIndex = 4;

        Color[] before = PixelsOf(editor);

        editor.RowColors[Line].SwapColorsCommand.Execute(null);

        Assert.Equal(before, PixelsOf(editor));
        Assert.Equal(0, row.ForeColor);
        Assert.Equal(8, row.BackColor);
    }

    /// <summary>
    /// El dibujo no cambia, pero el fichero sí: los dos bytes de la línea salen distintos.
    /// </summary>
    [AvaloniaFact]
    public void El_juego_queda_pendiente_de_guardar()
    {
        TileSetEditorViewModel editor = NewEditor();
        TileRow row = Prepare(editor, fore: 1, back: 2);

        editor.MarkClean();

        byte pattern = row.PatternByte;
        byte color = row.ColorByte;

        editor.RowColors[Line].SwapColorsCommand.Execute(null);

        Assert.True(editor.IsModified);
        Assert.Equal((byte)~pattern, row.PatternByte);

        // El frente va en el nibble alto: 1 sobre 2 es 0x12, y al revés 0x21.
        Assert.Equal(0x12, color);
        Assert.Equal(0x21, row.ColorByte);
    }

    /// <summary>
    /// Que la opción esté en el menú de los dos colores y llegue al comando.
    /// </summary>
    /// <remarks>
    /// Montando la vista y abriendo el menú, no leyendo el XAML: un <c>ContextMenu</c> vive
    /// fuera del árbol visual de su control y hereda el DataContext por su cuenta, así que
    /// el enlace puede quedarse sin resolver y el menú saldría con la opción apagada. Eso
    /// no se ve de ninguna otra forma.
    /// </remarks>
    [AvaloniaFact]
    public void Los_dos_colores_de_la_linea_ofrecen_la_opcion()
    {
        TileSetEditorViewModel editor = NewEditor();
        TileRow row = Prepare(editor, fore: 1, back: 2);

        var view = new TileSetEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 1200, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        List<Button> swatches = [.. view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.ContextMenu is not null)];

        // Ocho lineas por dos colores.
        Assert.Equal(16, swatches.Count);

        foreach (Button swatch in swatches)
        {
            swatch.ContextMenu!.Open(swatch);
            Dispatcher.UIThread.RunJobs();

            MenuItem item = Assert.IsType<MenuItem>(Assert.Single(swatch.ContextMenu.Items)!);

            Assert.NotNull(item.Command);
            Assert.Equal("Intercambiar frente y fondo", item.Header);

            swatch.ContextMenu.Close();
            Dispatcher.UIThread.RunJobs();
        }

        // Y el comando del menu es el de su linea: los dos colores de la cuarta.
        foreach (Button swatch in swatches.Skip(Line * 2).Take(2))
        {
            var item = (MenuItem)swatch.ContextMenu!.Items[0]!;
            int before = row.ForeColor;

            item.Command!.Execute(item.CommandParameter);

            Assert.NotEqual(before, row.ForeColor);
        }

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Una línea con dos colores distintos y un dibujo que no es simétrico.</summary>
    private static TileRow Prepare(TileSetEditorViewModel editor, int fore, int back)
    {
        TileRow row = editor.CurrentTile.ArrayTileRows[Line];

        row.ForeColor = fore;
        row.BackColor = back;
        row.ArrayPattern[0] = true;
        row.ArrayPattern[1] = true;
        row.ArrayPattern[5] = true;

        return row;
    }

    /// <summary>Los ocho pixeles de la línea tal como se ven, con el borde ya resuelto.</summary>
    private static Color[] PixelsOf(TileSetEditorViewModel editor) =>
        [.. Enumerable.Range(0, TileRow.Columns).Select(column => TileRenderer.ColorAt(
            editor.CurrentTile, editor.ColorPalette, editor.BorderColor.Color, column, Line))];

    private static TileSetEditorViewModel NewEditor() =>
        new(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());
}
