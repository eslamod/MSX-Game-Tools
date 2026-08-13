using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Los atributos de un documento: de momento sólo el nombre.
/// </summary>
/// <remarks>
/// <para>
/// Un panel para los tres tipos y no uno por tipo: lo que tienen en común es lo que hay
/// hoy, y el día que un mapa quiera enseñar aquí su tamaño y un juego su color de borde,
/// se añaden como secciones que aparecen según lo que sea el documento.
/// </para>
/// <para>
/// Con aceptar y cancelar, y no escribiendo según se teclea: renombrar toca la pestaña, el
/// árbol y los mapas que usan el juego, y ver todo eso bailar letra a letra mientras se
/// escribe es desconcertante.
/// </para>
/// </remarks>
public partial class EditPropertiesViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public EditPropertiesViewModel(MainWindowViewModel mainWindowVm, PanelBaseViewModel document)
    {
        _mainWindowVm = mainWindowVm;

        Document = document;
        _name = document.DocumentName;

        Header = $"{Localizer.Instance["TreeProperties"]}: {document.DocumentName}";
        TagId = "properties";
    }

    /// <summary>El documento cuyos atributos se están tocando.</summary>
    public PanelBaseViewModel Document { get; }

    /// <summary>Qué es, para que se vea de qué se están viendo las propiedades.</summary>
    public string Kind => Document.DocumentKind;

    /// <summary>Dónde está guardado, o que todavía no lo está.</summary>
    public string Where => Document.FilePath ?? "Sin guardar en ningún fichero todavía.";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Aviso de lo que arrastra el cambio, cuando arrastra algo.
    /// </summary>
    /// <remarks>
    /// Renombrar un juego de tiles ya no rompe nada —los mapas lo señalan por identidad—
    /// pero sí les cambia el fichero, y quedarse con tres mapas sin guardar sin saber por
    /// qué es peor que un renglón de aviso.
    /// </remarks>
    public string Affected
    {
        get
        {
            if (Document is not TileSetEditorViewModel tiles)
                return string.Empty;

            int maps = _mainWindowVm.MapsOf(tiles).Count;

            return maps switch
            {
                0 => string.Empty,
                1 => "Hay un mapa que se dibuja con él y también quedará sin guardar.",
                _ => $"Hay {maps} mapas que se dibujan con él y también quedarán sin guardar.",
            };
        }
    }

    public bool HasAffected => Affected.Length > 0;

    [RelayCommand]
    private void AcceptProperties()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = $"El nombre del {Kind} no puede estar vacío.";

            return;
        }

        ErrorMessage = null;

        _mainWindowVm.Rename(Document, Name);
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelProperties() => _mainWindowVm.RightPanViewModel = null;
}
