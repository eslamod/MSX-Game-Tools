using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>Un grupo en el panel: su composición dibujada y la edición de sus miembros.</summary>
public partial class SpriteGroupViewModel : ObservableObject
{
    private readonly SpriteBank _bank;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveMemberCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMemberUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMemberDownCommand))]
    private SpriteGroupMember? _selectedMember;

    public SpriteGroupViewModel(SpriteGroup group, SpriteBank bank)
    {
        Group = group;
        _bank = bank;
        Preview = SpriteGroupRenderer.CreatePreview();

        _selectedMember = group.Members.FirstOrDefault();

        Group.Changed += _ =>
        {
            AddMemberCommand.NotifyCanExecuteChanged();
            RemoveMemberCommand.NotifyCanExecuteChanged();
            MoveMemberUpCommand.NotifyCanExecuteChanged();
            MoveMemberDownCommand.NotifyCanExecuteChanged();
        };
    }

    public SpriteGroup Group { get; }

    /// <summary>La composición de los miembros, ya dibujada.</summary>
    public ImageMini Preview { get; }

    /// <summary>Último patrón del banco al que puede apuntar un miembro.</summary>
    public int MaxPatternIndex => _bank.SpritesList.Count - 1;

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
