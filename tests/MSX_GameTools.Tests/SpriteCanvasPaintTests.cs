using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// Pintado sobre el lienzo, dirigiendo el ratón de verdad sobre la vista real.
///
/// Estas pruebas nacieron de un bug concreto: al capturar el puntero, Avalonia lanza
/// un PointerExited sobre el Canvas nada más empezar el arrastre. Mientras hubo un
/// handler para ese evento, el trazo se cancelaba en el primer movimiento y sólo se
/// podía pintar clic a clic.
/// </summary>
public class SpriteCanvasPaintTests
{
    [AvaloniaFact]
    public void Arrastrar_celda_a_celda_pinta_todo_el_recorrido()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Drag);

        canvas.Press(1, 2);
        for (int x = 2; x <= 12; x++)
            canvas.MoveTo(x, 2);
        canvas.Release(12, 2);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], canvas.PaintedCellsInRow(2));
        Assert.Equal(12, canvas.PaintedCount);
    }

    [AvaloniaFact]
    public void Un_movimiento_rapido_no_deja_huecos()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Drag);

        // Un único PointerMoved saltando 11 celdas: sin interpolación pintaría 2 pixeles.
        canvas.Press(1, 2);
        canvas.MoveTo(12, 2);
        canvas.Release(12, 2);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], canvas.PaintedCellsInRow(2));
    }

    [AvaloniaFact]
    public void Un_movimiento_rapido_en_diagonal_no_deja_huecos()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Drag);

        canvas.Press(0, 0);
        canvas.MoveTo(15, 15);
        canvas.Release(15, 15);

        Assert.Equal(16, canvas.PaintedCount);
        for (int i = 0; i < 16; i++)
            Assert.Equal([i], canvas.PaintedCellsInRow(i));
    }

    [AvaloniaFact]
    public void En_modo_punto_arrastrar_pinta_un_solo_pixel()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Click);

        canvas.Press(1, 2);
        for (int x = 2; x <= 12; x++)
            canvas.MoveTo(x, 2);
        canvas.Release(12, 2);

        Assert.Equal([1], canvas.PaintedCellsInRow(2));
    }

    [AvaloniaFact]
    public void El_boton_derecho_borra_arrastrando()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Drag);

        canvas.Press(1, 2);
        canvas.MoveTo(12, 2);
        canvas.Release(12, 2);
        Assert.Equal(12, canvas.PaintedCount);

        canvas.Press(1, 2, MouseButton.Right);
        canvas.MoveTo(12, 2);
        canvas.Release(12, 2, MouseButton.Right);

        Assert.Equal(0, canvas.PaintedCount);
    }

    [AvaloniaFact]
    public void Salir_del_lienzo_corta_el_trazo_en_vez_de_unir_con_una_recta()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Drag);

        canvas.Press(2, 5);
        canvas.MoveTo(0, 5);   // pinta 2, 1, 0
        canvas.MoveOutside(5); // fuera: no pinta y corta el trazo
        canvas.MoveTo(0, 5);   // reentrada: pinta sólo esta celda
        canvas.MoveTo(3, 5);   // pinta 0, 1, 2, 3
        canvas.Release(3, 5);

        Assert.Equal([0, 1, 2, 3], canvas.PaintedCellsInRow(5));
    }

    [AvaloniaFact]
    public void Arrastrar_fuera_del_lienzo_no_lanza_excepcion()
    {
        using var canvas = new SpriteCanvasHarness(PaintMode.Drag);

        canvas.Press(0, 0);
        canvas.MoveOutside(0);
        canvas.MoveOutside(15);
        canvas.Release(0, 0);

        Assert.Equal([0], canvas.PaintedCellsInRow(0));
    }
}
