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

    /// <summary>Las que hay que ir a mirar: primero las rotas y luego las que no tienen arreglo.</summary>
    private IReadOnlyList<MapCell> Trouble { get; } = [.. report.Broken, .. report.Impossible];

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
        MapCell cell = Trouble[_next];

        // Da la vuelta al llegar al final. Recorrer ciento y pico sitios y quedarse con el
        // botón apagado obligaría a cerrar el informe y volver a sacarlo para repasarlos.
        _next = (_next + 1) % Trouble.Count;

        panel.ShowCell(cell);

        OnPropertyChanged(nameof(Step));
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

    [ObservableProperty]
    private string? _resultMessage;

    public ShiftReportViewModel(MainWindowViewModel mainWindowVm, MapEditorViewModel editor)
    {
        _mainWindowVm = mainWindowVm;
        _editor = editor;

        Header = Text.Format("ShiftHeader", editor.Map.Name);
        TagId = "shift:report";

        _report = Analyse();

        Fill();
    }

    private static Localizer Text => Localizer.Instance;

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

    private MapShiftReport Analyse() => MapShiftAnalysis.Of(_editor.Map, _editor.TileSet, _ignored);

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
    }

    private void Fill()
    {
        Rows.Clear();

        IEnumerable<ShiftTileReport> tiles = OnlyDirty ? _report.Dirty : _report.Tiles;

        foreach (ShiftTileReport tile in tiles)
            Rows.Add(new ShiftTileRowViewModel(this, tile, TileAt(tile.Tile)));
    }

    partial void OnOnlyDirtyChanged(bool value) => Fill();

    /// <summary>El dibujo de un tile, o nada si ese número no existe en el juego.</summary>
    private ImageMini? TileAt(int index) =>
        (uint)index < (uint)_editor.Tiles.Count ? _editor.Tiles[index] : null;

    [RelayCommand]
    private async Task ExportAssemblerAsync()
    {
        string? path = await _mainWindowVm.Dialogs.PickFileToSaveAsync(
            Text["PickExportShift"],
            $"{MainWindowViewModel.CleanFileName(_editor.Map.Name)}_shift.asm",
            PickerFileKind.Assembler);

        await WriteAsync(path, () => File.WriteAllTextAsync(
            path!, MapShiftExporter.ToAssembler(_report, _editor.Map.Name)));
    }

    [RelayCommand]
    private async Task ExportBinaryAsync()
    {
        string? path = await _mainWindowVm.Dialogs.PickFileToSaveAsync(
            Text["PickExportShift"],
            $"{MainWindowViewModel.CleanFileName(_editor.Map.Name)}_shift.bin",
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
