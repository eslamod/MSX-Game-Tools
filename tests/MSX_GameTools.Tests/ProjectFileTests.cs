using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Guardar y abrir el proyecto: el índice de lo que hay y dónde está.
/// </summary>
/// <remarks>
/// El proyecto no lleva nada dentro salvo el catálogo de paletas: cada juego de tiles,
/// banco y mapa sigue en su fichero. Lo que se prueba aquí es que el índice sepa
/// encontrarlos, que guardar el proyecto guarde lo que haga falta y nada más, y que
/// abrir otro no se lleve por delante trabajo sin guardar.
/// </remarks>
public class ProjectFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxproj-{Guid.NewGuid():N}");

    public ProjectFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string ProjectPath => Path.Combine(_folder, "Mi juego.msxproj");

    // ------------------------------------------------------------------ el formato

    [AvaloniaFact]
    public void El_indice_va_y_vuelve()
    {
        var project = new Project(
            "Mi juego",
            [
                new ProjectItem(ProjectItemKind.TileSet, "Bosque.json"),
                new ProjectItem(ProjectItemKind.Map, "mapas/Nivel 1.json"),
            ],
            [new PaletteLibrary().Add("Nocturna")],
            [new BackgroundImageRef(@"C:\hoja.png", 16)]);

        Project read = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project));

        Assert.Equal("Mi juego", read.Name);
        Assert.Equal(2, read.Items.Count);
        Assert.Equal(ProjectItemKind.TileSet, read.Items[0].Kind);
        Assert.Equal("Nocturna", read.Palettes[0].Name);
        Assert.Equal(16, read.Backgrounds[0].CellSize);
    }

    /// <summary>Un índice se lee y se corrige a mano; los tipos por su nombre.</summary>
    [AvaloniaFact]
    public void Los_tipos_se_escriben_con_su_nombre()
    {
        string json = ProjectSerializer.Serialize(new Project(
            "Mi juego", [new ProjectItem(ProjectItemKind.SpriteBank, "Bichos.json")], [], []));

        Assert.Contains("spriteBank", json);
    }

    /// <summary>Con barras normales, para que la carpeta se pueda llevar a otra máquina.</summary>
    [AvaloniaFact]
    public void Las_rutas_se_escriben_con_barras_normales()
    {
        string json = ProjectSerializer.Serialize(new Project(
            "Mi juego", [new ProjectItem(ProjectItemKind.Map, @"mapas\Nivel 1.json")], [], []));

        Assert.Contains("mapas/Nivel 1.json", json);
    }

    [AvaloniaFact]
    public void Un_formato_mas_nuevo_se_rechaza()
    {
        FileFormatException error = Assert.Throws<FileFormatException>(
            () => ProjectSerializer.Deserialize("""{ "version": 99, "name": "De mañana" }"""));

        Assert.Contains("99", error.Message);
    }

    // ------------------------------------------------------------------ guardar

    [AvaloniaFact]
    public async Task Guardar_el_proyecto_escribe_el_indice_y_sus_documentos()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        MainWindowViewModel main = Sample(dialogs);

        await main.SaveProjectCommand.ExecuteAsync(null);

        // Cada uno en su fichero, junto al proyecto y con su nombre.
        Assert.True(File.Exists(ProjectPath));
        Assert.True(File.Exists(Path.Combine(_folder, "Bosque.json")));
        Assert.True(File.Exists(Path.Combine(_folder, "Bichos.json")));
        Assert.True(File.Exists(Path.Combine(_folder, "Nivel 1.json")));

        Project index = ProjectSerializer.Deserialize(File.ReadAllText(ProjectPath));

        Assert.Equal(3, index.Items.Count);
        Assert.All(index.Items, item => Assert.False(Path.IsPathRooted(item.Path)));

        // Los juegos de tiles delante: un mapa necesita el suyo para poder abrirse.
        Assert.Equal(ProjectItemKind.TileSet, index.Items[0].Kind);
    }

    [AvaloniaFact]
    public async Task Guardar_dos_veces_no_vuelve_a_pedir_la_ruta()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        MainWindowViewModel main = Sample(dialogs);

        await main.SaveProjectCommand.ExecuteAsync(null);

        int asked = dialogs.SaveCalls;

        await main.SaveProjectCommand.ExecuteAsync(null);

        Assert.Equal(asked, dialogs.SaveCalls);

        await main.SaveProjectAsCommand.ExecuteAsync(null);

        Assert.Equal(asked + 1, dialogs.SaveCalls);
    }

    /// <summary>
    /// Guardar el proyecto no le cambia la fecha a todo lo que agrupa: sólo se escribe lo
    /// que se ha tocado.
    /// </summary>
    [AvaloniaFact]
    public async Task Lo_que_no_se_ha_tocado_no_se_reescribe()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        MainWindowViewModel main = Sample(dialogs);

        await main.SaveProjectCommand.ExecuteAsync(null);

        string tiles = Path.Combine(_folder, "Bosque.json");
        await File.WriteAllTextAsync(tiles, "no me toques");

        await main.SaveProjectCommand.ExecuteAsync(null);

        Assert.Equal("no me toques", File.ReadAllText(tiles));
    }

    /// <summary>Dos documentos con el mismo nombre no pueden acabar en el mismo fichero.</summary>
    [AvaloniaFact]
    public async Task Dos_documentos_con_el_mismo_nombre_no_se_pisan()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        var main = new MainWindowViewModel(dialogs);

        NewTileSet(main, "Bosque");
        NewTileSet(main, "Bosque");

        await main.SaveProjectCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(_folder, "Bosque.json")));
        Assert.True(File.Exists(Path.Combine(_folder, "Bosque 2.json")));
    }

    /// <summary>
    /// Y tampoco se quedan con el fichero de otro que ya lo tenía apuntado.
    /// </summary>
    /// <remarks>
    /// Pasa si alguien borra un fichero por fuera: su documento sigue apuntando ahí, y un
    /// documento nuevo con el mismo nombre encontraría el sitio libre y se lo quedaría.
    /// Los dos acabarían en el índice apuntando al mismo fichero.
    /// </remarks>
    [AvaloniaFact]
    public async Task Un_documento_nuevo_no_se_queda_con_el_fichero_de_otro()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel first = NewTileSet(main, "Bosque");

        await main.SaveProjectCommand.ExecuteAsync(null);

        string taken = Path.Combine(_folder, "Bosque.json");

        Assert.Equal(taken, first.FilePath);

        File.Delete(taken);
        NewTileSet(main, "Bosque");

        await main.SaveProjectCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Tabs.Count);
        Assert.Equal(2, main.Tabs.Select(tab => tab.FilePath).Distinct().Count());
    }

    // ------------------------------------------------------------------ abrir

    [AvaloniaFact]
    public async Task Abrir_devuelve_todo_lo_que_habia()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        MainWindowViewModel saved = Sample(dialogs);

        await saved.SaveProjectCommand.ExecuteAsync(null);

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = ProjectPath });

        await main.OpenProjectCommand.ExecuteAsync(null);

        Assert.Equal(3, main.Tabs.Count);
        Assert.Equal(ProjectPath, main.ProjectPath);
        Assert.Equal("Mi juego", main.ProjectName);

        var tiles = main.Tabs.OfType<TileSetEditorViewModel>().Single();
        var bank = main.Tabs.OfType<SpritesEditorViewModel>().Single();
        var map = main.Tabs.OfType<MapEditorViewModel>().Single();

        // Con lo que se pintó, no sólo con los nombres.
        Assert.True(tiles.TileSet.ListOfTiles[0].ArrayTileRows[4].ArrayPattern[3]);
        Assert.Equal(2, bank.SpritesBank.SpritesList.Count);
        Assert.Equal(7, map.Map.Layers[0].Grid[1, 1]);

        // El mapa se ha enganchado a su juego de tiles, que es lo que lo hace dibujable.
        Assert.Equal("Bosque", map.Map.TileSetName);

        // Y nada sale marcado: acaba de salir de sus ficheros.
        Assert.Empty(main.UnsavedDocuments());
    }

    /// <summary>Las rutas son relativas, así que la carpeta se puede mover de sitio.</summary>
    [AvaloniaFact]
    public async Task La_carpeta_se_puede_mover_entera()
    {
        MainWindowViewModel saved = Sample(new TestDialogService { SavePath = ProjectPath });

        await saved.SaveProjectCommand.ExecuteAsync(null);

        string moved = Path.Combine(_folder, "..", $"msxproj-{Guid.NewGuid():N}");
        Directory.Move(_folder, moved);

        try
        {
            string movedProject = Path.Combine(moved, "Mi juego.msxproj");
            var main = new MainWindowViewModel(new TestDialogService { OpenPath = movedProject });

            await main.OpenProjectCommand.ExecuteAsync(null);

            Assert.Equal(3, main.Tabs.Count);
        }
        finally
        {
            Directory.Move(moved, _folder);
        }
    }

    [AvaloniaFact]
    public async Task Abrir_otro_proyecto_cierra_lo_que_hubiera()
    {
        MainWindowViewModel saved = Sample(new TestDialogService { SavePath = ProjectPath });
        await saved.SaveProjectCommand.ExecuteAsync(null);

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = ProjectPath });

        NewTileSet(main, "De antes");
        main.AddMapCommand.Execute(null);

        await main.OpenProjectCommand.ExecuteAsync(null);

        Assert.Equal(3, main.Tabs.Count);
        Assert.DoesNotContain(main.Tabs, tab => tab.Header.StartsWith("De antes", StringComparison.Ordinal));

        // El árbol y el lateral tampoco se quedan con nada del anterior.
        Assert.Equal(3, main.TreeGeneralVm.PrimaryNodes.Sum(node => node.Childs.Count));
        Assert.Null(main.RightPanViewModel);
    }

    [AvaloniaFact]
    public async Task Abrir_con_algo_sin_guardar_avisa_y_cancelar_no_abre()
    {
        MainWindowViewModel saved = Sample(new TestDialogService { SavePath = ProjectPath });
        await saved.SaveProjectCommand.ExecuteAsync(null);

        var dialogs = new TestDialogService { OpenPath = ProjectPath, ChooseAnswer = null };
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = NewTileSet(main, "Sin guardar");
        tiles.PixelSurface.Set(1, 1, true);

        await main.OpenProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ChooseCalls);
        Assert.Single(main.Tabs);
        Assert.Null(main.ProjectPath);
    }

    /// <summary>
    /// Que se haya movido un fichero no puede dejarte sin el resto del proyecto: se abre
    /// lo que se pueda y se dice qué ha faltado.
    /// </summary>
    [AvaloniaFact]
    public async Task Un_fichero_que_falta_no_impide_abrir_el_resto()
    {
        MainWindowViewModel saved = Sample(new TestDialogService { SavePath = ProjectPath });
        await saved.SaveProjectCommand.ExecuteAsync(null);

        File.Delete(Path.Combine(_folder, "Bichos.json"));

        var dialogs = new TestDialogService { OpenPath = ProjectPath };
        var main = new MainWindowViewModel(dialogs);

        await main.OpenProjectCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Tabs.Count);
        Assert.Contains(dialogs.Messages, message => message.Contains("Bichos.json", StringComparison.Ordinal));
    }

    /// <summary>Las que se han creado y todavía no usa nadie también son del proyecto.</summary>
    [AvaloniaFact]
    public async Task Las_paletas_del_catalogo_vuelven_aunque_no_las_use_nadie()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath };
        var main = new MainWindowViewModel(dialogs);

        NewTileSet(main, "Bosque");

        TestPalette.Create(main);
        main.Palettes.ActivePalette.Name = "Nocturna";

        await main.SaveProjectCommand.ExecuteAsync(null);

        var opened = new MainWindowViewModel(new TestDialogService { OpenPath = ProjectPath });
        await opened.OpenProjectCommand.ExecuteAsync(null);

        Assert.Contains(opened.Palettes.Palettes, palette => palette.Name == "Nocturna");
    }

    // ------------------------------------------------------------------ salir

    /// <summary>
    /// El proyecto puede estar sin guardar sin que lo esté ninguno de sus documentos:
    /// basta con haber añadido uno.
    /// </summary>
    [AvaloniaFact]
    public async Task Añadir_un_documento_deja_el_proyecto_sin_guardar()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath, ChooseAnswer = false };
        MainWindowViewModel main = Sample(dialogs);

        await main.SaveProjectCommand.ExecuteAsync(null);

        Assert.True(await main.ConfirmExitAsync());
        Assert.Equal(0, dialogs.ChooseCalls);

        NewTileSet(main, "Cueva");

        await main.ConfirmExitAsync();

        Assert.Equal(1, dialogs.ChooseCalls);
        Assert.Contains("Mi juego (proyecto)", dialogs.LastChooseMessage);
    }

    /// <summary>Con proyecto, guardar y salir es un solo paso y no uno por pestaña.</summary>
    [AvaloniaFact]
    public async Task Guardar_y_salir_con_proyecto_guarda_el_proyecto_entero()
    {
        var dialogs = new TestDialogService { SavePath = ProjectPath, ChooseAnswer = true };
        MainWindowViewModel main = Sample(dialogs);

        await main.SaveProjectCommand.ExecuteAsync(null);

        NewTileSet(main, "Cueva");

        Assert.True(await main.ConfirmExitAsync());
        Assert.True(File.Exists(Path.Combine(_folder, "Cueva.json")));

        Project index = ProjectSerializer.Deserialize(File.ReadAllText(ProjectPath));

        Assert.Equal(4, index.Items.Count);
    }

    // ------------------------------------------------------------------ la ventana

    [AvaloniaFact]
    public async Task El_titulo_de_la_ventana_lleva_el_nombre_del_proyecto()
    {
        var main = new MainWindowViewModel(new TestDialogService { SavePath = ProjectPath, ChooseAnswer = false });
        var window = new MainWindow { DataContext = main };

        window.Show();
        Pump();

        Assert.Equal("MSX Game Tools — Sin proyecto", window.Title);

        NewTileSet(main, "Bosque");
        await main.SaveProjectCommand.ExecuteAsync(null);
        Pump();

        Assert.Equal("MSX Game Tools — Mi juego", window.Title);

        window.Close();
        Pump();
        Pump();
    }

    // ------------------------------------------------------------------ utilidades

    /// <summary>Un proyecto con un juego, un banco y un mapa, con algo puesto en cada uno.</summary>
    private static MainWindowViewModel Sample(IDialogService dialogs)
    {
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = NewTileSet(main, "Bosque");
        tiles.PixelSurface.Set(3, 4, true);

        main.AddSpriteBankCommand.Execute(null);

        var bankForm = (EditSpriteBankViewModel)main.RightPanViewModel!;
        bankForm.Name = "Bichos";
        bankForm.AcceptSpriteBankCommand.Execute(null);

        ((SpritesEditorViewModel)main.SelectedTab!).AddSpriteCommand.Execute(null);

        main.AddMapCommand.Execute(null);

        var mapForm = (EditMapViewModel)main.RightPanViewModel!;
        mapForm.Name = "Nivel 1";
        mapForm.AcceptMapCommand.Execute(null);

        var map = (MapEditorViewModel)main.SelectedTab!;
        map.PickTile(TilePatch.Single(7), "Tile 7");
        map.Paint(1, 1);

        return main;
    }

    private static TileSetEditorViewModel NewTileSet(MainWindowViewModel main, string name)
    {
        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;
        form.Name = name;
        form.AcceptTileSetCommand.Execute(null);

        return (TileSetEditorViewModel)main.SelectedTab!;
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
