using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>Un grupo en el panel: su composición dibujada y la edición de sus miembros.</summary>
public partial class SpriteGroupViewModel : ObservableObject
{
    private readonly SpriteBank _bank;
    private readonly PaletteLibrary _palettes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveMemberCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMemberUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMemberDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreColorsCommand))]
    [NotifyPropertyChangedFor(nameof(MemberColor))]
    private SpriteGroupMember? _selectedMember;

    public SpriteGroupViewModel(SpriteGroup group, SpriteBank bank, PaletteLibrary palettes)
    {
        Group = group;
        _bank = bank;
        _palettes = palettes;
        Preview = SpriteGroupRenderer.CreatePreview();

        for (int row = 0; row < Sprite.Rows; row++)
        {
            MemberColors.Add(new SpriteMemberColorViewModel(
                row, new SpriteAttributeRow(), palettes, OnMemberColorPicked));
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

    /// <summary>La composición de los miembros, ya dibujada.</summary>
    public ImageMini Preview { get; }

    /// <summary>Último patrón del banco al que puede apuntar un miembro.</summary>
    public int MaxPatternIndex => _bank.SpritesList.Count - 1;

    /// <summary>Una casilla por línea del miembro seleccionado. Sólo en MSX2.</summary>
    public ObservableCollection<SpriteMemberColorViewModel> MemberColors { get; } = [];

    /// <summary>En MSX1 el color es de todo el sprite, no de cada línea.</summary>
    public bool IsMsx1 => _bank.Type == SpriteBank.SpriteType.MSX;

    public bool IsMsx2 => !IsMsx1;

    /// <summary>El color del miembro en MSX1, donde las 16 líneas comparten el mismo.</summary>
    public PaletteColor? MemberColor =>
        SelectedMember is null ? null : _palettes.ActivePalette[SelectedMember.Rows[0].Color];

    public IReadOnlyList<PaletteColor> Palette => _palettes.ActivePalette.Colors;

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

    public void Render(ColorPalette palette, Color background) =>
        SpriteGroupRenderer.Render(Group, _bank, palette, background, Preview);

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
