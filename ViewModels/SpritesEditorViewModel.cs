using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

public partial class SpritesEditorViewModel : PanelBaseViewModel
{
    private readonly SpriteBank _spriteBank;

    /// <summary>La vista se resuscribe para repintar el lienzo al cambiar de sprite.</summary>
    public event Action<Sprite>? RefreshRequested;

    [ObservableProperty]
    private Sprite _currentSprite;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextSpriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousSpriteCommand))]
    private int _currentSpritePosition;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextSpriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSpriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSpriteCommand))]
    private int _numberSprites;

    public SpritesEditorViewModel(SpriteBank bank, ColorPalette colorPalette)
    {
        _spriteBank = bank;
        ColorPalette = colorPalette;

        _currentSprite = bank.SpritesList[0];
        _currentSpritePosition = 1;
        _numberSprites = bank.SpritesList.Count;

        // La versión WPF creaba la lista vacía, así que la miniatura del primer
        // sprite del banco nunca llegaba a aparecer.
        foreach (Sprite sprite in bank.SpritesList)
        {
            if (sprite.ImageMini is not null)
                ImagesMiniList.Add(sprite.ImageMini);
        }
    }

    public SpriteBank SpritesBank => _spriteBank;

    public ColorPalette ColorPalette { get; set; }

    public ObservableCollection<ImageMini> ImagesMiniList { get; } = [];

    [RelayCommand(CanExecute = nameof(CanAddSprite))]
    private void AddSprite()
    {
        Sprite? sprite = _spriteBank.NewSprite();
        if (sprite?.ImageMini is null)
            return;

        ImagesMiniList.Add(sprite.ImageMini);
        NumberSprites = _spriteBank.SpritesList.Count;
    }

    private bool CanAddSprite() => NumberSprites < SpriteBank.MaxSprites;

    [RelayCommand(CanExecute = nameof(CanDeleteSprite))]
    private void DeleteSprite()
    {
        int index = CurrentSpritePosition - 1;

        _spriteBank.DeleteSprite(index);
        if ((uint)index < (uint)ImagesMiniList.Count)
            ImagesMiniList.RemoveAt(index);

        NumberSprites = _spriteBank.SpritesList.Count;
        if (CurrentSpritePosition > NumberSprites)
            CurrentSpritePosition = NumberSprites;

        GoTo(CurrentSpritePosition);
    }

    // Un banco siempre conserva al menos un sprite: si no, el editor se queda sin
    // nada que dibujar (en WPF se podía vaciar y el lienzo apuntaba a un sprite muerto).
    private bool CanDeleteSprite() => NumberSprites > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextSprite() => GoTo(CurrentSpritePosition + 1);

    private bool CanGoNext() => CurrentSpritePosition < NumberSprites;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousSprite() => GoTo(CurrentSpritePosition - 1);

    private bool CanGoPrevious() => CurrentSpritePosition > 1;

    private void GoTo(int position)
    {
        if (position < 1 || position > NumberSprites)
            return;

        CurrentSpritePosition = position;
        CurrentSprite = _spriteBank.SpritesList[position - 1];
        RefreshRequested?.Invoke(CurrentSprite);
    }
}
