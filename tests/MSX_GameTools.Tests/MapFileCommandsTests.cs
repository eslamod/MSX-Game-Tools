using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Guardar y cargar un mapa desde el menú, con ficheros de verdad.</summary>
public class MapFileCommandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxmap-{Guid.NewGuid():N}");

    public MapFileCommandsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Sin_un_mapa_delante_no_se_puede_guardar()
    {
        var main = new MainWindowViewModel();

        Assert.False(main.SaveMapCommand.CanExecute(null));

        main.OpenTileSet(new TileSet("Bosque"));

        // Con un tileset delante tampoco: es otra cosa.
        Assert.False(main.SaveMapCommand.CanExecute(null));

        NewMap(main, "Nivel 1");

        Assert.True(main.SaveMapCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Guardar_y_cargar_devuelve_el_mapa_con_lo_pintado()
    {
        string path = Path.Combine(_folder, "nivel.json");
        var dialogs = new TestDialogService { SavePath = path, OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = NewMap(main, "Nivel 1", 20, 10);
        editor.AddLayerCommand.Execute(null);
        editor.PickTile(TilePatch.Single(9), "Tile 9");
        editor.Paint(3, 4);

        await main.SaveMapCommand.ExecuteAsync(null);

        Assert.True(File.Exists(path));

        await main.LoadMapCommand.ExecuteAsync(null);

        MapEditorViewModel loaded = main.Tabs.OfType<MapEditorViewModel>().Last();

        Assert.NotSame(editor, loaded);
        Assert.Equal("Nivel 1", loaded.Map.Name);
        Assert.Equal((20, 10), (loaded.Map.Width, loaded.Map.Height));
        Assert.Equal(2, loaded.Map.Layers.Count);
        Assert.Equal(9, loaded.Map.Layers[1].Grid[3, 4]);

        // Y se cuelga del arbol como cualquier otro mapa.
        Assert.Equal(2, main.TreeGeneralVm.PrimaryNodes[2].Childs.Count);
    }

    /// <summary>
    /// El juego de tiles no viaja dentro del mapa, sólo su nombre: sin él abierto no hay
    /// con qué dibujarlo, y se dice cuál falta en vez de abrir un mapa inservible.
    /// </summary>
    [AvaloniaFact]
    public async Task Sin_el_juego_de_tiles_abierto_se_dice_cual_falta()
    {
        string path = Path.Combine(_folder, "huerfano.json");

        await File.WriteAllTextAsync(
            path,
            MapSerializerText("Nivel 1", "Bosque"));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        // Con otro juego abierto tampoco vale: tiene que ser el suyo, no uno cualquiera.
        main.OpenTileSet(new TileSet("Ciudad"));

        await main.LoadMapCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Single(dialogs.Messages);
        Assert.Contains("Bosque", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Con_el_juego_de_tiles_abierto_el_mapa_se_engancha_a_el()
    {
        string path = Path.Combine(_folder, "nivel.json");
        await File.WriteAllTextAsync(path, MapSerializerText("Nivel 1", "Bosque"));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Ciudad"));
        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));

        await main.LoadMapCommand.ExecuteAsync(null);

        MapEditorViewModel loaded = Assert.Single(main.Tabs.OfType<MapEditorViewModel>());

        // Los tiles que enseña son los del juego que dice el fichero.
        Assert.Same(bosque.Thumbnails, loaded.Tiles);
        Assert.Empty(dialogs.Messages);
    }

    [AvaloniaFact]
    public async Task Un_fichero_que_no_es_un_mapa_se_rechaza_sin_romper_nada()
    {
        string path = Path.Combine(_folder, "cualquiera.json");
        await File.WriteAllTextAsync(path, "{ esto no es json }");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadMapCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs);
        Assert.Single(dialogs.Messages);
    }

    [AvaloniaFact]
    public async Task Cancelar_el_dialogo_no_escribe_nada()
    {
        var dialogs = new TestDialogService { SavePath = null };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));
        NewMap(main, "Nivel 1");

        await main.SaveMapCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(dialogs.Messages);
    }

    // ------------------------------------------------------------------ csv

    [AvaloniaFact]
    public async Task Exportar_a_csv_escribe_el_mapa_aplastado()
    {
        string path = Path.Combine(_folder, "nivel.csv");
        var main = new MainWindowViewModel(new TestDialogService { SavePath = path });

        main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = NewMap(main, "Nivel 1", 3, 2);
        editor.PickTile(TilePatch.Single(5), "Tile 5");
        editor.Paint(1, 0);

        await main.ExportMapCsvCommand.ExecuteAsync(null);

        Assert.Equal("-1,5,-1\n-1,-1,-1\n", await File.ReadAllTextAsync(path));
    }

    [AvaloniaFact]
    public void Sin_mapa_delante_no_se_puede_exportar_a_csv()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));

        Assert.False(main.ExportMapCsvCommand.CanExecute(null));
    }

    /// <summary>El csv se abre como un mapa nuevo, con el nombre del fichero.</summary>
    [AvaloniaFact]
    public async Task Importar_un_csv_abre_un_mapa_nuevo()
    {
        string path = Path.Combine(_folder, "desde_tiled.csv");
        await File.WriteAllTextAsync(path, "1,2,3,\n4,-1,6,\n");

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        main.OpenTileSet(new TileSet("Bosque"));

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        MapEditorViewModel editor = Assert.Single(main.Tabs.OfType<MapEditorViewModel>());

        Assert.Equal("desde_tiled", editor.Map.Name);
        Assert.Equal((3, 2), (editor.Map.Width, editor.Map.Height));
        Assert.Equal(6, editor.Map.Layers[0].Grid[2, 1]);
        Assert.Null(editor.Map.Layers[0].Grid[1, 1]);
    }

    /// <summary>
    /// Un csv no dice de qué juego son sus números. Con varios abiertos y sin un mapa
    /// delante que lo diga, no hay forma de adivinarlo.
    /// </summary>
    [AvaloniaFact]
    public async Task Con_varios_juegos_y_sin_mapa_delante_no_se_adivina_el_juego()
    {
        string path = Path.Combine(_folder, "mapa.csv");
        await File.WriteAllTextAsync(path, "1,2\n");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));
        main.OpenTileSet(new TileSet("Ciudad"));

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Single(dialogs.Messages);
    }

    /// <summary>Con un mapa delante se hereda su juego, aunque haya varios abiertos.</summary>
    [AvaloniaFact]
    public async Task Con_un_mapa_delante_se_hereda_su_juego_de_tiles()
    {
        string path = Path.Combine(_folder, "mapa.csv");
        await File.WriteAllTextAsync(path, "1,2\n");

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        main.OpenTileSet(new TileSet("Bosque"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        NewMap(main, "El de ciudad", tileSet: ciudad);

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        MapEditorViewModel imported = main.Tabs.OfType<MapEditorViewModel>().Last();

        Assert.Equal("Ciudad", imported.Map.TileSetName);
        Assert.Same(ciudad.Thumbnails, imported.Tiles);
    }

    [AvaloniaFact]
    public async Task Un_csv_estropeado_se_rechaza_diciendo_donde()
    {
        string path = Path.Combine(_folder, "malo.csv");
        await File.WriteAllTextAsync(path, "1,2\n3,x\n");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Contains("fila 1", dialogs.Messages[0]);
    }

    private static string MapSerializerText(string name, string tileSet) =>
        MSX_GameTools.Services.MapSerializer.Serialize(
            new TileMap(name, 8, 8) { TileSetName = tileSet });

    private static MapEditorViewModel NewMap(
        MainWindowViewModel main,
        string name,
        int columns = 32,
        int rows = 24,
        TileSetEditorViewModel? tileSet = null)
    {
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = name;
        form.Columns = columns;
        form.Rows = rows;

        // El formulario propone el primer juego abierto; aqui puede interesar otro.
        if (tileSet is not null)
            form.TileSet = tileSet;

        form.AcceptMapCommand.Execute(null);

        return main.Tabs.OfType<MapEditorViewModel>().Last();
    }
}
