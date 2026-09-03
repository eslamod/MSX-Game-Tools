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
/// <para>
/// El otro mando es el <see cref="Match"/>: cuánto se tienen que parecer dos pantallas para
/// darlas por seguidas. Alto de más corta lo que iba junto; bajo de más pega zonas que en el
/// juego no se tocan, y entonces sale un mapa grande y mal pegado.
/// </para>
/// <para>
/// Por eso el informe da además <see cref="MapStitcher.Noise"/>: cuántas de las celdas que dos
/// pantallas se reparten no dicen lo mismo. Es lo que distingue un mapa bueno de uno grande, y
/// el tamaño no lo dice —bajando el listón salen menos mapas y más largos, y peores—. Se elige
/// mirando ese número, no el número de mapas.
/// </para>
/// <para>
/// Y se traen <see cref="MostMaps"/> como mucho, los mayores. Una partida por un juego de
/// salas puede dar cientos de trozos de dos pantallas, y abrirlos todos deja la aplicación
/// inservible: al cerrar sale la lista de lo que hay que guardar con trescientas líneas y los
/// botones se van de la pantalla. Los grandes son además los que interesan; los de dos
/// pantallas son sitios por los que se pasó.
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

    /// <summary>Y con qué listón, que también cambia lo que sale.</summary>
    private int _stitchedAt = -1;

    private MapCapture.Stitched _stitch = new([], 0);

    /// <summary>En cuántos pares de pantallas se movió la cámara.</summary>
    private readonly int _moves;

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

    /// <summary>Lo que se tienen que parecer dos pantallas para darlas por seguidas, en tanto por ciento.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private int _match = (int)(MapStitcher.LeastMatch * 100);

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

        // Una vez y no en cada informe: la captura no cambia, y recorrer sus pares buscando
        // hacia donde fue la camara cuesta lo mismo que proponer la zona.
        _moves = MapCapture.Moves(capture);

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
    /// <summary>Cuántos mapas se traen como mucho.</summary>
    /// <remarks>Veinte caben en la lista de guardar sin echar los botones fuera.</remarks>
    public const int MostMaps = 20;

    public string Report
    {
        get
        {
            if (!Region.FitsIn(_capture))
                return Localizer.Instance["ImportCaptureBadRegion"];

            // Que ninguna pantalla se mueva sobre la anterior va por delante de todo: sin
            // recorrido no hay mapa que recomponer, y lo que salga son pantallas sueltas
            // cosidas consigo mismas. Sin decirlo, se ve «sale un mapa de 32x24» y no hay
            // manera de saber por que.
            string still = _moves == 0 ? Localizer.Instance["CaptureStillBody"] : string.Empty;

            if (Maps.Count == 0)
            {
                return _moves == 0
                    ? still
                    : Localizer.Instance["CaptureEmptyBody"];
            }

            TileMap biggest = Kept[0];

            string many = Maps.Count switch
            {
                1 => Localizer.Instance["ImportCaptureOneMap"],
                _ when Maps.Count > MostMaps =>
                    Localizer.Instance.Format("ImportCaptureTooMany", Maps.Count, MostMaps),
                _ => Localizer.Instance.Format("ImportCaptureManyMaps", Maps.Count),
            };

            return string.Join(
                Environment.NewLine,
                [.. new[]
                {
                    still,
                    many,
                    Localizer.Instance.Format("ImportCaptureBiggest", biggest.Width, biggest.Height),
                    Localizer.Instance.Format("ImportCaptureNoise", $"{_stitch.Noise:P1}"),
                }.Where(line => line.Length > 0)]);
        }
    }

    public bool CanAccept => Maps.Count > 0;

    /// <summary>Los que se van a abrir: los mayores, y no más de <see cref="MostMaps"/>.</summary>
    public IReadOnlyList<TileMap> Kept =>
        [.. Maps.OrderByDescending(map => map.Width * map.Height).Take(MostMaps)];

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
            if (_stitched != Region || _stitchedAt != Match)
            {
                _stitched = Region;
                _stitchedAt = Match;

                _stitch = Region.FitsIn(_capture)
                    ? MapCapture.Stitch(_capture, _name, Region, Match / 100.0)
                    : new MapCapture.Stitched([], 0);
            }

            return _stitch.Maps;
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
        foreach (TileMap map in Kept)
        {
            map.BackgroundColorIndex = _tileSet.ColorPalette.DefaultBackgroundIndex;

            _mainWindowVm.OpenMap(map, _tileSet);
        }

        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelImport() => _mainWindowVm.RightPanViewModel = null;
}
