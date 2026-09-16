using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Importar y exportar un juego de tiles como png, con ficheros de verdad.</summary>
public class TileSetPngCommandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxpng-{Guid.NewGuid():N}");

    public TileSetPngCommandsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public async Task Exportar_escribe_un_png_del_tamano_del_juego()
    {
        string path = Path.Combine(_folder, "bosque.png");
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel editor = main.OpenTileSet(new TileSet("Bosque"));
        editor.PixelSurface.Set(2, 3, true);

        await TestExport.TileSetAsync(main, ExportFormat.Png, path);

        (int[] pixels, PixelSize size) = PngFile.Read(path);

        Assert.Equal(TileSetPngConverter.FullSize, size);
        Assert.Equal(256 * 64, pixels.Length);
    }

    [AvaloniaFact]
    public void Exportar_esta_deshabilitado_sin_un_juego_delante()
        => Assert.False(new MainWindowViewModel().ExportTileSetCommand.CanExecute(null));

    /// <summary>Ida y vuelta pasando por el disco, que es como lo va a usar.</summary>
    [AvaloniaFact]
    public async Task Exportar_y_volver_a_importar_devuelve_el_mismo_dibujo()
    {
        string path = Path.Combine(_folder, "bosque.png");
        var dialogs = new TestDialogService { SavePath = path, OpenPath = path, ChooseAnswer = false };

        // Importar pregunta dos cosas seguidas: la paleta -la de siempre- y el modo, que aquí
        // tiene que ser screen 2 para que el color siga siendo de cada línea.
        dialogs.ChooseAnswers.Enqueue(false);
        dialogs.ChooseAnswers.Enqueue(true);

        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel editor = main.OpenTileSet(new TileSet("Bosque"));

        editor.RowColors[0].PickForegroundCommand.Execute(editor.ColorPalette[8]);
        editor.RowColors[0].PickBackgroundCommand.Execute(editor.ColorPalette[4]);
        editor.PixelSurface.Set(0, 0, true);
        editor.PixelSurface.Set(1, 0, true);

        await TestExport.TileSetAsync(main, ExportFormat.Png, path);
        await main.ImportTileSetPngCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Tabs.Count);

        TileRow line = ((TileSetEditorViewModel)main.Tabs[1]).TileSet.ListOfTiles[0].ArrayTileRows[0];

        Assert.Equal(8, line.ArrayPattern[0] ? line.ForeColor : line.BackColor);
        Assert.Equal(4, line.ArrayPattern[2] ? line.ForeColor : line.BackColor);
    }

    [AvaloniaFact]
    public async Task Una_imagen_que_no_cumple_no_se_importa_y_dice_por_que()
    {
        string path = WritePng("mala.png", 20, 8, _ => unchecked((int)0xFF000000));
        var dialogs = new TestDialogService { OpenPath = path, ChooseAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        await main.ImportTileSetPngCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs);
        Assert.Single(dialogs.Messages);
        Assert.Contains("múltiplos", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Con_demasiados_colores_ni_se_pregunta_por_la_paleta()
    {
        // Veinte colores distintos, mas de los quince que caben.
        string path = WritePng("colorida.png", 32, 8, i => unchecked((int)(0xFF000000 | (uint)(i * 1000))));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.ImportTileSetPngCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs);
        Assert.Equal(0, dialogs.ChooseCalls);
        Assert.Single(dialogs.Messages);
        Assert.Contains("15", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Cancelar_la_eleccion_de_paleta_no_importa_nada()
    {
        string path = WritePng("dos.png", 8, 8, i => i < 4 ? unchecked((int)0xFFFF0000) : unchecked((int)0xFF00FF00));
        var dialogs = new TestDialogService { OpenPath = path, ChooseAnswer = null };
        var main = new MainWindowViewModel(dialogs);

        await main.ImportTileSetPngCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ChooseCalls);
        Assert.Empty(main.Tabs);
        Assert.Empty(dialogs.Messages);
    }

    /// <summary>
    /// Con paleta generada los colores se respetan tal cual, salvo el recorte a los tres
    /// bits del MSX2. Y la paleta nueva pasa a ser la activa, que si no el juego se
    /// dibujaria con otros colores.
    /// </summary>
    [AvaloniaFact]
    public async Task Generar_paleta_la_deja_activa_y_respeta_los_colores()
    {
        string path = WritePng("dos.png", 8, 8, i => i < 4 ? unchecked((int)0xFFFF0000) : unchecked((int)0xFF00FF00));
        var dialogs = new TestDialogService { OpenPath = path, ChooseAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        await main.ImportTileSetPngCommand.ExecuteAsync(null);

        Assert.Single(main.Tabs);
        Assert.Equal("dos (png)", main.Palettes.ActivePalette.Name);
        Assert.Equal("700", main.Palettes.ActivePalette[1].HexRgb);
        Assert.Equal("070", main.Palettes.ActivePalette[2].HexRgb);
    }

    /// <summary>
    /// Lo que estaba vacío en GIMP tiene que seguir viéndose vacío al volver. Es código 0,
    /// que se dibuja del color del borde, y el borde arrancaba fijo en el índice 1: el que
    /// la paleta generada acaba de darle al primer color de la imagen.
    /// </summary>
    [AvaloniaFact]
    public async Task Con_paleta_generada_lo_transparente_no_se_ve_del_primer_color()
    {
        // Un tile rojo y otro entero transparente, como al traer de GIMP un juego a medias.
        string path = WritePng("medias.png", 16, 8, i => i % 16 < 8 ? unchecked((int)0xFFFF0000) : 0);

        var dialogs = new TestDialogService { OpenPath = path, ChooseAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        await main.ImportTileSetPngCommand.ExecuteAsync(null);

        var editor = (TileSetEditorViewModel)main.Tabs[0];

        Assert.Equal("700", main.Palettes.ActivePalette[1].HexRgb);
        Assert.NotEqual(1, editor.BorderColorIndex);
        Assert.Equal("000", editor.BorderColor.HexRgb);

        // Y la miniatura del tile vacio sale negra de verdad, no roja.
        Assert.Equal(0, editor.TileSet.ListOfTiles[1].ArrayTileRows[0].BackColor);
        Assert.Equal(PixelReader.Bgra(Colors.Black), PixelReader.At(editor.Thumbnails[1], 0, 0));
    }

    private string WritePng(string name, int width, int height, Func<int, int> pixel)
    {
        string path = Path.Combine(_folder, name);
        int[] pixels = new int[width * height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = pixel(i);

        PngFile.Write(path, pixels, new PixelSize(width, height));

        return path;
    }
}
