using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
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

        Assert.False(main.SaveDocumentCommand.CanExecute(null));
        Assert.False(main.ExportMapCommand.CanExecute(null));

        main.OpenTileSet(new TileSet("Bosque"));

        // Guardar vale para cualquier documento, asi que con el tileset delante guarda
        // el tileset; lo que es del mapa y solo del mapa es exportarlo.
        Assert.True(main.SaveDocumentCommand.CanExecute(null));
        Assert.False(main.ExportMapCommand.CanExecute(null));

        NewMap(main, "Nivel 1");

        Assert.True(main.SaveDocumentCommand.CanExecute(null));
        Assert.True(main.ExportMapCommand.CanExecute(null));
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

        await main.SaveDocumentCommand.ExecuteAsync(null);

        Assert.True(File.Exists(path));

        await main.OpenCommand.ExecuteAsync(null);

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

        await main.OpenCommand.ExecuteAsync(null);

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

        await main.OpenCommand.ExecuteAsync(null);

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

        await main.OpenCommand.ExecuteAsync(null);

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

        await main.SaveDocumentCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(dialogs.Messages);
    }

    // ------------------------------------------------------------------ csv

    [AvaloniaFact]
    public async Task Exportar_a_csv_escribe_el_mapa_aplastado()
    {
        string path = Path.Combine(_folder, "nivel.csv");
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = NewMap(main, "Nivel 1", 3, 2);
        editor.PickTile(TilePatch.Single(5), "Tile 5");
        editor.Paint(1, 0);

        await TestExport.MapAsync(main, ExportFormat.Csv, path);

        Assert.Equal("-1,5,-1\n-1,-1,-1\n", await File.ReadAllTextAsync(path));
    }

    [AvaloniaFact]
    public void Sin_mapa_delante_no_se_puede_exportar_a_csv()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));

        Assert.False(main.ExportMapCommand.CanExecute(null));
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
    /// Un csv no dice de qué juego son sus números. Con varios en el proyecto y delante
    /// algo que no es ni un juego ni un mapa, no hay forma de adivinarlo.
    /// </summary>
    [AvaloniaFact]
    public async Task Con_varios_juegos_y_nada_que_lo_diga_no_se_adivina()
    {
        string path = Path.Combine(_folder, "mapa.csv");
        await File.WriteAllTextAsync(path, "1,2\n");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));
        main.OpenTileSet(new TileSet("Ciudad"));

        // Delante un banco de sprites, que no dice nada de tiles.
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Single(dialogs.Messages);
    }

    /// <summary>
    /// Tener el juego delante es la señal más explícita que puede dar el usuario, aunque
    /// haya varios en el proyecto. Sin esto salía «no se sabe con qué tiles dibujarlo»
    /// teniendo el juego justo delante, que es de las cosas que más desconciertan.
    /// </summary>
    [AvaloniaFact]
    public async Task Con_el_juego_de_tiles_delante_se_usa_ese()
    {
        string path = Path.Combine(_folder, "mapa.csv");
        await File.WriteAllTextAsync(path, "1,2\n");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));
        main.OpenTileSet(new TileSet("Ciudad"));
        TileSetEditorViewModel tiles3 = main.OpenTileSet(new TileSet("Tiles3"));

        Assert.Same(tiles3, main.SelectedTab);

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        MapEditorViewModel imported = Assert.Single(main.Tabs.OfType<MapEditorViewModel>());

        Assert.Equal("Tiles3", imported.Map.TileSetName);
        Assert.Empty(dialogs.Messages);
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

    // ------------------------------------------------------------------ binario y asm

    [AvaloniaFact]
    public async Task Exportar_a_binario_escribe_la_cabecera_y_la_tabla()
    {
        string path = Path.Combine(_folder, "nivel.bin");
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = NewMap(main, "Nivel 1", 3, 2);
        editor.PickTile(TilePatch.Single(9), "Tile 9");
        editor.Paint(0, 0);

        await TestExport.MapAsync(main, ExportFormat.Binary, path);

        byte[] bytes = await File.ReadAllBytesAsync(path);

        Assert.Equal(MapExporter.HeaderBytes + 6, bytes.Length);
        Assert.Equal([3, 0, 2, 0], bytes[..4]);
        Assert.Equal(9, bytes[4]);
    }

    /// <summary>
    /// El aviso sale sólo si hay huecos: en un byte no cabe el vacío y salen con el tile
    /// de relleno, y eso hay que decirlo en vez de escribirlo en silencio.
    /// </summary>
    [AvaloniaFact]
    public async Task Al_exportar_con_huecos_se_avisa_del_tile_de_relleno()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));
        NewMap(main, "Nivel 1", 2, 1);

        await TestExport.MapAsync(
            main, ExportFormat.Binary, Path.Combine(_folder, "nivel.bin"));

        Assert.Single(dialogs.Messages);
        Assert.Contains("celdas vacías", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Sin_huecos_no_se_avisa_de_nada()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = NewMap(main, "Nivel 1", 2, 1);
        editor.Select(0, 0, 1, 0);
        editor.FillSelectionCommand.Execute(null);

        await TestExport.MapAsync(
            main, ExportFormat.Binary, Path.Combine(_folder, "nivel.bin"));

        Assert.Empty(dialogs.Messages);
    }

    /// <summary>
    /// Que el asm traiga los mismos bytes que el binario se comprueba en MapExporterTests,
    /// y ademas ensamblandolo de verdad. Aqui solo que el comando escribe el fichero.
    /// </summary>
    [AvaloniaFact]
    public async Task Exportar_a_asm_escribe_el_fichero_con_su_etiqueta()
    {
        string path = Path.Combine(_folder, "nivel.asm");
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));
        NewMap(main, "Nivel 1", 4, 3);

        await TestExport.MapAsync(main, ExportFormat.Assembler, path);

        string asm = await File.ReadAllTextAsync(path);

        Assert.Contains("nivel_1_map:", asm);
        Assert.Contains("nivel_1_map_end:", asm);
    }

    [AvaloniaFact]
    public async Task Importar_un_binario_abre_un_mapa_nuevo()
    {
        string path = Path.Combine(_folder, "desde_rom.bin");
        await File.WriteAllBytesAsync(path, [3, 0, 2, 0, 1, 2, 3, 4, 5, 6]);

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        main.OpenTileSet(new TileSet("Bosque"));

        await main.ImportMapBinaryCommand.ExecuteAsync(null);

        MapEditorViewModel editor = Assert.Single(main.Tabs.OfType<MapEditorViewModel>());

        Assert.Equal("desde_rom", editor.Map.Name);
        Assert.Equal((3, 2), (editor.Map.Width, editor.Map.Height));
        Assert.Equal(6, editor.Map.Layers[0].Grid[2, 1]);
    }

    [AvaloniaFact]
    public async Task Un_binario_que_no_es_un_mapa_se_rechaza()
    {
        string path = Path.Combine(_folder, "cualquiera.bin");
        await File.WriteAllBytesAsync(path, [0xFF, 0xFF, 0xFF, 0xFF, 1, 2]);

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque"));

        await main.ImportMapBinaryCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs.OfType<MapEditorViewModel>());
        Assert.Single(dialogs.Messages);
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
