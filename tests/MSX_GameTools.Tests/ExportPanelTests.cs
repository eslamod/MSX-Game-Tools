using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El panel de exportar: qué formatos ofrece, qué ficheros dice que va a escribir y cuáles
/// escribe.
/// </summary>
/// <remarks>
/// Lo que justifica el panel es la lista: de un juego de tiles salen hasta cuatro ficheros y el
/// selector del sistema sólo avisa del que se nombra, así que los otros tres se sobrescribían
/// sin decir nada.
/// </remarks>
public class ExportPanelTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"msxexport-{Guid.NewGuid():N}");

    public ExportPanelTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>De un juego con supertiles y atributos salen cuatro ficheros.</summary>
    [AvaloniaFact]
    public void El_panel_dice_los_cuatro_ficheros_que_va_a_escribir()
    {
        ExportViewModel form = Panel(Rich());

        Assert.Equal(
            (string[])["bosque_patterns.asm", "bosque_colors.asm", "bosque_supertiles.asm", "bosque_attributes.asm"],
            form.Files.Select(file => file.Name));
    }

    /// <summary>Y de uno normal, los dos de siempre.</summary>
    [AvaloniaFact]
    public void De_un_juego_normal_salen_los_dos_de_siempre()
    {
        ExportViewModel form = Panel(new TileSet("Bosque"));

        Assert.Equal(
            (string[])["bosque_patterns.asm", "bosque_colors.asm"],
            form.Files.Select(file => file.Name));
    }

    /// <summary>El formato cambia las extensiones, y el png sale de una pieza.</summary>
    [AvaloniaFact]
    public void El_formato_cambia_lo_que_va_a_salir()
    {
        ExportViewModel form = Panel(new TileSet("Bosque"));

        Use(form, ExportFormat.Binary);

        Assert.Equal(
            (string[])["bosque_patterns.bin", "bosque_colors.bin"],
            form.Files.Select(file => file.Name));

        Use(form, ExportFormat.Png);

        Assert.Equal((string[])["bosque.png"], form.Files.Select(file => file.Name));
    }

    /// <summary>Los nombres salen del destino elegido en cuanto lo hay.</summary>
    [AvaloniaFact]
    public void Los_nombres_salen_del_destino_elegido()
    {
        ExportViewModel form = Panel(new TileSet("Bosque"));

        form.Destination = Path.Combine(_folder, "nivel1.asm");

        Assert.Equal(
            (string[])["nivel1_patterns.asm", "nivel1_colors.asm"],
            form.Files.Select(file => file.Name));
    }

    /// <summary>
    /// Los que ya están se marcan, y se dice cuántos son.
    /// </summary>
    /// <remarks>
    /// Es lo que el selector del sistema no puede decir: pregunta por el fichero que se nombra
    /// y de aquí salen cuatro, así que los otros tres se pisaban en silencio.
    /// </remarks>
    [AvaloniaFact]
    public void Los_ficheros_que_ya_estan_se_marcan()
    {
        ExportViewModel form = Panel(new TileSet("Bosque"));

        File.WriteAllText(Path.Combine(_folder, "bosque_colors.asm"), "lo que hubiera");

        form.Destination = Path.Combine(_folder, "bosque.asm");

        Assert.False(form.Files[0].Exists);
        Assert.True(form.Files[1].Exists);

        Assert.True(form.HasOverwrites);
        Assert.Contains("1", form.Overwrites!);
    }

    /// <summary>Sin destino no se escribe nada, y el panel se queda abierto para ponerlo.</summary>
    [AvaloniaFact]
    public async Task Sin_destino_no_escribe_nada()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));
        main.ExportTileSetCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        await form.AcceptExportCommand.ExecuteAsync(null);

        Assert.True(form.HasError);
        Assert.Same(form, main.RightPanViewModel);

        Assert.Empty(Directory.GetFiles(_folder));
    }

    /// <summary>Y al aceptar salen todos, no sólo el que se nombró.</summary>
    [AvaloniaFact]
    public async Task Aceptar_escribe_todos_los_ficheros()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(Rich());

        await TestExport.TileSetAsync(main, ExportFormat.Binary, Path.Combine(_folder, "bosque.bin"));

        Assert.Equal(
            (string[])["bosque_attributes.bin", "bosque_colors.bin", "bosque_patterns.bin", "bosque_supertiles.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());

        // Y el panel se cierra, que ya ha hecho lo suyo.
        Assert.Null(main.RightPanViewModel);
    }

    /// <summary>De un banco salen los dos de siempre.</summary>
    [AvaloniaFact]
    public void De_un_banco_salen_los_patrones_y_los_grupos()
    {
        Assert.Equal(
            (string[])["bicho_patterns.asm", "bicho_groups.asm"],
            BankPanel(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"))
                .Files.Select(file => file.Name));
    }

    /// <summary>Y tres si hay animaciones, que es el mismo problema del selector.</summary>
    [AvaloniaFact]
    public void De_un_banco_con_animaciones_sale_tambien_el_de_animaciones()
    {
        Assert.Equal(
            (string[])["bicho_patterns.asm", "bicho_groups.asm", "bicho_animations.asm"],
            BankPanel(Animated()).Files.Select(file => file.Name));
    }

    /// <summary>
    /// Las animaciones que piden grupos borrados se avisan antes de escribir nada.
    /// </summary>
    /// <remarks>
    /// Se pregunta antes de tocar el disco: decir que no tiene que dejar la carpeta como
    /// estaba, y el panel abierto para poder arreglarlo.
    /// </remarks>
    [AvaloniaFact]
    public async Task Las_animaciones_rotas_se_avisan_antes_de_escribir()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = false });

        main.OpenSpriteBank(Animated());

        await TestExport.SpriteBankAsync(
            main, ExportFormat.Binary, Path.Combine(_folder, "bicho.bin"));

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.NotNull(main.RightPanViewModel);
    }

    /// <summary>
    /// Un banco de MSX1 no ofrece ROM de ejemplo.
    /// </summary>
    /// <remarks>
    /// La ROM pone GRAPHIC 3 con sprites de modo 2, que es para lo que están los 16 bytes de
    /// color por sprite de un banco de MSX2. Uno de MSX1 sería otro programa, así que antes
    /// que darle una ROM que lo enseña mal, la casilla no sale.
    /// </remarks>
    [AvaloniaFact]
    public void Un_banco_de_msx1_no_ofrece_rom_de_ejemplo()
    {
        Assert.False(BankPanel(new SpriteBank(SpriteBank.SpriteType.MSX, "Bicho")).ShowsExampleRom);
        Assert.True(BankPanel(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho")).ShowsExampleRom);
    }

    /// <summary>Con la casilla marcada salen también la ROM y el reproductor.</summary>
    [AvaloniaFact]
    public void La_rom_del_banco_se_trae_el_reproductor()
    {
        ExportViewModel form = BankPanel(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        form.WantsExampleRom = true;

        Assert.Equal(
            (string[])["bicho_patterns.asm", "bicho_groups.asm", "bicho_rom.asm", "bicho_player.asm"],
            form.Files.Select(file => file.Name));
    }

    /// <summary>
    /// El mapa ofrece el csv en la misma lista que el binario y el ensamblador.
    /// </summary>
    /// <remarks>
    /// Era una tercera entrada de menú contestando lo que contestaban las otras dos con otro
    /// formato. Y el aviso de las celdas vacías no sale con csv, que sí sabe decir vacío.
    /// </remarks>
    [AvaloniaFact]
    public async Task El_mapa_ofrece_el_csv_como_un_formato_mas()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        main.OpenMap(new TileMap("Nivel 1", 2, 1), tiles);
        main.ExportMapCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        Assert.Equal(
            (ExportFormat[])[ExportFormat.Assembler, ExportFormat.Binary, ExportFormat.Csv],
            form.Formats.Select(choice => choice.Format));

        Use(form, ExportFormat.Csv);

        Assert.Equal((string[])["nivel_1.csv"], form.Files.Select(file => file.Name));

        form.Destination = Path.Combine(_folder, "nivel_1.csv");

        await form.AcceptExportCommand.ExecuteAsync(null);

        // El mapa está vacío entero y aun así no se avisa: el csv guarda los huecos.
        Assert.Empty(dialogs.Messages);
    }

    /// <summary>
    /// Un mapa de supertiles ofrece ROM igual, aunque sea otro programa.
    /// </summary>
    /// <remarks>
    /// Desde el panel es la misma pregunta —la ROM de este mapa—; qué plantilla sale se
    /// decide más adentro, mirando si el juego de tiles tiene supertiles.
    /// </remarks>
    [AvaloniaFact]
    public void Un_mapa_de_supertiles_ofrece_rom_de_ejemplo()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2, SuperTileHeight = 2 };

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet);

        main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);
        main.ExportMapCommand.Execute(null);

        Assert.True(((ExportViewModel)main.RightPanViewModel!).ShowsExampleRom);
    }

    /// <summary>
    /// Con csv no sale la casilla de la ROM, que no sabría qué traerse.
    /// </summary>
    /// <remarks>
    /// La ROM se trae lo exportado con un incbin o un include, y un csv no es ninguna de las
    /// dos cosas. Marcarla y cambiar luego de formato tampoco la saca.
    /// </remarks>
    [AvaloniaFact]
    public async Task Con_csv_la_rom_de_ejemplo_no_sale()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);
        main.ExportMapCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        form.WantsExampleRom = true;

        Assert.True(form.ShowsExampleRom);

        Use(form, ExportFormat.Csv);

        Assert.False(form.ShowsExampleRom);
        Assert.Equal((string[])["nivel_1.csv"], form.Files.Select(file => file.Name));

        form.Destination = Path.Combine(_folder, "nivel_1.csv");

        await form.AcceptExportCommand.ExecuteAsync(null);

        Assert.Equal(
            (string[])["nivel_1.csv"],
            Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    /// <summary>
    /// La paleta propone su nombre y no ofrece ROM.
    /// </summary>
    /// <remarks>
    /// Sale un solo fichero, así que el nombre que se elija es el que se escribe: el
    /// <c>_palette</c> va en el que se propone, no pegado detrás del que se teclee.
    /// </remarks>
    [AvaloniaFact]
    public void La_paleta_propone_su_nombre_y_no_ofrece_rom()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.ExportPaletteCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        Assert.Equal(
            (string[])[$"{Services.AsmLabel.Of(main.Palettes.ActivePalette.Name)}_palette.asm"],
            form.Files.Select(file => file.Name));

        Assert.False(form.ShowsExampleRom);

        form.Destination = Path.Combine(_folder, "mipaleta.bin");

        Assert.Equal((string[])["mipaleta.asm"], form.Files.Select(file => file.Name));
    }

    /// <summary>
    /// El nombre que propone el mapa es el de su etiqueta, como en los otros tres.
    /// </summary>
    /// <remarks>
    /// Proponía «Nivel 1.bin», con el espacio y la mayúscula, mientras el fichero llevaba
    /// dentro <c>nivel_1_map:</c>. Ahora el fichero y su etiqueta se llaman igual.
    /// </remarks>
    [AvaloniaFact]
    public async Task El_mapa_propone_el_nombre_de_su_etiqueta()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        main.OpenMap(new TileMap("Nivel 1", 4, 4), tiles);
        main.ExportMapCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        Assert.Equal("nivel_1.asm", form.Files[0].Name);

        form.Destination = Path.Combine(_folder, form.Files[0].Name);

        await form.AcceptExportCommand.ExecuteAsync(null);

        Assert.Contains(
            "nivel_1_map:",
            await File.ReadAllTextAsync(Path.Combine(_folder, "nivel_1.asm")));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un juego con todo lo que puede salir: supertiles y atributos.</summary>
    private static TileSet Rich()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2, SuperTileHeight = 2 };

        tileSet.AttributeNames.Define(0, "Sólido");

        return tileSet;
    }

    /// <summary>El panel abierto por donde lo abre el usuario.</summary>
    private static ExportViewModel Panel(TileSet tileSet)
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(tileSet);
        main.ExportTileSetCommand.Execute(null);

        return (ExportViewModel)main.RightPanViewModel!;
    }

    private static void Use(ExportViewModel form, ExportFormat format) =>
        form.Format = form.Formats.Single(choice => choice.Format == format);

    /// <summary>Un banco con una animación que pide un grupo que no está.</summary>
    private static SpriteBank Animated()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        var animation = new SpriteAnimation("Andar", AnimationKind.Groups);

        animation.Steps.Add(new AnimationFrame { Target = 3, Wait = 7 });
        bank.Animations.Add(animation);

        return bank;
    }

    /// <summary>El panel de un banco, abierto por donde lo abre el usuario.</summary>
    private static ExportViewModel BankPanel(SpriteBank bank)
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenSpriteBank(bank);
        main.ExportSpriteBankCommand.Execute(null);

        return (ExportViewModel)main.RightPanViewModel!;
    }
}
