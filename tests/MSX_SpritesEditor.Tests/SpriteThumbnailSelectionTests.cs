using Avalonia.Headless.XUnit;
using MSX_SpritesEditor.Entities;
using Xunit;
using static MSX_SpritesEditor.Tests.SpriteCanvasHarness;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Selección de miniaturas y su sincronía con el lienzo de edición, en los dos sentidos.
/// </summary>
public class SpriteThumbnailSelectionTests
{
    [AvaloniaFact]
    public void Al_abrir_un_banco_la_primera_miniatura_sale_seleccionada()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);

        Assert.Equal(0, editor.Thumbnails.SelectedIndex);
        Assert.Equal(1, editor.ViewModel.CurrentSpritePosition);
        Assert.Same(editor.Bank.SpritesList[0].ImageMini, editor.ViewModel.SelectedThumbnail);
    }

    [AvaloniaFact]
    public void Pulsar_una_miniatura_lleva_ese_sprite_al_lienzo_de_edicion()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        AddSprites(editor, 3);

        editor.ClickThumbnail(2);

        Assert.Equal(3, editor.ViewModel.CurrentSpritePosition);
        Assert.Same(editor.Bank.SpritesList[2], editor.ViewModel.CurrentSprite);
        Assert.Equal(2, editor.Thumbnails.SelectedIndex);
    }

    [AvaloniaFact]
    public void Pulsar_una_miniatura_repinta_el_lienzo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        AddSprites(editor, 2);

        // Pintamos en el sprite 3 y volvemos al 1: el lienzo debe reflejar el sprite 1.
        editor.ClickThumbnail(2);
        editor.Press(4, 4);
        editor.Release(4, 4);
        Assert.Equal([4], editor.PaintedCellsInRow(4, sprite: 2));

        editor.ClickThumbnail(0);

        Assert.Same(editor.Bank.SpritesList[0], editor.ViewModel.CurrentSprite);
        Assert.Empty(editor.PaintedCellsInRow(4, sprite: 0));
    }

    [AvaloniaFact]
    public void Anadir_un_sprite_selecciona_su_miniatura()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        Assert.Equal(0, editor.Thumbnails.SelectedIndex);

        editor.ViewModel.AddSpriteCommand.Execute(null);
        editor.ThumbnailContainer(1);

        Assert.Equal(1, editor.Thumbnails.SelectedIndex);
        Assert.Same(editor.Bank.SpritesList[1], editor.ViewModel.CurrentSprite);
    }

    [AvaloniaFact]
    public void Navegar_con_los_botones_mueve_la_seleccion_de_las_miniaturas()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        AddSprites(editor, 2); // 3 sprites, seleccionado el ultimo

        Assert.Equal(2, editor.Thumbnails.SelectedIndex);

        editor.ViewModel.PreviousSpriteCommand.Execute(null);
        Assert.Equal(1, editor.Thumbnails.SelectedIndex);

        editor.ViewModel.PreviousSpriteCommand.Execute(null);
        Assert.Equal(0, editor.Thumbnails.SelectedIndex);

        editor.ViewModel.NextSpriteCommand.Execute(null);
        Assert.Equal(1, editor.Thumbnails.SelectedIndex);
    }

    [AvaloniaFact]
    public void Pasar_el_raton_por_encima_marca_la_miniatura()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        AddSprites(editor, 2);

        editor.HoverThumbnail(1);

        Assert.True(editor.ThumbnailContainer(1).IsPointerOver);
        Assert.False(editor.ThumbnailContainer(0).IsPointerOver);

        editor.HoverThumbnail(0);

        Assert.True(editor.ThumbnailContainer(0).IsPointerOver);
        Assert.False(editor.ThumbnailContainer(1).IsPointerOver);
    }

    [AvaloniaFact]
    public void Al_borrar_la_seleccion_pasa_al_sprite_que_ocupa_esa_posicion()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        AddSprites(editor, 2);

        Sprite tercero = editor.Bank.SpritesList[2];
        editor.ClickThumbnail(1);
        Assert.Equal(2, editor.ViewModel.CurrentSpritePosition);

        editor.ViewModel.DeleteSpriteCommand.Execute(null);

        Assert.Equal(2, editor.ViewModel.NumberSprites);
        Assert.Equal(2, editor.Thumbnails.ItemCount);
        Assert.Equal(2, editor.ViewModel.CurrentSpritePosition);
        Assert.Same(tercero, editor.ViewModel.CurrentSprite);
        Assert.Equal(1, editor.Thumbnails.SelectedIndex);
    }

    [AvaloniaFact]
    public void Al_borrar_el_ultimo_la_seleccion_retrocede()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        AddSprites(editor, 2);

        editor.ClickThumbnail(2);
        editor.ViewModel.DeleteSpriteCommand.Execute(null);

        Assert.Equal(2, editor.ViewModel.CurrentSpritePosition);
        Assert.Equal(1, editor.Thumbnails.SelectedIndex);
        Assert.Same(editor.Bank.SpritesList[1], editor.ViewModel.CurrentSprite);
    }

    private static void AddSprites(SpriteCanvasHarness editor, int count)
    {
        for (int i = 0; i < count; i++)
            editor.ViewModel.AddSpriteCommand.Execute(null);

        // Deja que el ListBox realice los contenedores nuevos.
        editor.ThumbnailContainer(count);
    }
}
