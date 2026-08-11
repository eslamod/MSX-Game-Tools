using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Elegir qué imagen de referencia se ve de fondo, y qué celda de ella.
/// </summary>
/// <remarks>
/// <para>
/// La misma pieza sirve para el grupo y para el patrón: sólo cambia dónde se guarda el
/// resultado, que llega como un par de funciones.
/// </para>
/// <para>
/// Son dos controles y no un desplegable de celdas porque una hoja de sprites puede
/// tener cientos, y «Background 137» no le dice nada a nadie: una celda se reconoce
/// mirándola. El desplegable elige la imagen y el botón abre la retícula. Que el botón
/// exista, y no sólo el desplegable, es lo que permite cambiar de celda sin cambiar de
/// imagen: volver a elegir en un ComboBox lo que ya estaba elegido no dispara nada.
/// </para>
/// </remarks>
public sealed partial class BackgroundSelectionViewModel : ObservableObject
{
    private readonly ReferenceImageLibrary _library;
    private readonly IDialogService _dialogs;
    private readonly Func<BackgroundRef> _read;
    private readonly Action<BackgroundRef> _write;

    public BackgroundSelectionViewModel(
        ReferenceImageLibrary library,
        IDialogService dialogs,
        Func<BackgroundRef> read,
        Action<BackgroundRef> write)
    {
        _library = library;
        _dialogs = dialogs;
        _read = read;
        _write = write;

        _library.Tiles.CollectionChanged += (_, _) => Refresh();
    }

    /// <summary>El fondo elegido ha cambiado y hay que repintar.</summary>
    public event Action? Changed;

    public ReferenceImageLibrary Library => _library;

    /// <summary>La celda elegida, o <c>null</c> si no hay fondo o su imagen ya no está.</summary>
    public ReferenceTile? Tile => _library.Find(_read());

    /// <summary>
    /// Enseñar u ocultar el fondo sin perder qué celda era.
    /// </summary>
    /// <remarks>
    /// Existe porque quitar el fondo para ver el dibujo limpio y volver a ponerlo te
    /// obligaba a acordarte de la celda. No se guarda en el banco: es una preferencia
    /// del momento, y al abrir un banco lo que se quiere es ver el fondo que trae.
    /// </remarks>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>La celda que hay que dibujar, que es ninguna si está oculta.</summary>
    public ReferenceTile? VisibleTile => IsVisible ? Tile : null;

    /// <summary>Con fondo elegido tiene sentido poder ocultarlo; sin él, no.</summary>
    public bool HasTile => Tile is not null;

    /// <summary>
    /// La imagen elegida en el desplegable.
    /// </summary>
    /// <remarks>
    /// Al elegir una hoja se aplica su primera celda y se abre la retícula para afinar.
    /// Se aplica antes de preguntar a propósito: así cancelar no deja el desplegable
    /// enseñando una imagen que no es la que está puesta de fondo.
    /// </remarks>
    public ReferenceImage? Image
    {
        get => Tile?.Source;
        set
        {
            if (value is null)
            {
                Apply(BackgroundRef.None);
                return;
            }

            if (!ReferenceEquals(value, Image))
                Apply(new BackgroundRef(value.Path, 0));

            if (value.IsSheet)
                PickCellCommand.Execute(null);
        }
    }

    /// <summary>Qué pone el botón que abre la retícula.</summary>
    public string CellLabel => Tile switch
    {
        null => "…",
        { Source.IsSheet: false } => "completa",
        { } tile => $"{tile.Column},{tile.Row}",
    };

    /// <summary>Sin hoja no hay nada que elegir: esa imagen ya es el fondo.</summary>
    public bool CanPickCell => Image?.IsSheet == true;

    /// <summary>Aplica un fondo y avisa de que hay que repintar.</summary>
    public void Apply(BackgroundRef reference)
    {
        if (_read() == reference)
            return;

        _write(reference);
        Refresh();
        Changed?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanPickCell))]
    private async Task PickCellAsync()
    {
        if (Image is not { IsSheet: true } image)
            return;

        int? cell = await _dialogs.PickReferenceCellAsync(image, Tile?.Index ?? 0);

        // El diálogo devuelve un índice negativo cuando se pide quitar el fondo.
        if (cell is { } chosen)
            Apply(chosen < 0 ? BackgroundRef.None : new BackgroundRef(image.Path, chosen));
    }

    partial void OnIsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(VisibleTile));
        Changed?.Invoke();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Tile));
        OnPropertyChanged(nameof(VisibleTile));
        OnPropertyChanged(nameof(HasTile));
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(CellLabel));
        OnPropertyChanged(nameof(CanPickCell));
        PickCellCommand.NotifyCanExecuteChanged();
    }
}
