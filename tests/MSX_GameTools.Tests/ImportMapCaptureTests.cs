using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Traer los mapas de una captura de pantallas, desde el menú.
/// </summary>
/// <remarks>
/// El cosido tiene sus pruebas aparte; lo que se mira aquí es lo que rodea al comando: que
/// abra todos los mapas que salen, que pida un juego de tiles antes, y que lo que no se puede
/// leer se diga en vez de dejar la ventana igual.
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

        await main.ImportMapCaptureCommand.ExecuteAsync(null);

        Assert.Equal(before + 2, main.Tabs.Count);
        Assert.Equal(2, main.Tabs.OfType<MapEditorViewModel>().Count());
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
    /// Una captura de la que no sale ningún mapa se dice en vez de no hacer nada.
    /// </summary>
    /// <remarks>
    /// Pasa con una partida en la que no se llegó a recorrer nada: pantallas sueltas que no
    /// encajan entre sí. Sin decirlo, el menú parecería roto.
    /// </remarks>
    [AvaloniaFact]
    public async Task Una_captura_sin_zonas_se_dice()
    {
        string path = Path.Combine(_folder, "suelta.txt");

        await File.WriteAllTextAsync(path, Written([Screen(1), Screen(2)]));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        await main.ImportMapCaptureCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Contains(Localizer.Instance["CaptureEmptyBody"], dialogs.Messages);
    }

    // ------------------------------------------------------------------ los andamios

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
