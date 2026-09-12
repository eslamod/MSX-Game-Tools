using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un tile en el informe: lo que le toca y lo que cuesta.
/// </summary>
/// <remarks>
/// Lleva el paso por las posiciones, y por eso es una clase con estado y no un registro. Un tile
/// de fondo sale roto en cien sitios: la lista entera no cabe en el panel ni se lee, así que lo
/// que hay es un botón que va llevando a uno detrás de otro.
/// </remarks>
public partial class ShiftTileRowViewModel(
    ShiftReportViewModel panel, ShiftTileReport report, ImageMini? image) : ObservableObject
{
    private int _next;

    public int Tile => report.Tile;

    public ImageMini? Image => image;

    /// <summary>Que no se pinte en el informe. Lo lleva el panel, que es quien rehace.</summary>
    public bool IsIgnored
    {
        get => panel.IsIgnored(Tile);
        set => panel.Ignore(Tile, value);
    }

    /// <summary>Lo que le toca, escrito.</summary>
    public string Fill => Text[NameOf(report.Suggested)];

    /// <summary>Y los que dan el mismo resultado, si los hay.</summary>
    public string? AlsoWork => report.AlsoWork.Count == 0
        ? null
        : Text.Format("ShiftAlsoWork", string.Join(", ", report.AlsoWork.Select(fill => Text[NameOf(fill)])));

    public bool IsClean => report.Clean;

    /// <summary>En cuántas celdas sale y en cuántas se va a notar.</summary>
    public string Cost => report.Clean
        ? Text.Format("ShiftClean", report.Places)
        : Text.Format("ShiftCost", Trouble.Count, report.Places);

    /// <summary>Los sitios donde no vale ninguno, que no se arreglan moviendo el tile.</summary>
    public string? Stuck => report.Impossible.Count == 0
        ? null
        : Text.Format("ShiftStuck", report.Impossible.Count);

    /// <summary>Por dónde va el paseo, para saber si quedan sitios por ver.</summary>
    public string? Step => Trouble.Count == 0
        ? null
        : Text.Format("ShiftStep", Math.Min(_next + 1, Trouble.Count), Trouble.Count);

    /// <summary>
    /// Qué pasa en el sitio al que lleva el botón: dónde, quién es el vecino y qué pedía.
    /// </summary>
    /// <remarks>
    /// Sin el motivo el informe no se explica solo. Un tile puede salir doce veces junto al
    /// terreno llano y una junto al final de su propio dibujo, y leyendo «una de trece celdas no
    /// se verá bien» no hay manera de saber cuál de los dos es el caso raro.
    /// </remarks>
    public string? Reason
    {
        get
        {
            if (Trouble.Count == 0)
                return null;

            ShiftTrouble next = Trouble[_next];

            return next.Wanted is { } wanted
                ? Text.Format("ShiftWanted", next.Cell.Column, next.Cell.Row, next.Neighbour, Text[NameOf(wanted)])
                : Text.Format("ShiftNoFill", next.Cell.Column, next.Cell.Row, next.Neighbour);
        }
    }

    /// <summary>Las que hay que ir a mirar: primero las rotas y luego las que no tienen arreglo.</summary>
    private IReadOnlyList<ShiftTrouble> Trouble { get; } = [.. report.Broken, .. report.Impossible];

    private static Localizer Text => Localizer.Instance;

    private static string NameOf(ShiftFill fill) => fill switch
    {
        ShiftFill.Zeros => "ShiftZeros",
        ShiftFill.Ones => "ShiftOnes",
        _ => "ShiftNext",
    };

    /// <summary>Lleva el mapa al siguiente sitio que no se va a ver bien.</summary>
    [RelayCommand(CanExecute = nameof(HasTrouble))]
    private void ShowNext()
    {
        panel.ShowCell(Trouble[_next].Cell);

        // Da la vuelta al llegar al final. Recorrer ciento y pico sitios y quedarse con el
        // botón apagado obligaría a cerrar el informe y volver a sacarlo para repasarlos.
        _next = (_next + 1) % Trouble.Count;

        OnPropertyChanged(nameof(Step));
        OnPropertyChanged(nameof(Reason));
    }

    private bool HasTrouble() => Trouble.Count > 0;
}

