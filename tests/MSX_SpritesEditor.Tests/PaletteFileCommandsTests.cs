using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.Services;
using MSX_SpritesEditor.ViewModels;
using Xunit;
using Avalonia.Headless.XUnit;

namespace MSX_SpritesEditor.Tests;

/// <summary>Cargar y guardar paletas, incluida la escritura real en disco.</summary>
public class PaletteFileCommandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxpal-{Guid.NewGuid():N}");

    public PaletteFileCommandsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public async Task Guardar_escribe_la_paleta_activa_en_el_fichero_elegido()
    {
        string path = Path.Combine(_folder, "nocturna.json");
        var dialogs = new TestDialogService { SavePath = path };
        var main = new MainWindowViewModel(dialogs);

        main.AddPaletteCommand.Execute(null);
        main.Palettes.ActivePalette.Name = "Nocturna";
        main.Palettes.ActivePalette[3].SetComponents(7, 0, 5);

        await main.SavePaletteCommand.ExecuteAsync(null);

        Assert.True(File.Exists(path));
        ColorPalette saved = PaletteSerializer.Deserialize(await File.ReadAllTextAsync(path));
        Assert.Equal("Nocturna", saved.Name);
        Assert.Equal("705", saved[3].HexRgb);
    }

    [AvaloniaFact]
    public async Task Guardar_sugiere_un_nombre_de_fichero_a_partir_del_de_la_paleta()
    {
        var dialogs = new TestDialogService { SavePath = null };
        var main = new MainWindowViewModel(dialogs);

        main.AddPaletteCommand.Execute(null);
        main.Palettes.ActivePalette.Name = "Cueva: nivel 3/4";

        await main.SavePaletteCommand.ExecuteAsync(null);

        // Sin caracteres prohibidos en un nombre de fichero.
        Assert.Equal("Cueva nivel 34.json", dialogs.LastSuggestedFileName);
    }

    [AvaloniaFact]
    public async Task Cancelar_el_selector_no_escribe_nada()
    {
        var dialogs = new TestDialogService { SavePath = null };
        var main = new MainWindowViewModel(dialogs);

        await main.SavePaletteCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(dialogs.Messages);
    }

    [AvaloniaFact]
    public async Task Cargar_anade_la_paleta_del_fichero_y_la_deja_activa()
    {
        string path = Path.Combine(_folder, "nocturna.json");
        ColorPalette source = new PaletteLibrary().Add("Nocturna");
        source[3].SetComponents(7, 0, 5);
        await File.WriteAllTextAsync(path, PaletteSerializer.Serialize(source));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadPaletteCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Palettes.Palettes.Count);
        Assert.Equal("Nocturna", main.Palettes.ActivePalette.Name);
        Assert.Equal("705", main.Palettes.ActivePalette[3].HexRgb);
        Assert.False(main.Palettes.ActivePalette.IsReadOnly);
    }

    [AvaloniaFact]
    public async Task Cargar_dos_veces_el_mismo_fichero_no_repite_el_nombre()
    {
        string path = Path.Combine(_folder, "nocturna.json");
        await File.WriteAllTextAsync(path, PaletteSerializer.Serialize(new PaletteLibrary().Add("Nocturna")));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadPaletteCommand.ExecuteAsync(null);
        await main.LoadPaletteCommand.ExecuteAsync(null);

        Assert.Equal(["MSX", "Nocturna", "Nocturna (2)"], main.Palettes.Palettes.Select(p => p.Name));
    }

    [AvaloniaFact]
    public async Task Un_fichero_invalido_avisa_y_no_toca_la_biblioteca()
    {
        string path = Path.Combine(_folder, "roto.json");
        await File.WriteAllTextAsync(path, "{ esto no es una paleta }");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadPaletteCommand.ExecuteAsync(null);

        Assert.Single(main.Palettes.Palettes);
        Assert.Single(dialogs.Messages);
        Assert.Contains("JSON válido", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Un_fichero_que_no_existe_avisa_en_vez_de_reventar()
    {
        var dialogs = new TestDialogService { OpenPath = Path.Combine(_folder, "no-existe.json") };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadPaletteCommand.ExecuteAsync(null);

        Assert.Single(main.Palettes.Palettes);
        Assert.Single(dialogs.Messages);
    }

}
