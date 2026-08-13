using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Un solo Abrir para los cuatro tipos de fichero, y empezar un proyecto de cero.
/// </summary>
/// <remarks>
/// Antes había cuatro entradas de menú y había que acertar con la del fichero que traías;
/// equivocarse decía que no era válido aunque estuviera perfecto. Los cuatro formatos se
/// distinguen por una propiedad de primer nivel, así que el programa puede mirarlo.
/// </remarks>
public class OpenAnyFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxopen-{Guid.NewGuid():N}");

    public OpenAnyFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // ------------------------------------------------------------------ reconocerlo

    [AvaloniaFact]
    public void Cada_formato_se_reconoce_por_lo_que_lleva_dentro()
    {
        ColorPalette palette = new PaletteLibrary().ActivePalette;

        Assert.Equal(
            EditorFileKind.SpriteBank,
            EditorFile.KindOf(SpriteBankSerializer.Serialize(new SpriteBank(name: "B"), palette, 1)));

        Assert.Equal(
            EditorFileKind.TileSet,
            EditorFile.KindOf(TileSetSerializer.Serialize(new TileSet("T"), palette)));

        Assert.Equal(
            EditorFileKind.Map,
            EditorFile.KindOf(MapSerializer.Serialize(new TileMap("M", 4, 4))));

        Assert.Equal(
            EditorFileKind.Palette,
            EditorFile.KindOf(PaletteSerializer.Serialize(palette)));
    }

    [AvaloniaTheory]
    [InlineData("esto no es json")]
    [InlineData("{ }")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "version": 1, "algo": "otra cosa" }""")]
    public void Lo_que_no_es_nuestro_no_se_reconoce(string content) =>
        Assert.Null(EditorFile.KindOf(content));

    /// <summary>
    /// Un mapa lleva filas que se llaman «tiles», igual que un juego de tiles, pero
    /// colgando de sus capas. Sólo cuenta lo de primer nivel.
    /// </summary>
    [AvaloniaFact]
    public void Un_mapa_con_tiles_dentro_sigue_siendo_un_mapa()
    {
        var map = new TileMap("Nivel", 4, 2);

        map.Stamp(0, 1, 1, TilePatch.Single(9));

        string json = MapSerializer.Serialize(map);

        Assert.Contains("\"tiles\"", json, StringComparison.Ordinal);
        Assert.Equal(EditorFileKind.Map, EditorFile.KindOf(json));
    }

    // ------------------------------------------------------------------ abrirlo

    [AvaloniaFact]
    public async Task Abrir_un_juego_de_tiles_lo_abre()
    {
        string path = Write("bosque.json", TileSetSerializer.Serialize(
            new TileSet("Bosque"), new PaletteLibrary().ActivePalette));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Bosque (TS)", main.Tabs.OfType<TileSetEditorViewModel>().Single().Header);
    }

    [AvaloniaFact]
    public async Task Abrir_un_banco_lo_abre()
    {
        string path = Write("bichos.json", SpriteBankSerializer.Serialize(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"), new PaletteLibrary().ActivePalette, 1));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Bichos (SP)", main.Tabs.OfType<SpritesEditorViewModel>().Single().Header);
    }

    [AvaloniaFact]
    public async Task Abrir_una_paleta_la_mete_en_la_biblioteca()
    {
        ColorPalette palette = new PaletteLibrary().Add("Nocturna");

        string path = Write("nocturna.json", PaletteSerializer.Serialize(palette));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Contains(main.Palettes.Palettes, candidate => candidate.Name == "Nocturna");
        Assert.Empty(main.Tabs);
    }

    [AvaloniaFact]
    public async Task Abrir_un_mapa_lo_engancha_a_su_juego()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        var map = new TileMap("Nivel 1", 8, 8) { TileSetId = tiles.TileSet.Id, TileSetName = "Bosque" };

        dialogs.OpenPath = Write("nivel.json", MapSerializer.Serialize(map));

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Same(tiles, main.TileSetOf(main.Tabs.OfType<MapEditorViewModel>().Single().Map));
        Assert.Empty(dialogs.Messages);
    }

    // ------------------------------------------------------------------ proyecto nuevo

    [AvaloniaFact]
    public async Task Empezar_de_cero_deja_el_editor_limpio()
    {
        var dialogs = new TestDialogService { SavePath = Path.Combine(_folder, "juego.msxproj"), ChooseAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque")).MarkClean();
        main.Palettes.Import(new PaletteLibrary().Add("Nocturna"));

        await main.SaveProjectCommand.ExecuteAsync(null);
        await main.NewProjectCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs);
        Assert.Null(main.ProjectPath);
        Assert.Equal("Sin proyecto", main.ProjectName);
        Assert.Empty(main.TreeGeneralVm.PrimaryNodes.SelectMany(node => node.Childs));

        // Las paletas del proyecto anterior tampoco se quedan: se meterían en el siguiente.
        Assert.Single(main.Palettes.Palettes);
    }

    [AvaloniaFact]
    public async Task Empezar_de_cero_avisa_de_lo_que_esta_sin_guardar()
    {
        var dialogs = new TestDialogService { ChooseAnswer = null };
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        tiles.MarkClean();
        tiles.PixelSurface.Set(1, 1, true);

        await main.NewProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ChooseCalls);
        Assert.Single(main.Tabs);
    }

    private string Write(string name, string content)
    {
        string path = Path.Combine(_folder, name);

        File.WriteAllText(path, content);

        return path;
    }
}
