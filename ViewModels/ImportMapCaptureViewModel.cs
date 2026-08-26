using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Traer los mapas de una captura de pantallas de openMSX, diciendo qué zona es mapa.
/// </summary>
/// <remarks>
/// <para>
/// Lo único que hay que decidir es qué parte de la pantalla es terreno, porque el marcador
/// —la puntuación, las vidas, el nivel— no scrollea con la cámara y metido en el cosido lo
/// estropea dos veces: baja el porcentaje de encaje, y se vuelve a estampar en cada posición
/// nueva dejando un reguero por todo el mapa.
/// </para>
/// <para>
/// La zona viene propuesta: se mira qué filas y columnas cambian de una captura a la
/// siguiente, y las que no cambian nunca son el marcador. Es una propuesta y no una certeza
/// —un trozo de cielo raso que nunca cambia también se daría por marcador—, así que se enseña
/// y se puede corregir.
/// </para>
/// <para>
/// Debajo se ve lo que va a salir con la zona puesta: cuántos mapas y lo que mide el mayor.
/// Así se prueba a recortar sin tener que aceptar y deshacer.
/// </para>
/// </remarks>
public partial class ImportMapCaptureViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly TileSetEditorViewModel _tileSet;
    private readonly MapCapture.Capture _capture;
    private readonly string _name;

    /// <summary>La zona con la que se coció <see cref="_maps"/>, para no coserla otra vez.</summary>
    private MapCapture.Region? _stitched;

    private IReadOnlyList<TileMap> _maps = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private int _left;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private int _top;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private int _columns;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private int _rows;

    public ImportMapCaptureViewModel(
        MainWindowViewModel mainWindowVm,
        TileSetEditorViewModel tileSet,
        string path,
        MapCapture.Capture capture)
    {
        _mainWindowVm = mainWindowVm;
        _tileSet = tileSet;
        _capture = capture;
        _name = Path.GetFileNameWithoutExtension(path);

        Header = Localizer.Instance["ImportCaptureTitle"];
        FileName = Path.GetFileName(path);

        Take(MapCapture.Suggest(capture));
    }

    /// <summary>La captura que se está leyendo, que el formulario no la vuelve a pedir.</summary>
    public string FileName { get; }

    public int Screens => _capture.Screens.Count;

    public int ScreenColumns => _capture.Columns;

    public int ScreenRows => _capture.Rows;

    /// <summary>Lo que va a salir con la zona puesta, antes de aceptar.</summary>
    public string Report
    {
        get
        {
            if (!Region.FitsIn(_capture))
                return Localizer.Instance["ImportCaptureBadRegion"];

            if (Maps.Count == 0)
                return Localizer.Instance["CaptureEmptyBody"];

            TileMap biggest = Maps.MaxBy(map => map.Width * map.Height)!;

            return string.Join(
                Environment.NewLine,
                Maps.Count == 1
                    ? Localizer.Instance["ImportCaptureOneMap"]
                    : Localizer.Instance.Format("ImportCaptureManyMaps", Maps.Count),
                Localizer.Instance.Format("ImportCaptureBiggest", biggest.Width, biggest.Height));
        }
    }

    public bool CanAccept => Maps.Count > 0;

    /// <summary>
    /// Los mapas que salen con la zona puesta, cosidos una sola vez.
    /// </summary>
    /// <remarks>
    /// El informe, el botón de aceptar y el propio aceptar quieren lo mismo, y una captura de
    /// una partida larga son cientos de pantallas: se cose al cambiar la zona y no cada vez
    /// que alguien pregunte.
    /// </remarks>
    private IReadOnlyList<TileMap> Maps
    {
        get
        {
            if (_stitched != Region)
            {
                _stitched = Region;

                _maps = Region.FitsIn(_capture)
                    ? MapCapture.Stitch(_capture, _name, Region)
                    : [];
            }

            return _maps;
        }
    }

    public MapCapture.Region Region => new(Left, Top, Columns, Rows);

    /// <summary>Vuelve a proponer la zona, por si se toqueteó y se quiere lo de antes.</summary>
    [RelayCommand]
    private void SuggestRegion() => Take(MapCapture.Suggest(_capture));

    /// <summary>La pantalla entera, para los juegos que no llevan marcador.</summary>
    [RelayCommand]
    private void WholeScreen() => Take(MapCapture.Region.Whole(_capture));

    private void Take(MapCapture.Region region)
    {
        Left = region.Left;
        Top = region.Top;
        Columns = region.Columns;
        Rows = region.Rows;
    }

    /// <remarks>
    /// Se abren todos y no sólo el primero: cuál interesa se ve abriéndolos, y el que se
    /// quedara sin abrir habría que volver a importarlo sin saber siquiera que estaba ahí.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void AcceptImport()
    {
        foreach (TileMap map in Maps)
        {
            map.BackgroundColorIndex = _tileSet.ColorPalette.DefaultBackgroundIndex;

            _mainWindowVm.OpenMap(map, _tileSet);
        }

        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelImport() => _mainWindowVm.RightPanViewModel = null;
}
