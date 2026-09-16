using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La ROM de ejemplo que sale junto a lo exportado.
/// </summary>
/// <remarks>
/// <para>
/// Ensamblándola de verdad y midiendo el cartucho, no mirando si el ensamblador protestó: una
/// plantilla a la que le falte una sustitución sale con la llave puesta, y un ensamblador que
/// se coma esa línea daría igual un fichero, más corto o más largo, que en una máquina no
/// arranca.
/// </para>
/// <para>
/// Cada ensamblador se salta si no está instalado, como en <see cref="AssemblersTests"/>.
/// </para>
/// </remarks>
public class ExampleRomTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"msxrom-{Guid.NewGuid():N}");

    public ExampleRomTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>
    /// Los cuatro ensambladores, con las dos salidas de datos —incbin e include— y lo que
    /// tiene que salir de cada uno.
    /// </summary>
    /// <remarks>
    /// El tamaño va aquí porque no es el mismo para todos: tres rellenan hasta los 16K de la
    /// página 1 porque se lo dice la plantilla, y asMSX redondea él al cartucho más pequeño
    /// donde quepa, que para esta ROM son 8K.
    /// </remarks>
    public static TheoryData<string, ExportFormat, int> Bundles => new()
    {
        { "sasSX", ExportFormat.Binary, 16384 },
        { "sasSX", ExportFormat.Assembler, 16384 },
        { "sjasmplus", ExportFormat.Binary, 16384 },
        { "sjasmplus", ExportFormat.Assembler, 16384 },
        { "pasmo", ExportFormat.Binary, 16384 },
        { "pasmo", ExportFormat.Assembler, 16384 },
        { "asMSX", ExportFormat.Binary, 8192 },
        { "asMSX", ExportFormat.Assembler, 8192 },
    };

    /// <summary>Lo que sale del panel ensambla tal cual, sin tocar nada.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Bundles))]
    public async Task La_rom_que_sale_del_panel_ensambla_y_da_un_cartucho(
        string name, ExportFormat format, int size)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        await ExportAsync(Painted(), format, name);

        string rom = Path.Combine(_folder, "bosque.rom");
        string said = Assembler.Run(
            tool!, name, Path.Combine(_folder, "bosque_rom.asm"), rom);

        Assert.True(File.Exists(rom), $"{name} no sacó ROM: {said}");

        byte[] bytes = File.ReadAllBytes(rom);

        // El tamaño y la «AB» del principio: si el relleno del final no se hubiera puesto
        // saldría más corta, y si el org no estuviera, empezaría en otro sitio. Las dos cosas
        // ensamblan sin una palabra y las dos dejan un cartucho que no arranca.
        Assert.Equal(size, bytes.Length);
        Assert.Equal("AB", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
    }

    /// <summary>La ROM sale en ensamblador aunque los datos salgan en binario.</summary>
    [AvaloniaFact]
    public async Task La_rom_sale_en_asm_aunque_los_datos_sean_binarios()
    {
        await ExportAsync(new TileSet("Bosque"), ExportFormat.Binary, "sasSX");

        Assert.Equal(
            (string[])["bosque_colors.bin", "bosque_patterns.bin", "bosque_rom.asm"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    /// <summary>Sin marcar la casilla no sale ninguna ROM, que es como estaba.</summary>
    [AvaloniaFact]
    public async Task Sin_marcar_la_casilla_no_sale_ninguna_rom()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(new TileSet("Bosque"));

        await TestExport.TileSetAsync(
            main, ExportFormat.Binary, Path.Combine(_folder, "bosque.bin"));

        Assert.Equal(
            (string[])["bosque_colors.bin", "bosque_patterns.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    /// <summary>
    /// El ensamblador elegido manda en todo el lote, también en los ficheros de datos.
    /// </summary>
    /// <remarks>
    /// pasmo no admite el punto de <c>.db</c>, y la ROM se trae el fichero de patrones con un
    /// <c>include</c>: si ese fichero saliera con la directiva de las preferencias, el
    /// ensamblado pararía dentro de un fichero que quien exporta no ha escrito.
    /// </remarks>
    [AvaloniaFact]
    public async Task El_ensamblador_elegido_manda_tambien_en_los_ficheros_de_datos()
    {
        await ExportAsync(new TileSet("Bosque"), ExportFormat.Assembler, "pasmo");

        string rom = File.ReadAllText(Path.Combine(_folder, "bosque_rom.asm"));
        string patterns = File.ReadAllText(Path.Combine(_folder, "bosque_patterns.asm"));

        Assert.DoesNotContain(".db", rom);
        Assert.DoesNotContain(".org", rom);
        Assert.DoesNotContain(".db", patterns);

        Assert.Contains("include \"bosque_patterns.asm\"", rom);
    }

    /// <summary>La paleta del juego viaja dentro de la ROM, sin un fichero más.</summary>
    [AvaloniaFact]
    public async Task La_paleta_del_juego_viaja_dentro_de_la_rom()
    {
        ColorPalette palette = new PaletteLibrary().Add("Prueba");
        palette[5].SetComponents(red: 5, green: 6, blue: 2);

        await ExportAsync(new TileSet("Bosque"), ExportFormat.Binary, "sasSX", palette);

        string rom = File.ReadAllText(Path.Combine(_folder, "bosque_rom.asm"));

        Assert.Contains("0x52,0x06", rom);
    }

    /// <summary>
    /// Un juego de screen 1 carga otra tabla de colores y una sola vez.
    /// </summary>
    /// <remarks>
    /// En GRAPHIC 1 la tabla de colores son 32 bytes —uno por grupo de ocho tiles— y hay una
    /// para toda la pantalla, no una por tercio. Copiándola como la de GRAPHIC 2 se escribirían
    /// 6144 bytes de basura por encima de lo que viene detrás.
    /// </remarks>
    [AvaloniaFact]
    public void Un_juego_de_screen_1_carga_32_bytes_de_color_una_sola_vez()
    {
        string rom = ExampleRom.ForTileSet(
            new TileSet("Bosque", TileSet.GraphicMode.Graphic1),
            ColorPalette.CreateMsxStandard(),
            AsmDialect.SasSx,
            "bosque",
            binary: true);

        Assert.Contains($"COLOR_BYTES     .equ {TileSet.ColorGroupCount}", rom);
        Assert.Contains("TABLE_COPIES    .equ 1", rom);

        // Y los registros del modo, que en GRAPHIC 1 no llevan la máscara de los tercios.
        Assert.Contains(".db  0, 0x00", rom);
        Assert.Contains(".db  3, 0x80", rom);
    }

    /// <summary>Y uno de screen 2 las copia en los tres tercios.</summary>
    [AvaloniaFact]
    public void Un_juego_de_screen_2_copia_las_tablas_en_los_tres_tercios()
    {
        string rom = ExampleRom.ForTileSet(
            new TileSet("Bosque"),
            ColorPalette.CreateMsxStandard(),
            AsmDialect.SasSx,
            "bosque",
            binary: true);

        Assert.Contains($"COLOR_BYTES     .equ {TileSetExporter.TableBytes}", rom);
        Assert.Contains($"TABLE_COPIES    .equ {TileSetExporter.ScreenThirds}", rom);

        Assert.Contains(".db  3, 0xFF", rom);
        Assert.Contains(".db  4, 0x03", rom);
    }

    /// <summary>
    /// Con asMSX la cabecera del cartucho no se escribe a mano.
    /// </summary>
    /// <remarks>
    /// La escribe él desde <c>.rom</c> y <c>.start</c>, y el nombre de la ROM sale del
    /// <c>.filename</c> porque no lo lleva en la línea de órdenes. El relleno del final
    /// tampoco va: su <c>ds</c> no admite byte de relleno y esa línea no ensamblaría.
    /// </remarks>
    [AvaloniaFact]
    public void Con_asmsx_la_cabecera_del_cartucho_no_se_escribe_a_mano()
    {
        string rom = ExampleRom.ForTileSet(
            new TileSet("Bosque"),
            ColorPalette.CreateMsxStandard(),
            AsmDialect.AsMsx,
            "bosque",
            binary: true);

        Assert.Contains(".filename \"bosque\"", rom);
        Assert.Contains(".start Begin", rom);

        Assert.DoesNotContain("db \"AB\"", rom);
        Assert.DoesNotContain("0x8000 - RomEnd", rom);
    }

    /// <summary>Y los otros sí la llevan, con el vector que apunta al arranque.</summary>
    [AvaloniaFact]
    public void Los_demas_llevan_la_cabecera_del_cartucho_escrita()
    {
        string rom = ExampleRom.ForTileSet(
            new TileSet("Bosque"),
            ColorPalette.CreateMsxStandard(),
            AsmDialect.Pasmo,
            "bosque",
            binary: true);

        Assert.Contains("db \"AB\"", rom);
        Assert.Contains("dw Begin", rom);
        Assert.Contains("ds 0x8000 - RomEnd, 0xFF", rom);
    }

    /// <summary>La cabecera dice con qué se ensambla y qué ficheros hacen falta al lado.</summary>
    [AvaloniaFact]
    public void La_cabecera_dice_como_ensamblarla()
    {
        string rom = ExampleRom.ForTileSet(
            new TileSet("Bosque"),
            ColorPalette.CreateMsxStandard(),
            AsmDialect.SjasmPlus,
            "nivel1",
            binary: true);

        Assert.Contains("sjasmplus --raw=nivel1.rom nivel1_rom.asm", rom);
        Assert.Contains("nivel1_patterns.bin", rom);
        Assert.Contains("nivel1_colors.bin", rom);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un juego con dibujo, para que las tablas no sean 4096 ceros.</summary>
    private static TileSet Painted()
    {
        var tileSet = new TileSet("Bosque");

        for (int index = 0; index < TileSet.TileCount; index++)
        {
            for (int row = 0; row < Tile.Rows; row++)
                tileSet.ListOfTiles[index].ArrayTileRows[row].ArrayPattern[(index + row) % 8] = true;
        }

        return tileSet;
    }

    /// <summary>Exportar con la casilla marcada, por donde lo hace el usuario.</summary>
    private async Task ExportAsync(
        TileSet tileSet, ExportFormat format, string assembler, ColorPalette? palette = null)
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenTileSet(tileSet, palette);

        await TestExport.TileSetAsync(
            main,
            format,
            Path.Combine(_folder, "bosque.bin"),
            form =>
            {
                form.WantsExampleRom = true;
                form.Assembler = form.Assemblers.Single(one => one.Name == assembler);
            });
    }
}
