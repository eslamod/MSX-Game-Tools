using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;
using Avalonia.Headless.XUnit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lógica del banco de sprites. No necesita plataforma gráfica: ImageMini sólo
/// construye el bitmap cuando alguien lee SpritePreview.
/// </summary>
public class SpritesEditorViewModelTests
{
    [AvaloniaFact]
    public void Un_banco_nuevo_muestra_la_miniatura_de_su_primer_sprite()
    {
        SpritesEditorViewModel vm = NewEditor();

        // En WPF la lista se creaba vacía y esta miniatura no aparecía nunca.
        Assert.Single(vm.ImagesMiniList);
        Assert.Equal(1, vm.NumberSprites);
        Assert.Equal(1, vm.CurrentSpritePosition);
    }

    [AvaloniaFact]
    public void Anadir_sprite_actualiza_contador_y_miniaturas()
    {
        SpritesEditorViewModel vm = NewEditor();

        vm.AddSpriteCommand.Execute(null);

        Assert.Equal(2, vm.NumberSprites);
        Assert.Equal(2, vm.ImagesMiniList.Count);
    }

    [AvaloniaFact]
    public void El_sprite_recien_anadido_queda_seleccionado()
    {
        SpritesEditorViewModel vm = NewEditor();

        vm.AddSpriteCommand.Execute(null);

        Assert.Equal(2, vm.CurrentSpritePosition);
        Assert.Same(vm.SpritesBank.SpritesList[1], vm.CurrentSprite);
        Assert.Same(vm.SpritesBank.SpritesList[1].ImageMini, vm.SelectedThumbnail);
    }

    [AvaloniaFact]
    public async Task Borrar_sprite_elimina_tambien_su_miniatura()
    {
        SpritesEditorViewModel vm = NewEditor();
        vm.AddSpriteCommand.Execute(null);
        vm.AddSpriteCommand.Execute(null);
        Assert.Equal(3, vm.ImagesMiniList.Count);

        await vm.DeleteSpriteCommand.ExecuteAsync(null);

        // En WPF se borraba del banco pero no de la lista, y se desincronizaban.
        Assert.Equal(2, vm.NumberSprites);
        Assert.Equal(2, vm.ImagesMiniList.Count);
    }

    [AvaloniaFact]
    public void No_se_puede_borrar_el_ultimo_sprite()
    {
        SpritesEditorViewModel vm = NewEditor();

        Assert.False(vm.DeleteSpriteCommand.CanExecute(null));

        vm.AddSpriteCommand.Execute(null);
        Assert.True(vm.DeleteSpriteCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void No_se_pueden_anadir_mas_sprites_de_los_que_admite_el_banco()
    {
        SpritesEditorViewModel vm = NewEditor();

        for (int i = 1; i < SpriteBank.MaxSprites; i++)
            vm.AddSpriteCommand.Execute(null);

        Assert.Equal(SpriteBank.MaxSprites, vm.NumberSprites);
        Assert.False(vm.AddSpriteCommand.CanExecute(null));

        // En WPF esto lanzaba NullReferenceException: NewSprite devolvía null.
        vm.AddSpriteCommand.Execute(null);

        Assert.Equal(SpriteBank.MaxSprites, vm.NumberSprites);
        Assert.Equal(SpriteBank.MaxSprites, vm.ImagesMiniList.Count);
    }

    [AvaloniaFact]
    public void La_navegacion_respeta_los_extremos()
    {
        SpritesEditorViewModel vm = NewEditor();
        vm.AddSpriteCommand.Execute(null); // deja seleccionado el 2 de 2

        Assert.Equal(2, vm.CurrentSpritePosition);
        Assert.False(vm.NextSpriteCommand.CanExecute(null));
        Assert.True(vm.PreviousSpriteCommand.CanExecute(null));

        vm.PreviousSpriteCommand.Execute(null);

        Assert.Equal(1, vm.CurrentSpritePosition);
        Assert.False(vm.PreviousSpriteCommand.CanExecute(null));
        Assert.True(vm.NextSpriteCommand.CanExecute(null));

        vm.NextSpriteCommand.Execute(null);

        Assert.Equal(2, vm.CurrentSpritePosition);
    }

    [AvaloniaFact]
    public void Cambiar_de_sprite_avisa_a_la_vista()
    {
        SpritesEditorViewModel vm = NewEditor();
        vm.AddSpriteCommand.Execute(null);

        int notifications = 0;
        vm.RefreshRequested += _ => notifications++;

        vm.PreviousSpriteCommand.Execute(null);
        vm.NextSpriteCommand.Execute(null);

        Assert.Equal(2, notifications);
        Assert.Same(vm.SpritesBank.SpritesList[1], vm.CurrentSprite);
    }

    [AvaloniaFact]
    public async Task Borrar_pide_confirmacion_diciendo_que_sprite_es()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        SpritesEditorViewModel vm = new(new SpriteBank(), ColorPalette.CreateMsxStandard(), dialogs);
        vm.AddSpriteCommand.Execute(null); // quedan 2, seleccionado el 2

        await vm.DeleteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Contains("sprite 2 de 2", dialogs.LastConfirmMessage);
        Assert.Equal(1, vm.NumberSprites);
    }

    [AvaloniaFact]
    public async Task Cancelar_la_confirmacion_no_borra_el_sprite()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        SpritesEditorViewModel vm = new(new SpriteBank(), ColorPalette.CreateMsxStandard(), dialogs);
        vm.AddSpriteCommand.Execute(null);
        Sprite current = vm.CurrentSprite;

        await vm.DeleteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Equal(2, vm.NumberSprites);
        Assert.Equal(2, vm.ImagesMiniList.Count);
        Assert.Same(current, vm.CurrentSprite);
        Assert.Equal(2, vm.CurrentSpritePosition);
    }

    private static SpritesEditorViewModel NewEditor() =>
        new(new SpriteBank(), ColorPalette.CreateMsxStandard());
}
