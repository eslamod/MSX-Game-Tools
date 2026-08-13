using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Un mapa señala su juego de tiles por identidad, no por el nombre.
/// </summary>
/// <remarks>
/// El nombre ataba las dos cosas: renombrar un juego dejaba a sus mapas sin encontrarlo
/// —al abrir el proyecto salían como que les faltaba el juego, y guardar entonces los
/// sacaba del índice— y dos juegos llamados igual hacían la búsqueda ambigua. El nombre
/// se sigue escribiendo en el fichero del mapa, pero sólo para poder leerlo y para los
/// mapas guardados antes de que la identidad existiera.
/// </remarks>
public class TileSetIdentityTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxid-{Guid.NewGuid():N}");

    public TileSetIdentityTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Cada_juego_nace_con_una_identidad_propia()
    {
        var one = new TileSet("Bosque");
        var other = new TileSet("Bosque");

        Assert.NotEqual(Guid.Empty, one.Id);
        Assert.NotEqual(one.Id, other.Id);
    }

    [AvaloniaFact]
    public void El_mapa_guarda_con_que_juego_se_dibuja()
    {
        var tiles = new TileSet("Bosque");
        var map = new TileMap("Nivel 1", 4, 2) { TileSetId = tiles.Id, TileSetName = tiles.Name };

        TileMap read = MapSerializer.Deserialize(MapSerializer.Serialize(map));

        Assert.Equal(tiles.Id, read.TileSetId);
        Assert.Equal("Bosque", read.TileSetName);
    }

    /// <summary>Que es de lo que iba todo esto.</summary>
    [AvaloniaFact]
    public async Task Renombrar_el_juego_no_deja_al_mapa_sin_encontrarlo()
    {
        string mapPath = Path.Combine(_folder, "nivel.json");
        var dialogs = new TestDialogService { SavePath = mapPath, OpenPath = mapPath };
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel map = main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);

        await main.SaveDocumentCommand.ExecuteAsync(null);

        // Lo que hasta ahora rompía el enlace. Cuando el panel de propiedades exista,
        // renombrar pasará por él; lo que se comprueba aquí es que el enlace aguanta.
        tiles.TileSet.Name = "Bosque de noche";

        await main.LoadMapCommand.ExecuteAsync(null);

        MapEditorViewModel reopened = main.Tabs.OfType<MapEditorViewModel>().Last();

        Assert.NotSame(map, reopened);
        Assert.Empty(dialogs.Messages);
        Assert.Same(tiles, main.TileSetOf(reopened.Map));
    }

    /// <summary>Con dos llamados igual, el mapa sabe cuál de los dos es el suyo.</summary>
    [AvaloniaFact]
    public void Dos_juegos_con_el_mismo_nombre_no_confunden_al_mapa()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));

        TileSetEditorViewModel second = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel map = main.OpenMap(new TileMap("Nivel 1", 8, 8), second);

        Assert.Same(second, main.TileSetOf(map.Map));
    }

    // ------------------------------------------------------ ficheros de antes

    /// <summary>
    /// Un mapa de la versión 1 no trae identidad, así que se busca por el nombre, igual
    /// que hacía entonces.
    /// </summary>
    [AvaloniaFact]
    public async Task Un_mapa_sin_identidad_se_sigue_abriendo_por_el_nombre()
    {
        string path = Path.Combine(_folder, "viejo.json");

        await File.WriteAllTextAsync(path, """
            {
              "version": 1,
              "name": "Nivel viejo",
              "width": 4,
              "height": 2,
              "backgroundColor": 1,
              "tileSet": "Bosque",
              "emptyTile": 0,
              "layers": [ { "name": "Capa 1", "visible": true, "locked": false, "rows": [] } ]
            }
            """);

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        await main.LoadMapCommand.ExecuteAsync(null);

        MapEditorViewModel loaded = main.Tabs.OfType<MapEditorViewModel>().Single();

        Assert.Empty(dialogs.Messages);
        Assert.Equal("Nivel viejo", loaded.Map.Name);

        // Y al abrirlo se le pega la identidad del juego, así que deja de depender del
        // nombre en cuanto se vuelva a guardar.
        Assert.Equal(tiles.TileSet.Id, loaded.Map.TileSetId);
    }

    /// <summary>Un juego de la versión 2 no trae identidad y se le hace una al abrirlo.</summary>
    [AvaloniaFact]
    public void Un_juego_sin_identidad_estrena_una_al_abrirlo()
    {
        string json = TileSetSerializer.Serialize(new TileSet("Bosque"), new PaletteLibrary().ActivePalette);

        // Se le quita la identidad, que es lo que distingue a un fichero de la 2. Sin
        // comprobar que de verdad se ha ido, esta prueba pasaría con el fichero entero.
        string old = string.Join(
            Environment.NewLine,
            json.Split(Environment.NewLine)
                .Where(line => !line.TrimStart().StartsWith("\"id\"", StringComparison.Ordinal))
                .Select(line => line.Replace(
                    $"\"version\": {TileSetSerializer.FormatVersion}", "\"version\": 2", StringComparison.Ordinal)));

        Assert.DoesNotContain("\"id\"", old, StringComparison.Ordinal);
        Assert.Contains("\"version\": 2", old, StringComparison.Ordinal);

        LoadedTileSet loaded = TileSetSerializer.Deserialize(old);

        Assert.NotEqual(Guid.Empty, loaded.TileSet.Id);
    }
}
