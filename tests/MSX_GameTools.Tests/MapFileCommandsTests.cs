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

    private static string MapSerializerText(string name, string tileSet) =>
        MSX_GameTools.Services.MapSerializer.Serialize(
            new TileMap(name, 8, 8) { TileSetName = tileSet });

    private static MapEditorViewModel NewMap(
        MainWindowViewModel main, string name, int columns = 32, int rows = 24)
    {
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = name;
        form.Columns = columns;
        form.Rows = rows;
        form.AcceptMapCommand.Execute(null);

        return main.Tabs.OfType<MapEditorViewModel>().Last();
    }
}
