using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;
using Avalonia.Headless.XUnit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lógica del banco de sprites. No necesita plataforma gráfica: ImageMini sólo
/// construye el bitmap cuando alguien lee SpritePreview.
/// </summary>
/// <remarks>
/// El banco tiene 64 huecos fijos, como la tabla de patrones del VDP, así que aquí no hay
/// nada que añadir ni que quitar: sólo recorrerlos. Lo de vaciar y duplicar está en
/// <see cref="DuplicateSpriteTests" />.
/// </remarks>
public class SpritesEditorViewModelTests
{
    [AvaloniaFact]
    public void Un_banco_nuevo_muestra_la_miniatura_de_su_primer_sprite()
    {
        SpritesEditorViewModel vm = NewEditor();

        // En WPF la lista se creaba vacía y esta miniatura no aparecía nunca.
        Assert.Equal(SpriteBank.MaxSprites, vm.ImagesMiniList.Count);
        Assert.Equal(SpriteBank.MaxSprites, vm.NumberSprites);
        Assert.Equal(0, vm.CurrentSpriteIndex);
        Assert.Same(vm.SpritesBank.SpritesList[0].ImageMini, vm.SelectedThumbnail);
    }

    /// <summary>
    /// Los 64 huecos están desde el principio, con su miniatura cada uno.
    /// </summary>
    /// <remarks>
    /// Antes el banco arrancaba con uno y se iban añadiendo. Que estén todos desde el
    /// principio es lo que hace que ningún índice se pueda mover nunca.
    /// </remarks>
    [AvaloniaFact]
    public void Los_sesenta_y_cuatro_huecos_estan_desde_el_principio()
    {
        SpritesEditorViewModel vm = NewEditor();

        Assert.Equal(SpriteBank.MaxSprites, vm.SpritesBank.SpritesList.Count);
        Assert.All(vm.SpritesBank.SpritesList, sprite => Assert.NotNull(sprite.ImageMini));
    }

    [AvaloniaFact]
    public void Avanzar_deja_seleccionado_el_siguiente()
    {
        SpritesEditorViewModel vm = NewEditor();

        vm.NextSpriteCommand.Execute(null);

        Assert.Equal(1, vm.CurrentSpriteIndex);
        Assert.Same(vm.SpritesBank.SpritesList[1], vm.CurrentSprite);
        Assert.Same(vm.SpritesBank.SpritesList[1].ImageMini, vm.SelectedThumbnail);
    }

    /// <summary>
    /// Los patrones se numeran de 0 a 63, como los numera el hardware.
    /// </summary>
    /// <remarks>
    /// Es el número que se escribe en el código del juego y el que ya usaban los grupos, que
    /// contaban desde 0 mientras el editor enseñaba «1 / 64» para el mismo patrón. El número
    /// que se ve tiene que ser el que se escribe.
    /// </remarks>
    [AvaloniaFact]
    public void Los_patrones_se_numeran_desde_cero()
    {
        SpritesEditorViewModel vm = NewEditor();

        Assert.Equal(0, vm.CurrentSpriteIndex);
        Assert.Equal(SpriteBank.MaxSprites - 1, vm.LastSpriteIndex);

        // Y el número que se enseña es el hueco del banco, sin desfase.
        vm.NextSpriteCommand.Execute(null);

        Assert.Same(vm.SpritesBank.SpritesList[vm.CurrentSpriteIndex], vm.CurrentSprite);
    }

    /// <summary>La navegación se para en el primero y en el último de los 64.</summary>
    [AvaloniaFact]
    public void La_navegacion_respeta_los_extremos()
    {
        SpritesEditorViewModel vm = NewEditor();

        Assert.False(vm.PreviousSpriteCommand.CanExecute(null));
        Assert.True(vm.NextSpriteCommand.CanExecute(null));

        for (int index = 1; index < SpriteBank.MaxSprites; index++)
            vm.NextSpriteCommand.Execute(null);

        Assert.Equal(SpriteBank.MaxSprites - 1, vm.CurrentSpriteIndex);
        Assert.False(vm.NextSpriteCommand.CanExecute(null));

        // Y pasado el último no se sale: en WPF esto lanzaba NullReferenceException.
        vm.NextSpriteCommand.Execute(null);

        Assert.Equal(SpriteBank.MaxSprites - 1, vm.CurrentSpriteIndex);
    }

    [AvaloniaFact]
    public void Cambiar_de_sprite_avisa_a_la_vista()
    {
        SpritesEditorViewModel vm = NewEditor();
        vm.NextSpriteCommand.Execute(null);

        int notifications = 0;
        vm.RefreshRequested += _ => notifications++;

        vm.PreviousSpriteCommand.Execute(null);
        vm.NextSpriteCommand.Execute(null);

        Assert.Equal(2, notifications);
        Assert.Same(vm.SpritesBank.SpritesList[1], vm.CurrentSprite);
    }

    private static SpritesEditorViewModel NewEditor() =>
        new(new SpriteBank(), ColorPalette.CreateMsxStandard());
}
