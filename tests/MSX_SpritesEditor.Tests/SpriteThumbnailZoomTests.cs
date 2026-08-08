using Avalonia.Headless.XUnit;
using Xunit;
using static MSX_SpritesEditor.Tests.SpriteCanvasHarness;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Zoom de la tira de miniaturas. Los tamaños son múltiplos enteros de los 16 px del
/// sprite (4, 8, 16 y 32 px de pantalla por pixel), para que todos los pixeles del
/// sprite salgan del mismo tamaño al escalar sin interpolación.
/// </summary>
public class SpriteThumbnailZoomTests
{
    [AvaloniaFact]
    public void Por_defecto_las_miniaturas_estan_en_x1()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);

        Assert.Equal(64, editor.ThumbnailImageSize(0));
    }

    [AvaloniaTheory]
    [InlineData(1, 64)]
    [InlineData(2, 128)]
    [InlineData(4, 256)]
    [InlineData(8, 512)]
    public void Cada_boton_de_zoom_fija_su_tamano(int factor, double expectedSize)
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);

        editor.SetThumbnailZoom(factor);

        Assert.Equal(expectedSize, editor.ThumbnailImageSize(0));
    }

    [AvaloniaFact]
    public void El_zoom_se_aplica_a_todas_las_miniaturas_y_a_las_que_se_anadan_despues()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        editor.ViewModel.AddSpriteCommand.Execute(null);

        editor.SetThumbnailZoom(4);

        Assert.Equal(256, editor.ThumbnailImageSize(0));
        Assert.Equal(256, editor.ThumbnailImageSize(1));

        editor.ViewModel.AddSpriteCommand.Execute(null);

        Assert.Equal(256, editor.ThumbnailImageSize(2));
    }

    [AvaloniaFact]
    public void Cambiar_el_zoom_no_altera_la_seleccion()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        editor.ViewModel.AddSpriteCommand.Execute(null);
        editor.ClickThumbnail(0);

        editor.SetThumbnailZoom(8);

        Assert.Equal(0, editor.Thumbnails.SelectedIndex);
        Assert.Equal(1, editor.ViewModel.CurrentSpritePosition);
    }
}
