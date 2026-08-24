using Avalonia.Headless.XUnit;
using Avalonia;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.Localization;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que dicen los formularios cuando algo no cuadra, en el idioma elegido.
/// </summary>
/// <remarks>
/// Estaban escritos a mano en español dentro del código, así que quien tuviera el programa en
/// inglés o en catalán recibía el aviso en español y sin que nada fallara. Nadie los miraba:
/// las pruebas de estos formularios comprueban que avisan, no en qué idioma.
/// </remarks>
public class FormMessagesLanguageTests : IDisposable
{
    private readonly string _before = Localizer.Instance.Language;

    public void Dispose() => Localizer.Instance.Language = _before;

    /// <summary>El aviso de un formulario cambia con el idioma.</summary>
    [AvaloniaFact]
    public void El_aviso_del_formulario_va_en_el_idioma_elegido()
    {
        Localizer.Instance.Language = "es";
        string spanish = EmptyNameError();

        Localizer.Instance.Language = "en";
        string english = EmptyNameError();

        Assert.NotEqual(spanish, english);
        Assert.Equal(Localizer.Instance["NewMapNoName"], english);
    }

    /// <summary>Y el que lleva un número dentro, también.</summary>
    [AvaloniaFact]
    public void El_aviso_con_un_numero_dentro_tambien()
    {
        Localizer.Instance.Language = "en";

        MainWindowViewModel main = WithTileSet();

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        // Con nombre: si no, salta antes el aviso del nombre y no se llega al del tamaño.
        form.Name = "Nivel";
        form.Columns = TileMap.MaxSide + 1;
        form.AcceptMapCommand.Execute(null);

        Assert.Equal(
            Localizer.Instance.Format("NewMapSizeRange", TileMap.MaxSide),
            form.ErrorMessage);

        Assert.Contains(TileMap.MaxSide.ToString(), form.ErrorMessage!);
    }

    /// <summary>
    /// Los cuatro avisos del supertile dicen algo, no el nombre de su clave.
    /// </summary>
    /// <remarks>
    /// La clave se arma juntando trozos —«PropsSuperOn» más «Many»—, y una clave que no existe
    /// no da error: el localizador devuelve la propia clave, así que el aviso saldría con el
    /// nombre interno puesto y nadie se enteraría hasta verlo en pantalla.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    public void Los_avisos_del_supertile_dicen_algo(int maps, bool useSuperTiles)
    {
        var main = new MainWindowViewModel();
        var tileSet = new TileSet("Bosque");

        // Se parte del revés de lo que se va a probar, que si no no hay nada que avisar.
        if (!useSuperTiles)
            tileSet.UseSuperTiles(2, 2);

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet);

        for (int each = 0; each < maps; each++)
            main.OpenMap(new TileMap($"Nivel {each}", 8, 8), tiles);

        var form = new EditPropertiesViewModel(main, tiles) { UseSuperTiles = useSuperTiles };

        string warning = form.SuperTileWarning;

        Assert.NotEmpty(warning);
        Assert.DoesNotContain("PropsSuper", warning);
        Assert.Contains(useSuperTiles ? "supertile" : "tile", warning);
    }

    /// <summary>
    /// Y los avisos del importador de hojas, que son de los que más se leen.
    /// </summary>
    /// <remarks>
    /// Estos viven en un servicio y no en un modelo de vista, pero acaban igual en pantalla:
    /// son lo que se lee cuando una hoja no entra, y estaban en español para todo el mundo.
    /// </remarks>
    [AvaloniaFact]
    public void Los_avisos_del_importador_van_en_el_idioma_elegido()
    {
        Localizer.Instance.Language = "es";
        string spanish = BadCellSize();

        Localizer.Instance.Language = "en";
        string english = BadCellSize();

        Assert.NotEqual(spanish, english);
        Assert.Equal(Localizer.Instance.Format("SheetBadCellSize", 7, "8 or 16"), english);
    }

    /// <summary>El aviso de una celda de un tamaño que no se sabe leer.</summary>
    private static string BadCellSize()
    {
        SheetAnalysis analysis = SpriteSheetAnalysis.Analyse(
            new int[64],
            new PixelSize(8, 8),
            7,
            null,
            new SheetSelection(0, 0, 1, 1),
            1);

        return Assert.Single(analysis.Problems);
    }

    private static string EmptyNameError()
    {
        MainWindowViewModel main = WithTileSet();

        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;

        form.Name = "   ";
        form.AcceptMapCommand.Execute(null);

        return form.ErrorMessage ?? string.Empty;
    }

    private static MainWindowViewModel WithTileSet()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));

        return main;
    }
}
