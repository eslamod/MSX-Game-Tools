using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un color de la hoja, para poder elegir cuál hace de transparente.
/// </summary>
/// <remarks>
/// El alfa de la hoja es uno más de la lista, con su cuenta de pixeles. Antes no salía —sólo se
/// contaban los colores opacos— y con una hoja con alfa de verdad no había manera de elegirlo:
/// se cogía el color opaco más usado, que en un dibujo grande es parte del dibujo, y esos
/// sprites entraban en blanco.
/// </remarks>
public sealed record SheetColor(Color Color, int Times)
{
    public IBrush Brush { get; } = new SolidColorBrush(Color);

    /// <summary>Si es el alfa de la hoja y no un color suyo.</summary>
    public bool IsAlpha => Color.A == 0;

    public string Hex => IsAlpha
        ? Localizer.Instance["ImportSheetAlpha"]
        : $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
}

/// <summary>
/// Formulario para traer un rectángulo de una hoja de sprites.
/// </summary>
/// <remarks>
/// <para>
/// Panel y no una tira de diálogos encadenados, y por una razón concreta: lo que hace esto
/// usable es que el informe se rehaga mientras mueves el rectángulo. Arrastras y ves al momento
/// si entra en los 64 huecos. Con diálogos en cadena habría que llegar al final para descubrir
/// que no cabe, y volver a empezar.
/// </para>
/// <para>
/// El análisis se rehace con cada cambio. Es barato —recorre el rectángulo elegido, no la hoja—
/// y es lo único que hace que los números de abajo signifiquen algo.
/// </para>
/// </remarks>
public partial class ImportSpriteSheetViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly int[] _pixels;
    private readonly string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(FigureLabel))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private int _cellSize = 16;

    /// <summary>
    /// Sprites de ancho y de alto que mide una figura de la hoja.
    /// </summary>
    /// <remarks>
    /// Un personaje no tiene por qué caber en un sprite. Con dos de alto, la cabeza y el
    /// cuerpo entran como dos patrones en un mismo grupo, cada uno con su desplazamiento, en
    /// vez de quedar como dos grupos sueltos que hay que volver a juntar a mano.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(FigureLabel))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private int _across = 1;

    /// <inheritdoc cref="Across"/>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(FigureLabel))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private int _down = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private int _maxPlanes = 3;

    /// <summary>
    /// De qué máquina va a ser el banco que salga.
    /// </summary>
    /// <remarks>
    /// Cambia la aritmética entera, no sólo una etiqueta. En MSX2 el bit CC mezcla con un OR
    /// los colores de los sprites solapados y dos planos pueden enseñar tres colores; en MSX1
    /// no existe ese bit, un sprite es de un color y hacen falta tantos como colores tenga la
    /// celda. El informe de abajo cuenta una cosa u otra según lo que haya puesto aquí.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(CanReusePalette))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private SpriteBank.SpriteType _spriteType = SpriteBank.SpriteType.MSX2;

    /// <summary>
    /// Si los colores se encajan en la paleta que ya hay en vez de traer una nueva.
    /// </summary>
    /// <remarks>
    /// Sólo en MSX1. En MSX2 el índice de cada color no se puede elegir: lo fija el reparto de
    /// planos, porque es lo que hace que el OR del bit CC reconstruya el color de cada pixel.
    /// Encajarlos en otros índices rompería esa cuenta.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private bool _reusePalette;

    /// <summary>
    /// Si se trae el color o sólo el dibujo.
    /// </summary>
    /// <remarks>
    /// Los dos modos comparten la hoja, la retícula, el rectángulo y el transparente, que es
    /// casi todo el formulario. Lo que cambia es qué sale por el otro lado, y por eso es un
    /// interruptor aquí y no otra entrada de menú.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(IsCombined))]
    [NotifyPropertyChangedFor(nameof(Packs))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private bool _onlyPatterns;

    /// <summary>
    /// Si se aprovecha el banco: fuera las celdas vacías y un solo patrón para las repetidas.
    /// </summary>
    /// <remarks>
    /// Apagado por defecto, y no por comodidad: sin tocarlo, el patrón número N es la celda
    /// número N de lo que se eligió, y un juego que direccione el patrón por la posición de la
    /// celda cuenta con eso. Encendido se hace lo que ya hace el modo de color, y en una hoja
    /// de animación eso es media tabla.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(Packs))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private bool _pack;

    /// <summary>
    /// Si el banco se va a aprovechar, se haya pedido o no.
    /// </summary>
    /// <remarks>
    /// En el modo de color siempre, y no porque se elija: una celda sin nada que pintar no tiene
    /// ningún plano que colocar, y los patrones repetidos se reconocen antes de darles hueco.
    /// Por eso la casilla se enseña marcada y apagada en vez de esconderse: escondida no se
    /// aprende que la opción existe ni que ahí ya está puesta.
    /// </remarks>
    public bool Packs
    {
        get => !OnlyPatterns || Pack;
        set => Pack = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private SheetColor? _transparent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private SheetSelection _selection = new(0, 0, 1, 1);

    public ImportSpriteSheetViewModel(
        MainWindowViewModel mainWindowVm, string path, int[] pixels, PixelSize size, IImage source)
    {
        _mainWindowVm = mainWindowVm;
        _pixels = pixels;
        _name = System.IO.Path.GetFileNameWithoutExtension(path);

        Size = size;
        Source = source;

        Header = Localizer.Instance["ImportSheetTitle"];
        TagId = "import:spritesheet";

        // El más usado de la hoja. Casi siempre es el fondo, así que acierta solo en la mayoría
        // de las hojas y quien tenga alfa de verdad no tiene que tocar nada.
        SheetColors = [.. Counted(pixels)];
        _transparent = SheetColors.FirstOrDefault();
    }

    /// <summary>La hoja entera, para enseñarla con la retícula encima.</summary>
    public IImage Source { get; }

    public PixelSize Size { get; }

    /// <summary>Los colores de la hoja, del más usado al menos.</summary>
    public IReadOnlyList<SheetColor> SheetColors { get; }

    /// <summary>
    /// Los lados de celda que sabe leer y que además caben en esta hoja.
    /// </summary>
    /// <remarks>
    /// Una hoja de ocho pixeles de alto no se puede trocear en celdas de dieciséis, y ofrecerlo
    /// sólo sirve para que el informe conteste que el rectángulo se sale. Si no cabe ninguno se
    /// deja el más pequeño, para que el desplegable no salga vacío y el aviso explique por qué.
    /// </remarks>
    public IReadOnlyList<int> CellSizes
    {
        get
        {
            int[] fit = [.. SpriteSheetAnalysis.CellSizes.Where(
                side => Size.Width >= side && Size.Height >= side)];

            return fit.Length > 0 ? fit : [SpriteSheetAnalysis.CellSizes[0]];
        }
    }

    /// <summary>Los topes de planos que se pueden pedir.</summary>
    public IReadOnlyList<int> PlaneChoices { get; } = [1, 2, 3, 4];

    /// <summary>Sprites que puede medir una figura, a lo ancho o a lo alto.</summary>
    /// <remarks>
    /// Hasta cuatro: son los desplazamientos que aguanta un miembro de grupo, y de todas
    /// formas una figura mayor no cabe en los sprites por línea de barrido del VDP.
    /// </remarks>
    public IReadOnlyList<int> SpriteChoices { get; } = [1, 2, 3, 4];

    /// <summary>Lo que mide una figura en pixeles, que es como se mira una hoja.</summary>
    public string FigureLabel =>
        Localizer.Instance.Format("ImportSheetFigureSize", Across * CellSize, Down * CellSize);

    /// <summary>De qué máquina va a ser el banco que salga.</summary>
    public IReadOnlyList<SpriteBank.SpriteType> TypeChoices { get; } =
        [SpriteBank.SpriteType.MSX, SpriteBank.SpriteType.MSX2];

    public int Columns => Size.Width / Math.Max(1, CellSize);

    public int Rows => Size.Height / Math.Max(1, CellSize);

    /// <summary>Si se enseñan los controles que sólo tienen sentido trayendo el color.</summary>
    public bool IsCombined => !OnlyPatterns;

    /// <summary>Si se puede encajar en la paleta que hay, que es cosa de la máquina.</summary>
    public bool CanReusePalette => SpriteType == SpriteBank.SpriteType.MSX;

    /// <summary>La paleta en la que encajar, o nada si se va a traer una nueva.</summary>
    private ColorPalette? Reuse =>
        ReusePalette && CanReusePalette ? _mainWindowVm.Palettes.ActivePalette : null;

    /// <summary>El análisis de lo que hay elegido ahora mismo.</summary>
    public SheetAnalysis Analysis => OnlyPatterns
        ? SpriteSheetAnalysis.AnalysePatterns(
            _pixels, Size, CellSize, Transparent?.Color, Selection, Pack)
        : SpriteSheetAnalysis.Analyse(
            _pixels, Size, CellSize, Transparent?.Color, Selection, MaxPlanes, SpriteType,
            Across, Down, Reuse);

    /// <summary>
    /// Lo que se lee debajo: o lo que va a costar, o por qué no se puede.
    /// </summary>
    /// <remarks>
    /// Los patrones con el tope al lado a propósito. El número solo no dice nada; «58 / 64» sí,
    /// y es lo que de verdad decide si esta selección vale o hay que recortarla.
    /// </remarks>
    public string Report
    {
        get
        {
            SheetAnalysis analysis = Analysis;

            if (!analysis.Ok)
                return string.Join("\n", analysis.Problems);

            // En modo patrones no hay colores ni planos que contar: sobra media frase, y
            // enseñar «0 colores · 1 planos» sería peor que no decir nada.
            string cost = OnlyPatterns
                ? Localizer.Instance.Format(
                    "ImportSheetPatternsCost",
                    analysis.Cells.Count(cell => cell.Planes > 0),
                    analysis.Patterns,
                    analysis.BankSize)
                : Localizer.Instance.Format(
                    "ImportSheetCost",
                    analysis.Colors.Count,
                    analysis.Planes,
                    analysis.Cells.Count(cell => cell.Planes > 0),
                    analysis.Patterns,
                    analysis.BankSize);

            return analysis.Fits
                ? cost
                : cost + "\n" + Localizer.Instance["ImportSheetTooMany"];
        }
    }

    public bool CanAccept => Analysis is { Ok: true, Fits: true };

    /// <summary>Deja el rectángulo elegido en la hoja, recortado a lo que hay.</summary>
    public void Select(int left, int top, int columns, int rows)
    {
        int maxColumns = Math.Max(1, Columns);
        int maxRows = Math.Max(1, Rows);

        left = Math.Clamp(left, 0, maxColumns - 1);
        top = Math.Clamp(top, 0, maxRows - 1);

        Selection = new SheetSelection(
            left,
            top,
            Math.Clamp(columns, 1, maxColumns - left),
            Math.Clamp(rows, 1, maxRows - top));
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        SheetImport import = OnlyPatterns
            ? SpriteSheetImporter.ImportPatterns(
                _pixels, Size, CellSize, Transparent?.Color, Selection, _name, Pack, SpriteType)
            : SpriteSheetImporter.Import(
                _pixels, Size, CellSize, Transparent?.Color, Selection, MaxPlanes, _name, SpriteType,
                Across, Down, Reuse);

        if (!import.Ok)
            return;

        // Sin paleta en modo patrones: allí el color no sale de la hoja, así que se queda la que
        // esté puesta en vez de inventarse una con los colores de una imagen que no se ha usado.
        _mainWindowVm.OpenSpriteBank(
            import.Bank!,
            import.Palette is { } made ? _mainWindowVm.Palettes.Adopt(made) : null);

        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void Cancel() => _mainWindowVm.RightPanViewModel = null;

    /// <summary>Cambiar el lado de celda deja el rectángulo dentro de la nueva retícula.</summary>
    partial void OnCellSizeChanged(int value)
    {
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(Rows));

        Select(Selection.Left, Selection.Top, Selection.Columns, Selection.Rows);
    }

    /// <summary>
    /// Los colores de la hoja con cuántas veces sale cada uno, del más usado al menos.
    /// </summary>
    /// <remarks>
    /// El alfa va el primero cuando lo hay, y no por cuántas veces salga: una hoja con alfa lo
    /// trae como fondo, y ése es el que se quiere. Poniéndolo por cuenta, en una hoja con poco
    /// hueco alrededor de las figuras acabaría el tercero y elegido saldría un color del dibujo.
    /// </remarks>
    private static IEnumerable<SheetColor> Counted(int[] pixels)
    {
        var times = new Dictionary<uint, int>();
        int clear = 0;

        foreach (int pixel in pixels)
        {
            if ((uint)pixel >> 24 == 0)
            {
                clear++;

                continue;
            }

            uint rgb = (uint)pixel & 0x00FFFFFF;

            times[rgb] = times.GetValueOrDefault(rgb) + 1;
        }

        if (clear > 0)
            yield return new SheetColor(Colors.Transparent, clear);

        IEnumerable<SheetColor> opaque = times
            .OrderByDescending(pair => pair.Value)
            .Select(pair => new SheetColor(
                Color.FromRgb(
                    (byte)((pair.Key >> 16) & 0xFF), (byte)((pair.Key >> 8) & 0xFF), (byte)(pair.Key & 0xFF)),
                pair.Value));

        foreach (SheetColor color in opaque)
            yield return color;
    }
}
