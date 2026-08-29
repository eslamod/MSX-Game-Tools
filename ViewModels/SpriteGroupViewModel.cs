using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>Un grupo en el panel: su composición dibujada y la edición de sus miembros.</summary>
public partial class SpriteGroupViewModel : ObservableObject
{
    private readonly SpriteBank _bank;
    private readonly Func<ColorPalette> _palette;
    private readonly ReferenceImageLibrary _backgrounds;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveMemberCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMemberUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMemberDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreColorsCommand))]
    [NotifyPropertyChangedFor(nameof(MemberColor))]
    private SpriteGroupMember? _selectedMember;

    /// <inheritdoc cref="TileRowColorViewModel(int, TileRow, Func{ColorPalette}, Action{int})" path="/param[@name='palette']"/>
    public SpriteGroupViewModel(
        SpriteGroup group,
        SpriteBank bank,
        Func<ColorPalette> palette,
        ReferenceImageLibrary backgrounds,
        IDialogService dialogs)
    {
        Group = group;
        _bank = bank;
        _palette = palette;
        _backgrounds = backgrounds;

        Background = new BackgroundSelectionViewModel(
            backgrounds, dialogs, () => group.Background, reference => group.Background = reference);

        Background.Changed += () => OnPropertyChanged(nameof(BackgroundTile));
        Preview = SpriteGroupRenderer.CreatePreview(group);

        for (int row = 0; row < Sprite.Rows; row++)
        {
            MemberColors.Add(new SpriteMemberColorViewModel(
                row, new SpriteAttributeRow(), palette, OnMemberColorPicked));
        }

        // Por la propiedad y no por el campo, para que quede enganchado el seguimiento
        // del patrón que hay que llevar al lienzo.
        SelectedMember = group.Members.FirstOrDefault();

        Group.Changed += _ =>
        {
            AddMemberCommand.NotifyCanExecuteChanged();
            RemoveMemberCommand.NotifyCanExecuteChanged();
            MoveMemberUpCommand.NotifyCanExecuteChanged();
            MoveMemberDownCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>
    /// Hay que llevar el lienzo a este patrón: o se ha seleccionado otro miembro, o el
    /// seleccionado ha cambiado de patrón. Elegir el modo Grupos no impide seguir
    /// dibujando, y así se sabe siempre qué patrón se está tocando.
    /// </summary>
    public event Action<SpriteGroupMember>? EditTargetChanged;

    public SpriteGroup Group { get; }

    /// <summary>Los fondos disponibles, para el desplegable del panel.</summary>
    public ReferenceImageLibrary Backgrounds => _backgrounds;

    /// <summary>
    /// Imagen de referencia que se ve detrás de la composición.
    /// </summary>
    /// <remarks>
    /// El grupo guarda la ruta y el número de celda, no la celda: es lo que sobrevive a
    /// cerrar el editor. Aquí se resuelve contra la biblioteca, y si esa imagen ya no
    /// está cargada sale <c>null</c> y el grupo se dibuja sin fondo.
    /// </remarks>
    public ReferenceTile? BackgroundTile
    {
        get => Background.Tile;
        set => Background.Apply(value?.Ref ?? BackgroundRef.None);
    }

    /// <summary>El desplegable de imágenes y el botón de celda del panel.</summary>
    public BackgroundSelectionViewModel Background { get; }

    /// <summary>
    /// Opacidad de los sprites sobre el fondo. Sirve para ver a la vez lo que estás
    /// dibujando y el dibujo que hay debajo.
    /// </summary>
    [ObservableProperty]
    private double _spriteOpacity = 1.0;

    /// <summary>La composición de los miembros, ya dibujada.</summary>
    public ImageMini Preview { get; private set; }

    /// <summary>Último patrón del banco al que puede apuntar un miembro.</summary>
    public int MaxPatternIndex => _bank.SpritesList.Count - 1;

    /// <summary>Una casilla por línea del miembro seleccionado. Sólo en MSX2.</summary>
    public ObservableCollection<SpriteMemberColorViewModel> MemberColors { get; } = [];

    /// <summary>En MSX1 el color es de todo el sprite, no de cada línea.</summary>
    public bool IsMsx1 => _bank.Type == SpriteBank.SpriteType.MSX;

    public bool IsMsx2 => !IsMsx1;

    /// <summary>El banco ha cambiado de máquina, y con ella lo que se enseña de un grupo.</summary>
    public void MachineChanged()
    {
        OnPropertyChanged(nameof(IsMsx1));
        OnPropertyChanged(nameof(IsMsx2));
    }

    /// <summary>El color del miembro en MSX1, donde las 16 líneas comparten el mismo.</summary>
    public PaletteColor? MemberColor =>
        SelectedMember is null ? null : _palette()[SelectedMember.Rows[0].Color];

    public IReadOnlyList<PaletteColor> Palette => _palette().Colors;

    /// <summary>Vuelve a sembrar los colores del miembro desde su patrón.</summary>
    [RelayCommand(CanExecute = nameof(CanRestoreColors))]
    private void RestoreColors()
    {
        if (SelectedMember is null || (uint)SelectedMember.PatternIndex >= (uint)_bank.SpritesList.Count)
            return;

        SelectedMember.CopyColorsFrom(_bank.SpritesList[SelectedMember.PatternIndex]);
        RefreshMemberColors();
    }

    private bool CanRestoreColors() => SelectedMember is not null;

    /// <summary>MSX1: el color elegido va a las 16 líneas del miembro.</summary>
    [RelayCommand]
    private void PickMemberColor(PaletteColor? color)
    {
        if (color is null || SelectedMember is null)
            return;

        foreach (SpriteAttributeRow row in SelectedMember.Rows)
            row.Color = color.Index;

        RefreshMemberColors();
    }

    /// <summary>MSX2: el color elegido va sólo a esa línea.</summary>
    private void OnMemberColorPicked(int rowIndex, PaletteColor color)
    {
        if (SelectedMember is null)
            return;

        SelectedMember.Rows[rowIndex].Color = color.Index;
        RefreshMemberColors();
    }

    /// <summary>Reengancha las casillas al miembro y las repinta.</summary>
    public void RefreshMemberColors()
    {
        for (int row = 0; row < MemberColors.Count; row++)
        {
            if (SelectedMember is not null)
                MemberColors[row].Attach(SelectedMember.Rows[row]);
            else
                MemberColors[row].Refresh();
        }

        OnPropertyChanged(nameof(MemberColor));
        OnPropertyChanged(nameof(Palette));
    }

    partial void OnSelectedMemberChanged(SpriteGroupMember? oldValue, SpriteGroupMember? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnSelectedMemberPropertyChanged;

        if (newValue is null)
            return;

        newValue.PropertyChanged += OnSelectedMemberPropertyChanged;

        RefreshMemberColors();
        EditTargetChanged?.Invoke(newValue);
    }

    private void OnSelectedMemberPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpriteGroupMember.PatternIndex) && SelectedMember is not null)
            EditTargetChanged?.Invoke(SelectedMember);
    }

    /// <remarks>
    /// Con imagen de referencia el hueco se deja transparente en vez de pintarlo del
    /// color de fondo. El renderizador ya trata igual las celdas vacías y el color 0,
    /// que en el MSX es el transparente de verdad, así que con esto la composición pasa
    /// a ser una capa que deja ver lo de debajo sin tocar nada más.
    /// </remarks>
    public void Render(ColorPalette palette, Color background)
    {
        // El lienzo sale de lo que ocupa el grupo, asi que sacar un plano de la figura puede
        // hacerlo mas grande. La imagen no se redimensiona: se cambia por otra, y hay que
        // avisar para que la vista deje de ensenar la anterior.
        (int width, int height, _, _) = SpriteGroupRenderer.CanvasOf(Group);

        if (Preview.Width != width || Preview.Height != height)
        {
            Preview = SpriteGroupRenderer.CreatePreview(Group);

            OnPropertyChanged(nameof(Preview));
        }

        SpriteGroupRenderer.Render(
            Group, _bank, palette, Background.VisibleTile is null ? background : Colors.Transparent, Preview);
    }

    /// <summary>
    /// Añade un sprite más con el mismo patrón que el seleccionado. Repetir el patrón
    /// con un desplazamiento pequeño y otro color es justo la técnica del contorno.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddMember))]
    private void AddMember()
    {
        int patternIndex = SelectedMember?.PatternIndex ?? 0;
        if ((uint)patternIndex >= (uint)_bank.SpritesList.Count)
            return;

        var member = new SpriteGroupMember(patternIndex, _bank.SpritesList[patternIndex]);

        if (Group.Add(member))
            SelectedMember = member;
    }

    private bool CanAddMember() => Group.CanAddMember;

    [RelayCommand(CanExecute = nameof(CanRemoveMember))]
    private void RemoveMember()
    {
        if (SelectedMember is null)
            return;

        int index = Group.Members.IndexOf(SelectedMember);
        if (!Group.Remove(SelectedMember))
            return;

        SelectedMember = Group.Members[Math.Min(index, Group.Members.Count - 1)];
    }

    private bool CanRemoveMember() => SelectedMember is not null && Group.CanRemoveMember;

    [RelayCommand(CanExecute = nameof(CanMoveMemberUp))]
    private void MoveMemberUp() => Group.MoveUp(SelectedMember!);

    private bool CanMoveMemberUp() => SelectedMember is not null && Group.Members.IndexOf(SelectedMember) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveMemberDown))]
    private void MoveMemberDown() => Group.MoveDown(SelectedMember!);

    private bool CanMoveMemberDown() =>
        SelectedMember is not null && Group.Members.IndexOf(SelectedMember) < Group.Members.Count - 1;

    /// <param name="direction">"left", "right", "up" o "down".</param>
    [RelayCommand]
    private void NudgeOffset(string direction)
    {
        if (SelectedMember is null)
            return;

        switch (direction)
        {
            case "left": SelectedMember.OffsetX--; break;
            case "right": SelectedMember.OffsetX++; break;
            case "up": SelectedMember.OffsetY--; break;
            case "down": SelectedMember.OffsetY++; break;
        }
    }

    /// <param name="delta">"-1" o "+1" sobre el patrón del miembro seleccionado.</param>
    [RelayCommand]
    private void StepPattern(string delta)
    {
        if (SelectedMember is null || !int.TryParse(delta, out int step))
            return;

        SelectedMember.PatternIndex = Math.Clamp(SelectedMember.PatternIndex + step, 0, MaxPatternIndex);
    }
}
