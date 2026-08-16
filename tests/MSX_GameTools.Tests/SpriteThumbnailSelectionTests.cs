using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

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
        GoToSprite(editor, 3);

        editor.ClickThumbnail(2);

        Assert.Equal(3, editor.ViewModel.CurrentSpritePosition);
        Assert.Same(editor.Bank.SpritesList[2], editor.ViewModel.CurrentSprite);
        Assert.Equal(2, editor.Thumbnails.SelectedIndex);
    }

    [AvaloniaFact]
    public void Pulsar_una_miniatura_repinta_el_lienzo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        GoToSprite(editor, 2);

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

        editor.ViewModel.NextSpriteCommand.Execute(null);
        editor.ThumbnailContainer(1);

        Assert.Equal(1, editor.Thumbnails.SelectedIndex);
        Assert.Same(editor.Bank.SpritesList[1], editor.ViewModel.CurrentSprite);
    }

    [AvaloniaFact]
    public void Navegar_con_los_botones_mueve_la_seleccion_de_las_miniaturas()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        GoToSprite(editor, 2); // 3 sprites, seleccionado el ultimo

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
        GoToSprite(editor, 2);

        editor.HoverThumbnail(1);

        Assert.True(editor.ThumbnailContainer(1).IsPointerOver);
        Assert.False(editor.ThumbnailContainer(0).IsPointerOver);

        editor.HoverThumbnail(0);

        Assert.True(editor.ThumbnailContainer(0).IsPointerOver);
        Assert.False(editor.ThumbnailContainer(1).IsPointerOver);
    }

    /// <summary>
    /// Vaciar un patrón no mueve la selección: sigue siendo el mismo hueco.
    /// </summary>
    /// <remarks>
    /// Aquí había dos comprobaciones de a dónde saltaba la selección al borrar —una para
    /// el de en medio y otra para el último—. Ya no hay borrar: los 64 huecos están
    /// siempre, y vaciar deja el que estabas mirando donde estaba.
    /// </remarks>
    [AvaloniaFact]
    public async Task Al_vaciar_la_seleccion_se_queda_donde_estaba()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag);
        GoToSprite(editor, 2);

        editor.ClickThumbnail(1);
        Assert.Equal(2, editor.ViewModel.CurrentSpritePosition);

        Sprite current = editor.ViewModel.CurrentSprite;

        await editor.ViewModel.ClearSpriteCommand.ExecuteAsync(null);

        Assert.Equal(2, editor.ViewModel.CurrentSpritePosition);
        Assert.Same(current, editor.ViewModel.CurrentSprite);
        Assert.Equal(1, editor.Thumbnails.SelectedIndex);
    }

    private static void GoToSprite(SpriteCanvasHarness editor, int count)
    {
        for (int i = 0; i < count; i++)
            editor.ViewModel.NextSpriteCommand.Execute(null);

        // Deja que el ListBox realice los contenedores.
        editor.ThumbnailContainer(count);
    }
}
