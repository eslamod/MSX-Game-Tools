using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Formulario para sacar sólo un trozo de la tabla de patrones.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque un juego no siempre carga los 64 de golpe: cada pantalla trae sus bichos, o no
/// caben todos en VRAM a la vez. La exportación normal saca el banco entero y las dos tablas;
/// ésta saca los patrones que se le digan y nada más.
/// </para>
/// <para>
/// Los grupos no salen de aquí, y a propósito. Apuntan a números de patrón del banco, así que un
/// fichero de grupos junto a media tabla sólo tiene sentido si el juego sabe cuánto rebajar, y
/// eso es cosa suya. Lo que sí se dice, en la cabecera del ensamblador, es dónde cae cada índice
/// dentro del fichero.
/// </para>
/// </remarks>
public partial class ExportPatternsViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly SpritesEditorViewModel _editor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeLabel))]
    [NotifyPropertyChangedFor(nameof(FirstImage))]
    private int _first;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeLabel))]
    [NotifyPropertyChangedFor(nameof(LastImage))]
    private int _last;

    [ObservableProperty]
    private string? _resultMessage;

    public ExportPatternsViewModel(MainWindowViewModel mainWindowVm, SpritesEditorViewModel editor)
    {
        _mainWindowVm = mainWindowVm;
        _editor = editor;

        Header = Text.Format("ExportPatternsHeader", editor.SpritesBank.Name);
        TagId = "export:patterns";

        // Hasta el último dibujado, que es lo que saca la exportación de siempre: quien abre
        // esto para acotar un trozo parte de lo que ya tenía, no de un banco entero en blanco.
        _last = Math.Max(0, editor.SpritesBank.LastDrawn());
    }

    private static Localizer Text => Localizer.Instance;

    public int MaxPattern => _editor.SpritesBank.Capacity - 1;

    /// <summary>Cuántos salen y cuánto ocupan, que es lo que se va a mirar para el hueco.</summary>
    public string SizeLabel
    {
        get
        {
            int count = Math.Abs(Last - First) + 1;

            return Text.Format("ExportPatternsSize", count, count * SpriteBankExporter.PatternBytes);
        }
    }

    public ImageMini? FirstImage => ImageAt(First);

    public ImageMini? LastImage => ImageAt(Last);

    private ImageMini? ImageAt(int index) =>
        (uint)index < (uint)_editor.ImagesMiniList.Count ? _editor.ImagesMiniList[index] : null;

    [RelayCommand]
    private async Task ExportAssemblerAsync()
    {
        string? path = await Pick(".asm", PickerFileKind.Assembler);

        await WriteAsync(path, () => File.WriteAllTextAsync(
            path!, SpriteBankExporter.PatternsToAssembler(
                _editor.SpritesBank, First, Last, _mainWindowVm.Preferences.AsmStyle)));
    }

    [RelayCommand]
    private async Task ExportBinaryAsync()
    {
        string? path = await Pick(".bin", PickerFileKind.Binary);

        await WriteAsync(path, () => File.WriteAllBytesAsync(
            path!, SpriteBankExporter.PatternsToBinary(_editor.SpritesBank, First, Last)));
    }

    /// <summary>
    /// El nombre propuesto lleva el rango.
    /// </summary>
    /// <remarks>
    /// Quien saca trozos saca varios, y en la carpeta acaban tres ficheros del mismo banco. Sin
    /// el rango en el nombre hay que abrirlos para saber cuál es cuál, o pisarse uno.
    /// </remarks>
    private Task<string?> Pick(string extension, PickerFileKind kind) =>
        _mainWindowVm.Dialogs.PickFileToSaveAsync(
            Text["PickExportPatterns"],
            $"{SpriteBankExporter.LabelOf(_editor.SpritesBank.Name)}"
                + $"_patterns_{Math.Min(First, Last)}_{Math.Max(First, Last)}{extension}",
            kind);

    private async Task WriteAsync(string? path, Func<Task> write)
    {
        if (path is null)
            return;

        try
        {
            await write();

            ResultMessage = Text.Format("ExportPatternsDone", Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await _mainWindowVm.Dialogs.ShowMessageAsync(Text["ErrorExportSpriteBank"], exception.Message);
        }
    }

    [RelayCommand]
    private void CloseExportPatterns() => _mainWindowVm.RightPanViewModel = null;
}
