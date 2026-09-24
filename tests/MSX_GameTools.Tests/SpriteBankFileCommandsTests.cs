using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Guardar y cargar bancos desde el menú, con escritura real en disco.</summary>
public class SpriteBankFileCommandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxbank-{Guid.NewGuid():N}");

    public SpriteBankFileCommandsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Guardar_esta_deshabilitado_sin_un_banco_abierto()
    {
        var main = new MainWindowViewModel();

        Assert.False(main.SaveDocumentCommand.CanExecute(null));
        Assert.False(main.ExportSpriteBankCommand.CanExecute(null));

        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Uno"));

        Assert.True(main.SaveDocumentCommand.CanExecute(null));
        Assert.True(main.ExportSpriteBankCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Guardar_y_cargar_devuelve_el_mismo_banco()
    {
        string path = Path.Combine(_folder, "bicho.json");
        var dialogs = new TestDialogService { SavePath = path, OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        SpritesEditorViewModel editor = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));
        editor.NextSpriteCommand.Execute(null);
        editor.CurrentSprite.ArraySpriteRows[4].ArrayColumns[9] = true;
        editor.AddGroupCommand.Execute(null);
        editor.SelectedGroup!.NudgeOffsetCommand.Execute("right");

        await main.SaveDocumentCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path));

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Tabs.Count);
        var loaded = (SpritesEditorViewModel)main.Tabs[1];

        Assert.Equal("Bicho", loaded.SpritesBank.Name);
        Assert.Equal("Bicho (SP)", loaded.Header);
        // Lo que dice que ha vuelto entero es el pixel donde se pinto, no la cuenta de
        // patrones: esos son siempre 64.
        Assert.True(loaded.SpritesBank.SpritesList[1].ArraySpriteRows[4].ArrayColumns[9]);
        Assert.True(loaded.SpritesBank.SpritesList[0].IsEmpty);
        Assert.Single(loaded.SpritesBank.Groups);
        Assert.Equal(1, loaded.SpritesBank.Groups[0].Members[0].OffsetX);
    }

    [AvaloniaFact]
    public async Task Cargar_lo_deja_como_pestana_activa_y_en_el_arbol()
    {
        string path = await WriteBankAsync("bicho.json", "Bicho");
        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Single(main.Tabs);
        Assert.Same(main.Tabs[0], main.SelectedTab);

        ItemTree spriteBanks = main.TreeGeneralVm.PrimaryNodes[0];
        Assert.Single(spriteBanks.Childs);
        Assert.Equal("Bicho (SP)", spriteBanks.Childs[0].DisplayText);
    }

    [AvaloniaFact]
    public async Task Cargar_reutiliza_la_paleta_si_ya_esta_en_la_biblioteca()
    {
        string path = await WriteBankAsync("bicho.json", "Bicho");
        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        // El banco se guardo con la paleta MSX estandar, que ya esta.
        await main.OpenCommand.ExecuteAsync(null);
        await main.OpenCommand.ExecuteAsync(null);

        Assert.Single(main.Palettes.Palettes);
        Assert.Equal(ColorPalette.StandardName, main.Palettes.ActivePalette.Name);
    }

    [AvaloniaFact]
    public async Task Cargar_anade_y_activa_la_paleta_si_no_la_tiene()
    {
        var source = new PaletteLibrary();
        ColorPalette palette = source.Add("Nocturna");
        palette[3].SetComponents(7, 0, 5);

        string path = Path.Combine(_folder, "bicho.json");
        await File.WriteAllTextAsync(path, Services.SpriteBankSerializer.Serialize(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), palette, 1));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Palettes.Palettes.Count);
        Assert.Equal("Nocturna", main.Palettes.ActivePalette.Name);
        Assert.Equal("705", main.Palettes.ActivePalette[3].HexRgb);
    }

    [AvaloniaFact]
    public async Task El_color_de_fondo_guardado_se_recupera()
    {
        string path = Path.Combine(_folder, "bicho.json");
        await File.WriteAllTextAsync(path, Services.SpriteBankSerializer.Serialize(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard(), backgroundColorIndex: 7));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal(7, ((SpritesEditorViewModel)main.Tabs[0]).BackgroundColorIndex);
    }

    [AvaloniaFact]
    public async Task Un_fichero_invalido_avisa_y_no_abre_pestana()
    {
        string path = Path.Combine(_folder, "roto.json");
        await File.WriteAllTextAsync(path, "{ esto no es un banco }");

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Empty(main.Tabs);
        Assert.Single(dialogs.Messages);
        Assert.Contains("no parece", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Cancelar_el_selector_no_escribe_nada()
    {
        var dialogs = new TestDialogService { SavePath = null };
        var main = new MainWindowViewModel(dialogs);
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await main.SaveDocumentCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(dialogs.Messages);
        Assert.Equal("Bicho.json", dialogs.LastSuggestedFileName);
    }

    [AvaloniaFact]
    public async Task Exportar_a_binario_escribe_los_dos_ficheros()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await TestExport.SpriteBankAsync(
            main, ExportFormat.Binary, Path.Combine(_folder, "bicho.bin"));

        Assert.True(File.Exists(Path.Combine(_folder, "bicho_patterns.bin")));
        Assert.True(File.Exists(Path.Combine(_folder, "bicho_groups.bin")));

        // Un patron de 16x16 son 32 bytes.
        Assert.Equal(32, new FileInfo(Path.Combine(_folder, "bicho_patterns.bin")).Length);

        // Y se dice que ficheros han salido, porque el nombre elegido se reparte en dos.
        Assert.Single(dialogs.Messages);
        Assert.Contains("bicho_patterns.bin", dialogs.Messages[0]);
        Assert.Contains("bicho_groups.bin", dialogs.Messages[0]);
    }

    [AvaloniaFact]
    public async Task Exportar_a_ensamblador_escribe_texto_con_etiquetas()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await TestExport.SpriteBankAsync(
            main, ExportFormat.Assembler, Path.Combine(_folder, "bicho.asm"));

        string patterns = await File.ReadAllTextAsync(Path.Combine(_folder, "bicho_patterns.asm"));

        Assert.Contains("bicho_patterns:", patterns);
        Assert.Contains(
            $"{AsmStyle.Default.Data}  {Services.AsmHex.Prefix}00",
            patterns);
    }

    [AvaloniaFact]
    public void Exportar_esta_deshabilitado_sin_un_banco_abierto()
    {
        var main = new MainWindowViewModel();

        Assert.False(main.ExportSpriteBankCommand.CanExecute(null));

        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Uno"));

        Assert.True(main.ExportSpriteBankCommand.CanExecute(null));
    }

    /// <summary>Cancelar cierra el panel sin escribir, aunque el destino esté puesto.</summary>
    [AvaloniaFact]
    public void Cancelar_la_exportacion_no_escribe_nada()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        main.ExportSpriteBankCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        form.Destination = Path.Combine(_folder, "bicho.bin");
        form.CancelExportCommand.Execute(null);

        Assert.Null(main.RightPanViewModel);
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(dialogs.Messages);
    }

    private async Task<string> WriteBankAsync(string fileName, string bankName)
    {
        string path = Path.Combine(_folder, fileName);

        await File.WriteAllTextAsync(path, Services.SpriteBankSerializer.Serialize(
            new SpriteBank(SpriteBank.SpriteType.MSX2, bankName), ColorPalette.CreateMsxStandard(), 1));

        return path;
    }
}
