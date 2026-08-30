using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Traer tiles y sprites de un volcado de VRAM de openMSX.
/// </summary>
/// <remarks>
/// <para>
/// El formulario enseña dónde va a buscar cada tabla y deja cambiarlo, porque no todas las
/// ROMs usan las direcciones de siempre. Pero teclearlas es la manera mala: cargando además el
/// volcado de registros del VDP, las rellena él y no hay nada que acertar.
/// </para>
/// <para>
/// Los registros no importan por su cuenta: <b>rellenan estos mismos campos</b>. Así se ve lo
/// que ha detectado antes de aceptar y se puede corregir si esa ROM hace algo raro.
/// </para>
/// <para>
/// Las direcciones de patrones y colores no son libres. En GRAPHIC 2 y 3 sólo pueden empezar
/// en <c>0000H</c> o <c>2000H</c>, porque los bits bajos de R#4 y R#3 son una máscara sobre
/// los tercios y no parte de la dirección. Por eso son desplegables y no cajas de texto: una
/// dirección imposible no se puede ni escribir.
/// </para>
/// </remarks>
public partial class ImportVramViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly byte[] _vram;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    [NotifyPropertyChangedFor(nameof(TableChoices))]
    private VdpRegisters.ScreenMode _mode = VdpRegisters.ScreenMode.Graphic2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    private int _patterns;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    private int _colors = 0x2000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    private int _spritePatterns = 0x3800;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Report))]
    private bool _bigSprites = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private bool _wantsTiles = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(AcceptImportCommand))]
    private bool _wantsSprites = true;

    /// <summary>De dónde salieron las direcciones, para que se vea si se acertaron o se pusieron.</summary>
    [ObservableProperty]
    private string _detected = string.Empty;

    public ImportVramViewModel(MainWindowViewModel mainWindowVm, string path, byte[] vram)
    {
        _mainWindowVm = mainWindowVm;
        _vram = vram;

        Header = Localizer.Instance["ImportVramTitle"];
        FileName = Path.GetFileName(path);
        Detected = Localizer.Instance["ImportVramNoRegisters"];
    }

    /// <summary>El volcado que se está leyendo, que el formulario no lo vuelve a pedir.</summary>
    public string FileName { get; }

    public int Size => _vram.Length;

    /// <summary>Los modos que sabe leer el importador.</summary>
    public IReadOnlyList<VdpRegisters.ScreenMode> Modes { get; } =
    [
        VdpRegisters.ScreenMode.Graphic1,
        VdpRegisters.ScreenMode.Graphic2,
        VdpRegisters.ScreenMode.Graphic3,
    ];

    /// <inheritdoc cref="ImportVramViewModel"/>
    public IReadOnlyList<int> TableChoices =>
        Every(Mode == VdpRegisters.ScreenMode.Graphic1 ? 0x800 : 0x2000);

    /// <summary>Los sitios donde puede empezar la tabla de patrones de sprite: cada 2 KB.</summary>
    public IReadOnlyList<int> SpriteChoices => Every(0x800);

    /// <summary>
    /// Las direcciones legales que caben en el volcado, de <paramref name="step"/> en
    /// <paramref name="step"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hasta donde llegue el volcado y no hasta los 16 KB: en un V9938 la VRAM son 128 KB y
    /// las tablas se van muy arriba. Space Manbow tiene los patrones en <c>18000H</c>, y con la
    /// lista cortada en <c>2000H</c> el desplegable salía vacío y no había manera de decirlo.
    /// </para>
    /// <para>
    /// Un volcado de 16 KB sigue dando lo de siempre —dos sitios para las tablas y ocho para
    /// los sprites—, que es lo que puede tener un MSX1.
    /// </para>
    /// </remarks>
    private IReadOnlyList<int> Every(int step) =>
        [.. Enumerable
            .Range(0, Math.Max(1, _vram.Length / step))
            .Select(at => at * step)];

    /// <summary>Lo que va a salir, antes de aceptar.</summary>
    public string Report
    {
        get
        {
            VramImporter.VramImport preview = VramImporter.Read(_vram, Layout);

            string tiles = preview.TileSets.Count switch
            {
                0 => Localizer.Instance["ImportVramNoTileSets"],
                1 => Localizer.Instance["ImportVramOneTileSet"],
                _ => Localizer.Instance.Format("ImportVramManyTileSets", preview.TileSets.Count),
            };

            return string.Join(
                Environment.NewLine,
                [tiles, .. preview.Problems]);
        }
    }

    public bool CanAccept => WantsTiles || WantsSprites;

    private VramImporter.VramLayout Layout => new()
    {
        Mode = Mode,
        BigSprites = BigSprites,
        Patterns = Patterns,
        Colors = Colors,
        SpritePatterns = SpritePatterns,
    };

    /// <summary>
    /// Rellena los campos con lo que digan los registros.
    /// </summary>
    /// <remarks>
    /// Rellenar y no importar: lo detectado se ve antes de aceptar, y si esa ROM hace algo que
    /// el importador no entiende, se corrige a mano sin tener que adivinar de dónde salió.
    /// </remarks>
    public void Apply(VdpRegisters.Layout registers, string fileName)
    {
        Mode = registers.Mode == VdpRegisters.ScreenMode.Other
            ? VdpRegisters.ScreenMode.Graphic2
            : registers.Mode;

        BigSprites = registers.BigSprites;
        Patterns = registers.Patterns;
        Colors = registers.Colors;
        SpritePatterns = registers.SpritePatterns;

        Detected = registers.Mode == VdpRegisters.ScreenMode.Other
            ? Localizer.Instance.Format("ImportVramOtherMode", fileName)
            : Localizer.Instance.Format("ImportVramFromRegisters", fileName);
    }

    [RelayCommand]
    private async Task LoadRegistersAsync()
    {
        string? path = await _mainWindowVm.Dialogs.PickFileToOpenAsync(
            Localizer.Instance["PickVdpRegisters"], PickerFileKind.Any);

        if (path is null)
            return;

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path);

            if (VdpRegisters.Read(bytes) is not { } registers)
            {
                Detected = Localizer.Instance.Format("ImportVramBadRegisters", VdpRegisters.Count);
                return;
            }

            Apply(registers, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await _mainWindowVm.Dialogs.ShowMessageAsync(
                Localizer.Instance["ErrorOpenFile"], exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void AcceptImport()
    {
        VramImporter.VramImport import = VramImporter.Read(_vram, Layout);

        if (WantsTiles)
        {
            foreach (TileSet tileSet in import.TileSets)
                _mainWindowVm.OpenTileSet(tileSet);
        }

        if (WantsSprites && import.Sprites is { } bank)
            _mainWindowVm.OpenSpriteBank(bank);

        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelImport() => _mainWindowVm.RightPanViewModel = null;
}
