using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>Un color de la hoja, para poder elegir cuál hace de transparente.</summary>
public sealed record SheetColor(Color Color, int Times)
{
    public IBrush Brush { get; } = new SolidColorBrush(Color);

    public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
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
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private int _cellSize = 16;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private int _maxPlanes = 3;

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

    public int Columns => Size.Width / Math.Max(1, CellSize);

    public int Rows => Size.Height / Math.Max(1, CellSize);

    /// <summary>El análisis de lo que hay elegido ahora mismo.</summary>
    public SheetAnalysis Analysis => SpriteSheetAnalysis.Analyse(
        _pixels, Size, CellSize, Transparent?.Color, Selection, MaxPlanes);

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

            string cost = Localizer.Instance.Format(
                "ImportSheetCost",
                analysis.Colors.Count,
                analysis.Planes,
                analysis.Cells.Count(cell => cell.Planes > 0),
                analysis.Patterns,
                SpriteBank.MaxSprites);

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
        SheetImport import = SpriteSheetImporter.Import(
            _pixels, Size, CellSize, Transparent?.Color, Selection, MaxPlanes, _name);

        if (!import.Ok)
            return;

        _mainWindowVm.OpenSpriteBank(import.Bank!, _mainWindowVm.Palettes.Adopt(import.Palette!));
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

    /// <summary>Los colores de la hoja con cuántas veces sale cada uno, del más usado al menos.</summary>
    private static IEnumerable<SheetColor> Counted(int[] pixels)
    {
        var times = new Dictionary<uint, int>();

        foreach (int pixel in pixels)
        {
            if ((uint)pixel >> 24 == 0)
                continue;

            uint rgb = (uint)pixel & 0x00FFFFFF;

            times[rgb] = times.GetValueOrDefault(rgb) + 1;
        }

        return times
            .OrderByDescending(pair => pair.Value)
            .Select(pair => new SheetColor(
                Color.FromRgb(
                    (byte)((pair.Key >> 16) & 0xFF), (byte)((pair.Key >> 8) & 0xFF), (byte)(pair.Key & 0xFF)),
                pair.Value));
    }
}
