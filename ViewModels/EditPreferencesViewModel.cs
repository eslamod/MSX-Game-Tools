using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>Una escala de la interfaz, con lo que se lee y el factor que aplica.</summary>
public sealed record ScaleChoice(string Label, double Value);

/// <summary>Una variante de la interfaz, con lo que se lee y lo que vale.</summary>
/// <remarks>
/// La etiqueta se resuelve al abrir el formulario, como el resto: se abre y se cierra en
/// un momento y no compensa enlazarla al idioma.
/// </remarks>
public sealed record VariantChoice(string Label, AppThemeVariant Value);

/// <summary>Un nivel de zoom, con lo que se lee y lo que vale por dentro.</summary>
/// <remarks>
/// Hacen falta las dos cosas porque no coinciden: el zoom de los lienzos se guarda como
/// posición (0, 1, 2) y el de las miniaturas como factor (1, 2, 3, 4). Fuera se enseñan
/// todos igual, «x1, x2, x3», que es lo único que significa algo para quien lo elige.
/// </remarks>
public sealed record ZoomChoice(string Label, int Value);

/// <summary>
/// Los ajustes del usuario: el idioma y con qué zoom arranca cada sitio.
/// </summary>
/// <remarks>
/// Con aceptar y cancelar, como los demás formularios. El idioma se aplica al aceptar y no
/// según se elige: media frase en un idioma y media en otro mientras se decide es peor que
/// esperar al botón.
/// </remarks>
public partial class EditPreferencesViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private LanguageChoice _language;

    [ObservableProperty]
    private ScaleChoice _scale;

    [ObservableProperty]
    private VariantChoice _variant;

    [ObservableProperty]
    private int _spriteCanvasZoom;

    [ObservableProperty]
    private int _spriteThumbnailZoom;

    [ObservableProperty]
    private int _tileCanvasZoom;

    [ObservableProperty]
    private int _tileThumbnailZoom;

    [ObservableProperty]
    private int _blockGridZoom;

    [ObservableProperty]
    private int _blockTileZoom;

    [ObservableProperty]
    private int _mapTileZoom;

    /// <summary>Cómo se llama la directiva de datos en el ensamblador que se use.</summary>
    [ObservableProperty]
    private string _asmData = Entities.AsmStyle.Dotted;

    public EditPreferencesViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;

        _language = Localizer.Languages.First(choice => choice.Code == Localizer.Instance.Language);

        Entities.EditorPreferences zoom = mainWindowVm.Preferences;

        _scale = Scales.FirstOrDefault(choice => choice.Value == zoom.InterfaceScale) ?? Scales[0];
        _variant = Variants.First(choice => choice.Value == zoom.ThemeVariant);
        _spriteCanvasZoom = zoom.SpriteCanvasZoom;
        _spriteThumbnailZoom = zoom.SpriteThumbnailZoom;
        _tileCanvasZoom = zoom.TileCanvasZoom;
        _tileThumbnailZoom = zoom.TileThumbnailZoom;
        _blockGridZoom = zoom.BlockGridZoom;
        _blockTileZoom = zoom.BlockTileZoom;
        _mapTileZoom = zoom.MapTileZoom;
        _asmData = zoom.AsmData;

        Header = Localizer.Instance["PreferencesTitle"];
        TagId = "preferences";
    }

    public IReadOnlyList<LanguageChoice> Languages => Localizer.Languages;

    /// <summary>
    /// Las dos grafías que se han probado, con qué ensamblador vale cada una.
    /// </summary>
    /// <remarks>
    /// El desplegable se puede escribir además de elegir: cuatro ensambladores no agotan los
    /// que hay, y quien use otro no debería quedarse fuera por no estar en la lista.
    /// </remarks>
    public IReadOnlyList<string> AsmDataChoices { get; } =
        [Entities.AsmStyle.Dotted, Entities.AsmStyle.Plain];

    /// <summary>
    /// Cuánto agrandar la interfaz por encima de lo que ya haga el sistema.
    /// </summary>
    /// <remarks>
    /// Sólo hacia arriba: esto existe para las pantallas densas donde todo sale pequeño,
    /// y encoger la interfaz no le hace falta a nadie.
    /// </remarks>
    public static IReadOnlyList<ScaleChoice> Scales { get; } =
        [new("100 %", 1), new("125 %", 1.25), new("150 %", 1.5), new("175 %", 1.75), new("200 %", 2)];

    /// <summary>
    /// Claro, oscuro, o lo que diga el sistema.
    /// </summary>
    /// <remarks>
    /// No es estática como las demás: las etiquetas están traducidas y una lista estática
    /// se quedaría con el idioma del primer formulario que se abriera.
    /// </remarks>
    public static IReadOnlyList<VariantChoice> Variants =>
    [
        new(Localizer.Instance["PreferencesThemeSystem"], AppThemeVariant.System),
        new(Localizer.Instance["PreferencesThemeLight"], AppThemeVariant.Light),
        new(Localizer.Instance["PreferencesThemeDark"], AppThemeVariant.Dark),
        new(Localizer.Instance["PreferencesThemeBlue"], AppThemeVariant.Blue),
        new(Localizer.Instance["PreferencesThemeOrange"], AppThemeVariant.Orange),
        new(Localizer.Instance["PreferencesThemeLightBlue"], AppThemeVariant.LightBlue),
        new(Localizer.Instance["PreferencesThemeLightOrange"], AppThemeVariant.LightOrange),
    ];

    /// <summary>Los lienzos de dibujo, que guardan la posición del botón y no el factor.</summary>
    public static IReadOnlyList<ZoomChoice> CanvasZooms { get; } =
        [new("x1", 0), new("x2", 1), new("x3", 2)];

    /// <summary>Tres pasos, que es lo que ofrecen sus botones.</summary>
    public static IReadOnlyList<ZoomChoice> ThreeZooms { get; } =
        [new("x1", 1), new("x2", 2), new("x3", 3)];

    /// <summary>Cuatro pasos.</summary>
    public static IReadOnlyList<ZoomChoice> FourZooms { get; } =
        [new("x1", 1), new("x2", 2), new("x3", 3), new("x4", 4)];

    /// <summary>
    /// Los del selector del editor de mapas, que no empieza en x1 y llega mas arriba.
    /// </summary>
    /// <remarks>
    /// Ahí un tile a x1 son ocho pixeles y no se reconoce; y para ver mucho mapa de golpe
    /// está el zoom del lienzo, que baja hasta un cuarto. Tienen que ser los mismos pasos
    /// que ofrecen sus botones: lo que se elija aquí se guarda tal cual.
    /// </remarks>
    public static IReadOnlyList<ZoomChoice> MapZooms { get; } =
        [new("x2", 2), new("x3", 3), new("x4", 4), new("x6", 6), new("x8", 8)];

    /// <summary>
    /// Lo que se elige aquí es con qué zoom se abre cada sitio, no el de lo que ya está
    /// abierto: una pestaña montada se queda con el suyo hasta que se vuelve a montar.
    /// </summary>
    [RelayCommand]
    private async Task AcceptPreferencesAsync()
    {
        Entities.EditorPreferences zoom = _mainWindowVm.Preferences;

        // La escala se aplica sola: la ventana está enlazada a ella. Y la variante también,
        // que la ventana principal la vigila y se la pasa a Avalonia.
        zoom.InterfaceScale = Scale.Value;
        zoom.ThemeVariant = Variant.Value;
        zoom.SpriteCanvasZoom = SpriteCanvasZoom;
        zoom.SpriteThumbnailZoom = SpriteThumbnailZoom;
        zoom.TileCanvasZoom = TileCanvasZoom;
        zoom.TileThumbnailZoom = TileThumbnailZoom;
        zoom.BlockGridZoom = BlockGridZoom;
        zoom.BlockTileZoom = BlockTileZoom;
        zoom.MapTileZoom = MapTileZoom;
        zoom.AsmData = AsmData;

        bool languageChanged = Localizer.Instance.Language != Language.Code;

        Localizer.Instance.Language = Language.Code;

        _mainWindowVm.SaveSettings();
        _mainWindowVm.RightPanViewModel = null;

        // Después de aplicarlo, así que el aviso sale ya en el idioma nuevo. Y sólo si de
        // verdad ha cambiado: avisar de algo que no ha pasado enseña a no leer los avisos.
        if (languageChanged)
        {
            await _mainWindowVm.Dialogs.ShowMessageAsync(
                Localizer.Instance["LanguageChangedTitle"],
                Localizer.Instance["LanguageChangedMessage"]);
        }
    }

    [RelayCommand]
    private void CancelPreferences() => _mainWindowVm.RightPanViewModel = null;
}
