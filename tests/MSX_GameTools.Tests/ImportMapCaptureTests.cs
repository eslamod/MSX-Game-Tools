using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Traer los mapas de una captura de pantallas, desde el menú.
/// </summary>
/// <remarks>
/// El cosido tiene sus pruebas aparte; lo que se mira aquí es lo que rodea al comando: que
/// abra el formulario con la zona propuesta, que abra todos los mapas que salen, que pida un
/// juego de tiles antes, y que lo que no se puede leer se diga en vez de dejar la ventana
/// igual.
/// </remarks>
public class ImportMapCaptureTests : IDisposable
{
    private const int Columns = 32;
    private const int Rows = 24;

    private readonly string _folder = Directory.CreateTempSubdirectory("capture").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>
    /// De una captura con dos zonas salen dos mapas, y los dos se abren.
    /// </summary>
    /// <remarks>
    /// Todos y no sólo el primero: cuál interesa se ve abriéndolos, y el que se quedara sin
    /// abrir habría que volver a importarlo sin saber siquiera que estaba ahí.
    /// </remarks>
    [AvaloniaFact]
    public async Task Se_abren_todos_los_mapas_de_la_captura()
    {
        var dialogs = new TestDialogService { OpenPath = Capture() };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        int before = main.Tabs.Count;

        ImportMapCaptureViewModel form = await Form(main);

        form.AcceptImportCommand.Execute(null);

        Assert.Equal(before + 2, main.Tabs.Count);
        Assert.Equal(2, main.Tabs.OfType<MapEditorViewModel>().Count());

        // Y el formulario se cierra al aceptar.
        Assert.Null(main.RightPanViewModel);
    }

    /// <summary>Sin un juego de tiles delante no se importa, y se dice por qué.</summary>
    /// <remarks>
    /// Una captura son números de tile: sin saber de qué juego son no dibujan nada. Es la misma
    /// regla que ya tiene la importación de csv.
    /// </remarks>
    [AvaloniaFact]
    public async Task Sin_juego_de_tiles_no_se_importa()
    {
        var dialogs = new TestDialogService { OpenPath = Capture() };
        var main = new MainWindowViewModel(dialogs);

        await main.ImportMapCaptureCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Single(dialogs.Messages);
    }

    /// <summary>Un fichero que no es una captura se dice, y no se abre nada.</summary>
    [AvaloniaFact]
    public async Task Un_fichero_que_no_es_una_captura_se_dice()
    {
        string path = Path.Combine(_folder, "cualquiera.txt");

        await File.WriteAllTextAsync(path, "esto no es una captura");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        await main.ImportMapCaptureCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Single(dialogs.Messages);
    }

    /// <summary>
    /// Una captura de la que no sale ningún mapa lo dice el informe, y no deja aceptar.
    /// </summary>
    /// <remarks>
    /// Pasa con una partida en la que no se llegó a recorrer nada: pantallas sueltas que no
    /// encajan entre sí. Sin decirlo, el formulario parecería roto.
    /// </remarks>
    [AvaloniaFact]
    public async Task Una_captura_sin_zonas_lo_dice_el_informe()
    {
        string path = Path.Combine(_folder, "suelta.txt");

        await File.WriteAllTextAsync(path, Written([Screen(1), Screen(2)]));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        ImportMapCaptureViewModel form = await Form(main);

        Assert.Equal(Localizer.Instance["CaptureEmptyBody"], form.Report);
        Assert.False(form.AcceptImportCommand.CanExecute(null));
    }

    /// <summary>
    /// La zona viene propuesta con el marcador fuera.
    /// </summary>
    /// <remarks>
    /// Es lo que hay que decidir aquí y lo que más cuesta acertar: si el formulario se abriera
    /// con la pantalla entera, el primer intento de todo el mundo saldría con el reguero de
    /// marcadores dentro.
    /// </remarks>
    [AvaloniaFact]
    public async Task La_zona_viene_propuesta_con_el_marcador_fuera()
    {
        var dialogs = new TestDialogService { OpenPath = Falling() };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        ImportMapCaptureViewModel form = await Form(main);

        Assert.Equal(new MapCapture.Region(0, 0, Columns, Rows - Marker), form.Region);
    }