/// <summary>
/// Lo que costaría desplazar este mapa un pixel, y la tabla que hace falta para hacerlo.
/// </summary>
/// <remarks>
/// <para>
/// No es un «pasa o no pasa». Un tile puesto junto a vecinos distintos no tiene una respuesta
/// buena para todos, y eso es la técnica y no un fallo del mapa: lo que hace falta es la opción
/// mayoritaria, cuánto cuesta y dónde se va a notar, para ir a mirarlo y decidir.
/// </para>
/// <para>
/// Por eso los ignorados. El tile de fondo toca con todo y se lleva casi todas las posiciones
/// rotas del mapa; mientras esté dentro, los demás quedan enterrados debajo de su cuenta.
/// </para>
/// </remarks>
public partial class ShiftReportViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly MapEditorViewModel _editor;
    private readonly HashSet<int> _ignored = [];

    private MapShiftReport _report;

    /// <summary>Sólo los que dan guerra, que son los que se van a mirar.</summary>
    [ObservableProperty]
    private bool _onlyDirty = true;

    /// <summary>
    /// Del juego de tiles, el primero que se desplaza.
    /// </summary>
    /// <remarks>
    /// Las copias desplazadas cuestan VRAM y casi nadie las hace de los 256: lo normal es dejar
    /// los primeros para el marcador y los números, que son iguales en las ocho tablas.
    /// </remarks>
    [ObservableProperty]
    private int _firstTile;

    [ObservableProperty]
    private int _lastTile = TileSet.TileCount - 1;

    /// <summary>
    /// Del mapa, la primera fila que se desplaza.
    /// </summary>
    /// <remarks>
    /// La de arriba suele ser el marcador y no se mueve. Sin esto, un tile puede salir con una
    /// sola posición rota y estar esa posición donde nunca se va a notar.
    /// </remarks>
    [ObservableProperty]
    private int _firstRow;

    [ObservableProperty]
    private int _lastRow;

    [ObservableProperty]
    private string? _resultMessage;

    /// <summary>
    /// Which band of the screen is being looked at.
    /// </summary>
    /// <remarks>
    /// The report splits by band and does not lose anything on the way: it only ever compares a
    /// cell with the one to its right, which is in the same row, so nothing it looks at crosses
    /// the line between two bands. What it cannot do is mix them, because the same tile number
    /// is another drawing in each band and the table of shifted copies is one per band.
    /// </remarks>
    [ObservableProperty]
    private int _band;

    public ShiftReportViewModel(MainWindowViewModel mainWindowVm, MapEditorViewModel editor)
    {
        _mainWindowVm = mainWindowVm;
        _editor = editor;

        Header = Text.Format("ShiftHeader", editor.Map.Name);
        TagId = "shift:report";

        _lastRow = MaxRow;

        _report = Analyse();

        Fill();
    }

    private static Localizer Text => Localizer.Instance;

    public int MaxTile => TileSet.TileCount - 1;

    /// <summary>Whether the map is drawn with more than one tile set, and there are bands to pick.</summary>
    public bool ShowsBands => _editor.BandCount > 1;

    /// <summary>And whether it reaches the bottom one.</summary>
    public bool HasBottomBand => _editor.BandCount > 2;

    /// <summary>The first row that can be looked at: the top one of the band.</summary>
    public int MinRow => ShowsBands ? Band * TileMap.RowsPerThird : 0;

    /// <summary>And the last one, which is where the band ends or where the map does.</summary>
    public int MaxRow => ShowsBands
        ? Math.Min(((Band + 1) * TileMap.RowsPerThird) - 1, _editor.Map.Height - 1)
        : Math.Max(0, _editor.Map.Height - 1);

    /// <summary>Which tile set the band being looked at is drawn with.</summary>
    public string BandName => Text.Format("ShiftBandTiles", _editor.Map.TileSetFor(MinRow).Name);

    /// <summary>
    /// The name of what gets exported, with the band when there is more than one.
    /// </summary>
    /// <remarks>
    /// Each band has its own table of shifted copies, so they cannot all come out with the same
    /// name: the second one would be saved over the first without saying a word.
    /// </remarks>
    public string ExportName => ShowsBands
        ? $"{_editor.Map.Name} {_editor.Map.TileSetFor(MinRow).Name}"
        : _editor.Map.Name;

    /// <summary>Las filas que se ven ahora mismo.</summary>
    public ObservableCollection<ShiftTileRowViewModel> Rows { get; } = [];

    /// <summary>De cuántas celdas hablamos y cuántas no se van a ver bien.</summary>
    public string Totals => Text.Format("ShiftTotals", _report.Places, _report.Broken);

    /// <summary>Cuántos tiles ata el mapa y cuántos se contradicen.</summary>
    public string Counts =>
        Text.Format("ShiftCounts", _report.Tiles.Count, _report.Tiles.Count(tile => !tile.Clean));

    /// <summary>Los que se han sacado del informe, para poder devolverlos.</summary>
    public string? IgnoredLabel => _ignored.Count == 0
        ? null
        : Text.Format("ShiftIgnored", string.Join(", ", _ignored.Order()));

    /// <summary>Cuánto ocupa lo que se va a exportar, que sale del rango de tiles.</summary>
    public string TableLabel =>
        Text.Format("ShiftTableSize", _report.Scope.TileCount, _report.Scope.FirstTile, _report.Scope.LastTile);

    public bool IsIgnored(int tile) => _ignored.Contains(tile);

    /// <summary>Saca un tile del informe, o lo devuelve, y rehace las cuentas.</summary>
    public void Ignore(int tile, bool ignored)
    {
        if (!(ignored ? _ignored.Add(tile) : _ignored.Remove(tile)))
            return;

        Recompute();
    }

    /// <summary>Lleva el mapa a una celda y la deja marcada.</summary>
    public void ShowCell(MapCell cell) => _editor.ShowCell(cell.Column, cell.Row);

    /// <summary>
    /// El ámbito tal y como está puesto, con los extremos ordenados.
    /// </summary>
    /// <remarks>
    /// Ordenados y no validados: quien escribe un rango a mano pasa por estados a medias —sube
    /// el primero por encima del último antes de subir el último— y plantarse con el informe
    /// vacío mientras tanto no ayuda a nadie.
    /// </remarks>
    private ShiftScope Scope() => new()
    {
        FirstTile = Math.Min(FirstTile, LastTile),
        LastTile = Math.Max(FirstTile, LastTile),
        FirstRow = Math.Clamp(Math.Min(FirstRow, LastRow), MinRow, MaxRow),
        LastRow = Math.Clamp(Math.Max(FirstRow, LastRow), MinRow, MaxRow),
        Ignored = _ignored,
    };

    private MapShiftReport Analyse() =>
        MapShiftAnalysis.Of(_editor.Map, _editor.TileSetOfBand(Band), Scope());

    /// <summary>
    /// Rehace el informe entero.
    /// </summary>
    /// <remarks>
    /// Y no sólo quita una fila: sacar un tile cambia las cuentas de todos, porque las celdas
    /// que aportaba dejan de contar en los totales.
    /// </remarks>
    [RelayCommand]
    private void Recompute()
    {
        _report = Analyse();

        Fill();

        OnPropertyChanged(nameof(Totals));
        OnPropertyChanged(nameof(Counts));
        OnPropertyChanged(nameof(IgnoredLabel));
        OnPropertyChanged(nameof(TableLabel));
    }

    private void Fill()
    {
        Rows.Clear();

        IEnumerable<ShiftTileReport> tiles = OnlyDirty ? _report.Dirty : _report.Tiles;

        foreach (ShiftTileReport tile in tiles)
            Rows.Add(new ShiftTileRowViewModel(this, tile, TileAt(tile.Tile)));
    }

    partial void OnOnlyDirtyChanged(bool value) => Fill();

    partial void OnFirstTileChanged(int value) => Recompute();

    partial void OnLastTileChanged(int value) => Recompute();

    partial void OnFirstRowChanged(int value) => Recompute();

    partial void OnLastRowChanged(int value) => Recompute();

    /// <summary>
    /// Changing band moves the rows to the ones of that band.
    /// </summary>
    /// <remarks>
    /// The range is inside the band, not on top of it: what the rows are for is leaving out the
    /// scoreboard and whatever else does not scroll, and that is said band by band.
    /// </remarks>
    partial void OnBandChanged(int value)
    {
        OnPropertyChanged(nameof(MinRow));
        OnPropertyChanged(nameof(MaxRow));
        OnPropertyChanged(nameof(BandName));

        FirstRow = MinRow;
        LastRow = MaxRow;

        Recompute();
    }

    /// <summary>El dibujo de un tile, o nada si ese número no existe en el juego.</summary>
    private ImageMini? TileAt(int index)
    {
        IList<ImageMini> tiles = _editor.TilesOfBand(Band);

        return (uint)index < (uint)tiles.Count ? tiles[index] : null;
    }

    [RelayCommand]
    private async Task ExportAssemblerAsync()
    {
        string? path = await _mainWindowVm.Dialogs.PickFileToSaveAsync(
            Text["PickExportShift"],
            $"{MainWindowViewModel.CleanFileName(ExportName)}_shift.asm",
            PickerFileKind.Assembler);

        await WriteAsync(path, () => File.WriteAllTextAsync(
            path!, MapShiftExporter.ToAssembler(
                _report, ExportName, _mainWindowVm.Preferences.AsmStyle)));
    }

    [RelayCommand]
    private async Task ExportBinaryAsync()
    {
        string? path = await _mainWindowVm.Dialogs.PickFileToSaveAsync(
            Text["PickExportShift"],
            $"{MainWindowViewModel.CleanFileName(ExportName)}_shift.bin",
            PickerFileKind.Binary);

        await WriteAsync(path, () => File.WriteAllBytesAsync(path!, MapShiftExporter.ToBinary(_report)));
    }

    private async Task WriteAsync(string? path, Func<Task> write)
    {
        if (path is null)
            return;

        try
        {
            await write();

            ResultMessage = Text.Format("ShiftExported", Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await _mainWindowVm.Dialogs.ShowMessageAsync(Text["ErrorExportShift"], exception.Message);
        }
    }

    [RelayCommand]
    private void CloseShiftReport() => _mainWindowVm.RightPanViewModel = null;
}
