using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// Qué se ve de la columna de colores según el tipo de banco, y su alineación con
/// las filas del lienzo.
/// </summary>
public class SpriteColorViewTests
{
    [AvaloniaFact]
    public void En_msx2_se_ve_la_columna_de_16_colores()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        Assert.True(editor.RowColorStrip.IsVisible);
        Assert.False(editor.SpriteColorPanel.IsVisible);
        Assert.Equal(Sprite.Rows, editor.RowColorStrip.ItemCount);
    }

    [AvaloniaFact]
    public void En_msx1_se_ve_una_sola_muestra_de_color()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX);

        Assert.True(editor.SpriteColorPanel.IsVisible);
        Assert.False(editor.RowColorStrip.IsVisible);
    }

    [AvaloniaFact]
    public void El_selector_de_fondo_esta_siempre()
    {
        using var msx1 = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX);
        Assert.True(msx1.BackgroundSwatch.IsVisible);

        using var msx2 = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        Assert.True(msx2.BackgroundSwatch.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(0, 16)]  // lienzo 256 / 16 filas
    [InlineData(1, 32)]  // lienzo 512
    public void Cada_casilla_mide_lo_mismo_que_una_fila_del_lienzo(int zoomIndex, double expectedHeight)
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetCanvasZoom(zoomIndex);

        Assert.Equal(expectedHeight, editor.CellSize);

        // Comprobar el botón real y no sólo la propiedad: lo frágil es el enlace $parent.
        Button swatch = editor.RowColorSwatch(0);
        Assert.Equal(expectedHeight, swatch.Height);
    }

    // El desplegable vive en un popup, fuera del árbol visual del botón que lo abre.
    // Sus enlaces $parent sólo se resuelven al abrirlo, así que no basta con que
    // compile ni con que la aplicación arranque.

    [AvaloniaFact]
    public void En_msx1_pulsar_un_color_lo_aplica_a_todo_el_sprite()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX);

        Button swatch = editor.SpriteColorPanel.GetVisualDescendants().OfType<Button>().First();
        var flyout = (Flyout)swatch.Flyout!;
        Button colorButton = OpenPalette(swatch, colorIndex: 2);

        ClickButton(colorButton);

        Assert.All(editor.Bank.SpritesList[0].ArraySpriteRows, row => Assert.Equal(2, row.Color));
        Assert.False(flyout.IsOpen);
    }

    [AvaloniaFact]
    public void El_desplegable_del_fondo_no_ofrece_el_transparente()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        var flyout = (Flyout)editor.BackgroundSwatch.Flyout!;
        flyout.ShowAt(editor.BackgroundSwatch);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        List<Button> colors = [.. ((ItemsControl)flyout.Content!).GetVisualDescendants().OfType<Button>()];

        Assert.Equal(15, colors.Count);
        Assert.Equal(1, ((PaletteColor)colors[0].CommandParameter!).Index);
        Assert.DoesNotContain(colors, b => ((PaletteColor)b.CommandParameter!).IsTransparent);
    }

    [AvaloniaFact]
    public void Pulsar_un_color_del_desplegable_lo_aplica_de_verdad()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        Button swatch = editor.RowColorSwatch(4);
        var flyout = (Flyout)swatch.Flyout!;
        Button colorButton = OpenPalette(swatch, colorIndex: 6);

        // Pulsación real: pasa por el handler que cierra el desplegable y por la
        // ejecución del comando que hace Avalonia. Ejecutar el comando a mano no
        // cubre ese orden, y el orden es justo lo que estuvo roto.
        ClickButton(colorButton);

        Assert.Equal(6, editor.Bank.SpritesList[0].ArraySpriteRows[4].Color);
        Assert.False(flyout.IsOpen);
    }

    [AvaloniaFact]
    public void Pulsar_un_color_del_fondo_lo_aplica_de_verdad()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        // Indice 3 de la lista de fondos = color 4 de la paleta (la lista empieza en el 1).
        var flyout = (Flyout)editor.BackgroundSwatch.Flyout!;
        Button colorButton = OpenPalette(editor.BackgroundSwatch, colorIndex: 3);

        ClickButton(colorButton);

        Assert.Equal(4, editor.ViewModel.BackgroundColor.Index);
        Assert.False(flyout.IsOpen);
    }

    private static void ClickButton(Button button)
    {
        var root = (TopLevel)button.GetVisualRoot()!;
        Point centre = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), root)!.Value;

        root.MouseDown(centre, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        root.MouseUp(centre, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Abre el desplegable de un botón de color y devuelve el enésimo color.</summary>
    private static Button OpenPalette(Button swatch, int colorIndex)
    {
        var flyout = (Flyout)swatch.Flyout!;
        flyout.ShowAt(swatch);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var items = (ItemsControl)flyout.Content!;
        return items.GetVisualDescendants().OfType<Button>().ElementAt(colorIndex);
    }

    [AvaloniaFact]
    public void La_casilla_muestra_el_hex_del_color_de_su_linea()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.RowColors[5].PickCommand.Execute(editor.ViewModel.ColorPalette[12]);

        Button swatch = editor.RowColorSwatch(5);
        TextBlock hex = swatch.GetVisualDescendants().OfType<TextBlock>().Single();

        Assert.Equal("C", hex.Text);
    }
}