    /// <summary>Y aceptando, el mapa que se abre no trae ni rastro del marcador.</summary>
    [AvaloniaFact]
    public async Task El_mapa_que_se_trae_no_lleva_marcador()
    {
        var dialogs = new TestDialogService { OpenPath = Falling() };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        ImportMapCaptureViewModel form = await Form(main);

        form.AcceptImportCommand.Execute(null);

        TileMap map = Assert.Single(main.Tabs.OfType<MapEditorViewModel>()).Map;

        Assert.Equal(Rows - Marker + 3, map.Height);

        for (int column = 0; column < map.Width; column++)
        {
            for (int row = 0; row < map.Height; row++)
            {
                int cell = map.Layers[0].Grid[column, row] ?? 0;

                Assert.NotEqual(Labels + column, cell);
                Assert.NotEqual(Values + column, cell);
            }
        }
    }

    /// <summary>
    /// El formulario sale por el ViewLocator, con sus enlaces vivos.
    /// </summary>
    /// <remarks>
    /// Un nombre mal escrito en el XAML no lo caza nadie más: el modelo de vista puede estar
    /// perfecto y el panel salir vacío.
    /// </remarks>
    [AvaloniaFact]
    public async Task El_panel_sale_montado()
    {
        var dialogs = new TestDialogService { OpenPath = Capture() };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        await Form(main);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Single(window.GetVisualDescendants().OfType<ImportMapCaptureView>());

        window.Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Las dos filas de marcador de abajo: los rótulos y los números.</summary>
    private const int Marker = 2;

    private const int Labels = 500;
    private const int Values = 700;

    /// <summary>El formulario que abre el menú.</summary>
    private static async Task<ImportMapCaptureViewModel> Form(MainWindowViewModel main)
    {
        await main.ImportMapCaptureCommand.ExecuteAsync(null);

        return Assert.IsType<ImportMapCaptureViewModel>(main.RightPanViewModel);
    }

    /// <summary>Una captura bajando por un mundo, con el marcador quieto abajo.</summary>
    private string Falling(int screens = 4)
    {
        int[,] world = Tall(Rows + screens);

        string path = Path.Combine(_folder, "bajando.txt");

        File.WriteAllText(path, Written([.. Enumerable.Range(0, screens).Select(at =>
        {
            var screen = new int[Columns, Rows];

            for (int column = 0; column < Columns; column++)
            {
                for (int row = 0; row < Rows; row++)
                    screen[column, row] = world[column, at + row];

                screen[column, Rows - 2] = Labels + column;
                screen[column, Rows - 1] = Values + column;
            }

            return ((long)1, screen);
        })]));

        return path;
    }

    private static int[,] Tall(int rows)
    {
        var world = new int[Columns, rows];
        var random = new Random(77);

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < rows; row++)
                world[column, row] = random.Next(1, 256);
        }

        return world;
    }

    /// <summary>Una captura con dos zonas recorridas, cortadas por un cambio de tileset.</summary>
    private string Capture()
    {
        int[,] world = World(seed: 11);
        int[,] other = World(seed: 22);

        string path = Path.Combine(_folder, "captura.txt");

        File.WriteAllText(path, Written(
        [
            (1, Cut(world, 0)),
            (1, Cut(world, 1)),
            (2, Cut(other, 0)),
            (2, Cut(other, 1)),
        ]));

        return path;
    }

    private static (long Stamp, int[,] Cells) Screen(int seed) => (1, Cut(World(seed), 0));

    private static int[,] World(int seed)
    {
        var world = new int[Columns + 4, Rows];
        var random = new Random(seed);

        for (int column = 0; column < Columns + 4; column++)
        {
            for (int row = 0; row < Rows; row++)
                world[column, row] = random.Next(1, 256);
        }

        return world;
    }

    private static int[,] Cut(int[,] world, int at)
    {
        var screen = new int[Columns, Rows];

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
                screen[column, row] = world[at + column, row];
        }

        return screen;
    }

    /// <summary>El mismo formato que escribe map_grabber.tcl.</summary>
    private static string Written((long Stamp, int[,] Cells)[] screens)
    {
        var text = new System.Text.StringBuilder();

        text.AppendLine("msxmap 1");
        text.AppendLine($"columns {Columns}");
        text.AppendLine($"rows {Rows}");
        text.AppendLine("mode 2");
        text.AppendLine("names 6144");
        text.AppendLine("patterns 0");
        text.AppendLine("colors 8192");
        text.AppendLine("screens");

        foreach ((long stamp, int[,] cells) in screens)
        {
            var numbers = new List<string> { stamp.ToString() };

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                    numbers.Add(cells[column, row].ToString());
            }

            text.AppendLine(string.Join(' ', numbers));
        }

        return text.ToString();
    }
}
