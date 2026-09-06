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

    /// <summary>Donde van los cambios de la lista de planos, para poder deshacerlos.</summary>
    private readonly PixelUndoStack? _undo;
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
    /// <param name="undo">
    /// La historia del banco, donde van los cambios en la lista de planos. Sin ella el panel
    /// funciona igual pero no se deshace nada, que es como se monta suelto en una prueba.
    /// </param>
    public SpriteGroupViewModel(
        SpriteGroup group,
        SpriteBank bank,
        Func<ColorPalette> palette,
        ReferenceImageLibrary backgrounds,
        IDialogService dialogs,
        PixelUndoStack? undo = null)
    {
        Group = group;
        _bank = bank;
        _palette = palette;
        _backgrounds = backgrounds;
        _undo = undo;

        // Deshacer rehace la lista entera, así que el plano elegido puede haberse ido: sin
        // esto el panel seguiría enseñando los desplazamientos de uno que ya no está.
        group.Members.CollectionChanged += (_, _) => KeepSelection();

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

    /// <summary>Lo que se puede sacar un plano del grupo, para las cajas de desplazamiento.</summary>
    public int MinOffset => SpriteGroupMember.MinOffset;

    /// <inheritdoc cref="MinOffset"/>
    public int MaxOffset => SpriteGroupMember.MaxOffset;

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
    private void AddMember() => AddMember(SelectedMember?.PatternIndex ?? 0);

    /// <summary>
    /// Añade un plano con ese patrón. Devuelve si ha cabido.
    /// </summary>
    /// <remarks>
    /// Es lo que hace falta para traer un patrón arrastrándolo desde la tira: el que se suelta
    /// no tiene nada que ver con el que hubiera seleccionado.
    /// </remarks>
    public bool AddMember(int patternIndex)
    {
        // El tope lo pone el grupo al añadir, no hace falta preguntarlo aquí: se comprobó
        // quitando la comprobación y no cayó ninguna prueba, porque Add ya devuelve false.
        if ((uint)patternIndex >= (uint)_bank.SpritesList.Count)
            return false;

        var member = new SpriteGroupMember(patternIndex, _bank.SpritesList[patternIndex]);

        bool added = false;

        Recording(() =>
        {
            added = Group.Add(member);

            if (added)
                SelectedMember = member;
        });

        return added;
    }

    private bool CanAddMember() => Group.CanAddMember;

    /// <summary>
    /// Quita el plano elegido.
    /// </summary>
    /// <remarks>
    /// Sin preguntar: quitar uno queriendo es lo normal -al montar una figura se prueban
    /// planos y se descartan-, y preguntar cada vez convierte quitar ocho en ocho diálogos.
    /// Lo que hace que no preguntar sea aceptable es que se deshace, con sus desplazamientos
    /// y sus colores, que es lo que costaba volver a poner.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRemoveMember))]
    private void RemoveMember()
    {
        if (SelectedMember is null)
            return;

        int index = Group.Members.IndexOf(SelectedMember);

        Recording(() =>
        {
            if (Group.Remove(SelectedMember!))
                SelectedMember = Group.Members[Math.Min(index, Group.Members.Count - 1)];
        });
    }

    /// <summary>
    /// Hace ese cambio en la lista de planos y lo deja en la historia del banco.
    /// </summary>
    /// <remarks>
    /// Con el paso abierto antes de tocar nada: cambiar la lista dice que el banco ha
    /// cambiado, y ese aviso tiraría la historia justo antes de anotar este paso. Si al final
    /// la lista quedó igual -añadir con el grupo lleno- no se anota nada.
    /// </remarks>
    private void Recording(Action change)
    {
        if (_undo is null)
        {
            change();

            return;
        }

        SpriteGroupMember[] before = [.. Group.Members];

        _undo.Begin();

        change();

        SpriteGroupMember[] after = [.. Group.Members];

        if (before.SequenceEqual(after))
        {
            _undo.Cancel();

            return;
        }

        _undo.Push(new MembersChanged(Group, before, after));
    }

    /// <summary>Si el plano elegido ya no está en la lista, se elige otro.</summary>
    private void KeepSelection()
    {
        if (SelectedMember is { } chosen && Group.Members.Contains(chosen))
            return;

        SelectedMember = Group.Members.FirstOrDefault();
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

}
