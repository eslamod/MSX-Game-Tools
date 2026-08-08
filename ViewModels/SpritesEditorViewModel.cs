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

    /// <summary>
    /// Miniatura seleccionada en la tira. Enlazada al SelectedItem del ListBox en los
    /// dos sentidos: es lo que mantiene sincronizados el lienzo de edición y la tira.
    /// </summary>
    /// <remarks>
    /// Por identidad y no por índice a propósito. Al borrar el sprite seleccionado el
    /// ListBox se limpia solo y escribe el hueco en el ViewModel; si esto fuera un
    /// índice, el valor de vuelta coincidiría con el que ya tenía el enlace y la
    /// selección no se recuperaría nunca.
    /// </remarks>
    [ObservableProperty]
    private ImageMini? _selectedThumbnail;

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

        _selectedThumbnail = _currentSprite.ImageMini;
    }

    /// <summary>Al pulsar una miniatura, el lienzo pasa a editar ese sprite.</summary>
    partial void OnSelectedThumbnailChanged(ImageMini? value)
    {
        // null llega cuando el ListBox limpia su selección al borrarse el elemento
        // seleccionado. Lo ignoramos: DeleteSprite reasigna la selección acto seguido.
        if (value is null)
            return;

        int index = ImagesMiniList.IndexOf(value);
        if (index >= 0)
            GoTo(index + 1);
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

        // Se queda en la misma posición, que ahora ocupa el sprite siguiente,
        // salvo que se hubiera borrado el último.
        GoTo(Math.Min(index + 1, NumberSprites));
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

        Sprite target = _spriteBank.SpritesList[position - 1];

        // Comparar también el sprite y no sólo la posición: al borrar, la posición
        // puede no cambiar pero el sprite que la ocupa sí, y hay que repintar.
        // Y al revés, el enlace bidireccional de la lista de miniaturas reescribe
        // el mismo índice constantemente y no debe provocar repintados.
        if (CurrentSpritePosition == position && ReferenceEquals(CurrentSprite, target))
            return;

        CurrentSpritePosition = position;
        CurrentSprite = target;
        SelectedThumbnail = target.ImageMini;

        RefreshRequested?.Invoke(CurrentSprite);
    }
}
