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

    /// <summary>La del banco de sprites igual, y además se trae el reproductor.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Bundles))]
    public async Task La_rom_del_banco_ensambla_y_da_un_cartucho(
        string name, ExportFormat format, int size)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        await ExportBankAsync(Animated(), format, name);

        string rom = Path.Combine(_folder, "bosque.rom");
        string said = Assembler.Run(
            tool!, name, Path.Combine(_folder, "bosque_rom.asm"), rom);

        Assert.True(File.Exists(rom), $"{name} no sacó ROM: {said}");

        byte[] bytes = File.ReadAllBytes(rom);

        Assert.Equal(size, bytes.Length);
        Assert.Equal("AB", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
    }

    /// <summary>
    /// Y de todos los ensambladores que haya sale el mismo programa.
    /// </summary>
    /// <remarks>
    /// Comparando bytes y no sólo el tamaño, por el mismo motivo que en
    /// <see cref="AssemblersTests"/>: un ensamblador puede tragarse una línea y entender otra
    /// cosa —un número en otra base, una directiva que hace algo parecido— y eso no se ve en
    /// lo que diga por pantalla, se ve en el binario. Lo que sí caza el tamaño es el relleno
    /// del final, y lo que no caza ninguno de los dos, que arranque.
    /// </remarks>
    [AvaloniaFact]
    public async Task Todos_los_ensambladores_sacan_el_mismo_programa()
    {
        var made = new Dictionary<string, byte[]>();

        foreach (string name in (string[])["sasSX", "sjasmplus", "pasmo", "asMSX"])
        {
            if (Assembler.Find(name) is not { } tool)
                continue;

            string folder = Path.Combine(_folder, name);

            Directory.CreateDirectory(folder);

            await ExportBankAsync(Animated(), ExportFormat.Binary, name, folder);

            string rom = Path.Combine(folder, "bosque.rom");

            Assembler.Run(tool, name, Path.Combine(folder, "bosque_rom.asm"), rom);

            if (File.Exists(rom))
                made[name] = Code(File.ReadAllBytes(rom));
        }

        Assert.SkipWhen(made.Count < 2, "aquí no hay dos ensambladores con los que comparar");

        foreach ((string name, byte[] bytes) in made)
        {
            Assert.Equal(made.First().Value, bytes);

            // Y que quede programa después de quitar el relleno, no sea que se compare vacío
            // con vacío.
            Assert.True(bytes.Length > 1000, $"{name} se quedó en {bytes.Length} bytes");
        }
    }

    /// <summary>Un banco sin animaciones deja el bloque vacío y el reproductor no arranca.</summary>
    [AvaloniaFact]
    public void Un_banco_sin_animaciones_deja_el_bloque_vacio()
    {
        string rom = ExampleRom.ForSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"),
            ColorPalette.CreateMsxStandard(),
            AsmDialect.SasSx,
            "bicho",
            binary: true);

        Assert.DoesNotContain("bicho_animations", rom);

        // Las etiquetas siguen ahí: el reproductor las compara para saber que no hay nada.
        Assert.Contains("AnimationsData:", rom);
        Assert.Contains("AnimationsEnd:", rom);
    }

    /// <summary>
    /// Los cuatro con la de supertiles, que también es de 32K.
    /// </summary>
    /// <remarks>
    /// Con supertiles rectangulares a propósito: uno cuadrado disimularía un ancho y un alto
    /// intercambiados, que es justo lo que esta ROM existe para delatar.
    /// </remarks>
    public static TheoryData<string, ExportFormat, int> SuperBundles => new()
    {
        { "sasSX", ExportFormat.Binary, 32768 },
        { "sasSX", ExportFormat.Assembler, 32768 },
        { "sjasmplus", ExportFormat.Binary, 32768 },
        { "sjasmplus", ExportFormat.Assembler, 32768 },
        { "pasmo", ExportFormat.Binary, 32768 },
        { "pasmo", ExportFormat.Assembler, 32768 },
        { "asMSX", ExportFormat.Binary, 8192 },
        { "asMSX", ExportFormat.Assembler, 8192 },
    };

    /// <summary>La de un mapa de supertiles ensambla con la tabla al lado.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(SuperBundles))]
    public async Task La_rom_de_supertiles_ensambla_y_da_un_cartucho(
        string name, ExportFormat format, int size)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        await ExportSuperMapAsync(format, name);

        string rom = Path.Combine(_folder, "nivel.rom");
        string said = Assembler.Run(
            tool!, name, Path.Combine(_folder, "nivel_rom.asm"), rom);

        Assert.True(File.Exists(rom), $"{name} no sacó ROM: {said}");

        byte[] bytes = File.ReadAllBytes(rom);

        Assert.Equal(size, bytes.Length);
        Assert.Equal("AB", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
    }

    /// <summary>
    /// Un mapa de supertiles saca otra ROM, y se trae la tabla del juego de tiles.
    /// </summary>
    /// <remarks>
    /// Sus celdas son sitios de la tabla y no números de tile, así que el programa resuelve
    /// cada celda al dibujar. La tabla sale del juego de tiles, que es de donde es.
    /// </remarks>
    [AvaloniaFact]
    public void Un_mapa_de_supertiles_saca_la_rom_que_resuelve_la_tabla()
    {
        string rom = ExampleRom.ForMap(
            new TileMap("Nivel", 20, 15),
            [WithSuperTiles()],
            ColorPalette.CreateMsxStandard(),
            AsmDialect.SasSx,
            "nivel",
            binary: true);

        Assert.Contains("ReadSuperHeader", rom);
        Assert.Contains("\"bosque_supertiles.bin\"", rom);
        Assert.Contains(";     bosque_supertiles.bin", rom);

        // Y la de tiles no habla de tablas de supertiles.
        Assert.DoesNotContain("ReadSuperHeader", MapRom(Bands(1)));
    }

    /// <summary>Y la del mapa sale igual de todos, que es donde más piezas hay que juntar.</summary>
    /// <inheritdoc cref="Todos_los_ensambladores_sacan_el_mismo_programa" path="/remarks"/>
    [AvaloniaFact]
    public async Task Todos_los_ensambladores_sacan_el_mismo_mapa()
    {
        var made = new Dictionary<string, byte[]>();

        foreach (string name in (string[])["sasSX", "sjasmplus", "pasmo", "asMSX"])
        {
            if (Assembler.Find(name) is not { } tool)
                continue;

            string folder = Path.Combine(_folder, name);

            Directory.CreateDirectory(folder);

            await ExportMapAsync(ExportFormat.Binary, name, folder);

            string rom = Path.Combine(folder, "nivel.rom");

            Assembler.Run(tool, name, Path.Combine(folder, "nivel_rom.asm"), rom);

            if (File.Exists(rom))
                made[name] = Code(File.ReadAllBytes(rom));
        }

        Assert.SkipWhen(made.Count < 2, "aquí no hay dos ensambladores con los que comparar");

        foreach ((string name, byte[] bytes) in made)
        {
            Assert.Equal(made.First().Value, bytes);
            Assert.True(bytes.Length > 1000, $"{name} se quedó en {bytes.Length} bytes");
        }
    }

    /// <summary>
    /// Los cuatro con la del mapa, que es de 32K.
    /// </summary>
    /// <remarks>
    /// El cartucho ocupa las páginas 1 y 2 porque el mapa viaja dentro. asMSX redondea al
    /// más pequeño donde quepa, y con este mapa y sus tres juegos de tiles le bastan 16K.
    /// </remarks>
    public static TheoryData<string, ExportFormat, int> MapBundles => new()
    {
        { "sasSX", ExportFormat.Binary, 32768 },
        { "sasSX", ExportFormat.Assembler, 32768 },
        { "sjasmplus", ExportFormat.Binary, 32768 },
        { "sjasmplus", ExportFormat.Assembler, 32768 },
        { "pasmo", ExportFormat.Binary, 32768 },
        { "pasmo", ExportFormat.Assembler, 32768 },
        { "asMSX", ExportFormat.Binary, 16384 },
        { "asMSX", ExportFormat.Assembler, 16384 },
    };

    /// <summary>La del mapa ensambla con lo que sale del juego de tiles al lado.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(MapBundles))]
    public async Task La_rom_del_mapa_ensambla_y_da_un_cartucho(
        string name, ExportFormat format, int size)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        await ExportMapAsync(format, name);

        string rom = Path.Combine(_folder, "nivel.rom");
        string said = Assembler.Run(
            tool!, name, Path.Combine(_folder, "nivel_rom.asm"), rom);

        Assert.True(File.Exists(rom), $"{name} no sacó ROM: {said}");

        byte[] bytes = File.ReadAllBytes(rom);

        Assert.Equal(size, bytes.Length);
        Assert.Equal("AB", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
    }

    /// <summary>
    /// Un mapa con tres juegos de tiles carga una tabla distinta en cada tercio.
    /// </summary>
    /// <remarks>
    /// Es lo que hace la lista de bloques: el programa no sabe de bandas, recorre lo que le
    /// pongan. Con un solo juego las tres entradas apuntan al mismo sitio.
    /// </remarks>
    [AvaloniaFact]
    public void Un_mapa_con_tres_bandas_carga_una_tabla_por_tercio()
    {
        string rom = MapRom(Bands(3));

        Assert.Contains("Patterns0, 0x0000", rom);
        Assert.Contains("Patterns1, 0x0800", rom);
        Assert.Contains("Patterns2, 0x1000", rom);
        Assert.Contains("Colors2, 0x3000", rom);

        // Y cada uno se trae los suyos.
        Assert.Contains("\"bosque_patterns.bin\"", rom);
        Assert.Contains("\"cielo_patterns.bin\"", rom);
        Assert.Contains("\"cueva_patterns.bin\"", rom);
    }

    /// <summary>Y con uno solo, la misma en los tres.</summary>
    [AvaloniaFact]
    public void Un_mapa_de_una_banda_carga_la_misma_tabla_en_los_tres_tercios()
    {
        string rom = MapRom(Bands(1));

        Assert.Contains("Patterns0, 0x0000", rom);
        Assert.Contains("Patterns0, 0x0800", rom);
        Assert.Contains("Patterns0, 0x1000", rom);

        Assert.DoesNotContain("Patterns1", rom);
    }

    /// <summary>
    /// Con un juego de screen 1 son dos bloques y no seis.
    /// </summary>
    /// <remarks>
    /// Una tabla de patrones y otra de colores de 32 bytes para toda la pantalla, que es lo
    /// que hay en GRAPHIC 1. Copiarlas por tercios escribiría encima de lo que viene detrás.
    /// </remarks>
    [AvaloniaFact]
    public void Un_mapa_de_screen_1_carga_dos_bloques()
    {
        string rom = MapRom([new TileSet("Bosque", TileSet.GraphicMode.Graphic1)]);

        Assert.Contains("TABLE_LOADS     .equ 2", rom);
        Assert.Contains($"Colors0, 0x2000, {TileSet.ColorGroupCount}", rom);
        Assert.DoesNotContain("0x0800", rom);
    }

    /// <summary>La cabecera pide los ficheros del juego de tiles, que el mapa no exporta.</summary>
    [AvaloniaFact]
    public void La_rom_del_mapa_pide_los_ficheros_del_juego_de_tiles()
    {
        string rom = MapRom(Bands(1));

        Assert.Contains(";     bosque_patterns.bin", rom);
        Assert.Contains(";     bosque_colors.bin", rom);
        Assert.Contains(";     nivel.bin", rom);
    }

    /// <summary>
    /// El reproductor sabe donde va el color de un sprite en el modo del banco.
    /// </summary>
    /// <remarks>
    /// En modo 2 son los 16 bytes de la tabla de color del plano y en modo 1 el cuarto byte
    /// del atributo. Se elige al ensamblar, y si se eligiera mal la ROM ensamblaria igual y
    /// pintaria los colores donde no son.
    /// </remarks>
    [AvaloniaFact]
    public void El_reproductor_sabe_donde_va_el_color_en_modo_2()
    {
        string player = ExampleRom.AnimationPlayer(
            AsmDialect.SasSx, new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        Assert.Contains("ANIM_COLOR_TABLE .equ 1", player);
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

    /// <summary>
    /// Every ROM that comes out of the panel carries its license at the top: MIT-0.
    /// </summary>
    /// <remarks>
    /// That code ends up inside other people's games, far from this repository and from its
    /// LICENSE files, so the notice travels inside each file: whoever comes across one on its
    /// own knows it can be used with no conditions. The player of the animations as well, which
    /// goes out as a file of its own next to the ROM of the bank.
    /// </remarks>
    [AvaloniaFact]
    public async Task Cada_rom_de_ejemplo_lleva_su_licencia()
    {
        string bank = Path.Combine(_folder, "banco");
        string map = Path.Combine(_folder, "mapa");
        string super = Path.Combine(_folder, "supertiles");

        foreach (string folder in (string[])[bank, map, super])
            Directory.CreateDirectory(folder);

        await ExportAsync(new TileSet("Bosque"), ExportFormat.Binary, "sasSX");
        await ExportBankAsync(Animated(), ExportFormat.Binary, "sasSX", bank);
        await ExportMapAsync(ExportFormat.Binary, "sasSX", map);
        await ExportSuperMapAsync(ExportFormat.Binary, "sasSX", super);

        string[] written =
        [
            Path.Combine(_folder, "bosque_rom.asm"),
            Path.Combine(bank, "bosque_rom.asm"),
            Path.Combine(bank, "bosque_player.asm"),
            Path.Combine(map, "nivel_rom.asm"),
            Path.Combine(super, "nivel_rom.asm"),
        ];

        foreach (string file in written)
        {
            // Among the first lines, under the title: the first thing read in the file.
            string head = string.Join('\n', File.ReadLines(file).Take(8));

            Assert.True(
                head.Contains("; SPDX-License-Identifier: MIT-0", StringComparison.Ordinal),
                $"{Path.GetRelativePath(_folder, file)} does not carry its license at the top");
        }
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Lo que queda de una ROM al quitarle el relleno del final.</summary>
    /// <remarks>
    /// Los tres primeros rellenan con 0xFF hasta los 16K y asMSX con 0x00 hasta el cartucho
    /// más pequeño donde quepa, así que lo que se compara es el programa y no lo que sobra.
    /// El último byte de verdad es un <c>ret</c>, así que no se lleva nada por delante.
    /// </remarks>
    private static byte[] Code(byte[] rom) =>
        [.. rom.Reverse().SkipWhile(one => one is 0x00 or 0xFF).Reverse()];

    /// <summary>Un banco con un grupo y una animación que lo usa.</summary>
    private static SpriteBank Animated()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bosque");
        SpriteGroup group = bank.NewGroup(0)!;

        group.Add(new SpriteGroupMember(0, bank.SpritesList[1]));

        var animation = new SpriteAnimation("Andar", AnimationKind.Groups);

        animation.Steps.Add(new AnimationFrame { Target = group.Id, Wait = 5 });
        bank.Animations.Add(animation);

        return bank;
    }

    /// <summary>Exportar un banco con la casilla marcada, por donde lo hace el usuario.</summary>
    private async Task ExportBankAsync(
        SpriteBank bank, ExportFormat format, string assembler, string? folder = null)
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.OpenSpriteBank(bank);

        await TestExport.SpriteBankAsync(
            main,
            format,
            Path.Combine(folder ?? _folder, "bosque.bin"),
            form =>
            {
                form.WantsExampleRom = true;
                form.Assembler = form.Assemblers.Single(one => one.Name == assembler);
            });
    }

    /// <summary>Un mapa dibujado con esos juegos de tiles, sin pasar por el panel.</summary>
    private static string MapRom(IReadOnlyList<TileSet> bands) =>
        ExampleRom.ForMap(
            new TileMap("Nivel", 32, 24),
            bands,
            ColorPalette.CreateMsxStandard(),
            AsmDialect.SasSx,
            "nivel",
            binary: true);

    /// <summary>Un juego con supertiles rectangulares y cuatro puestos.</summary>
    private static TileSet WithSuperTiles()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2, SuperTileHeight = 3 };

        for (int index = 0; index < 4; index++)
        {
            tileSet.Blocks.Add(new TileBlock($"S{index}")
            {
                Width = 2,
                Height = 3,
                [0, 0] = index,
            });
        }

        return tileSet;
    }

    /// <summary>Exportar el juego con supertiles y su mapa, con la ROM marcada en los dos.</summary>
    private async Task ExportSuperMapAsync(ExportFormat format, string assembler, string? folder = null)
    {
        folder ??= _folder;

        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(WithSuperTiles());

        await TestExport.TileSetAsync(
            main,
            format,
            Path.Combine(folder, "bosque.bin"),
            form =>
            {
                form.WantsExampleRom = true;
                form.Assembler = form.Assemblers.Single(one => one.Name == assembler);
            });

        main.OpenMap(new TileMap("Nivel", 20, 15), tiles);

        await TestExport.MapAsync(
            main,
            format,
            Path.Combine(folder, "nivel.bin"),
            form =>
            {
                form.WantsExampleRom = true;
                form.Assembler = form.Assemblers.Single(one => one.Name == assembler);
            });
    }

    /// <summary>Uno, dos o tres juegos de tiles distintos.</summary>
    private static IReadOnlyList<TileSet> Bands(int many) =>
        [.. new[] { "Bosque", "Cielo", "Cueva" }
            .Take(many)
            .Select(one => new TileSet(one))];

    /// <summary>
    /// Exportar el juego de tiles y el mapa a la misma carpeta, con la ROM marcada.
    /// </summary>
    /// <remarks>
    /// En ese orden y los dos, que es lo que hace falta para que la ROM ensamble: los
    /// ficheros del juego de tiles no salen del mapa, y la ROM los nombra.
    /// </remarks>
    private async Task ExportMapAsync(ExportFormat format, string assembler, string? folder = null)
    {
        folder ??= _folder;

        var main = new MainWindowViewModel(new TestDialogService());
        var panels = new List<TileSetEditorViewModel>();

        foreach (TileSet one in Bands(3))
        {
            panels.Add(main.OpenTileSet(one));

            // Con el mismo ensamblador que la ROM del mapa: si el juego de tiles sale con
            // otra directiva, el include de la ROM para en su primera línea.
            await TestExport.TileSetAsync(
                main,
                format,
                Path.Combine(folder, $"{one.Name.ToLowerInvariant()}.bin"),
                form =>
                {
                    form.WantsExampleRom = true;
                    form.Assembler = form.Assemblers.Single(two => two.Name == assembler);
                });
        }

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), panels[0]);

        editor.UseTileSets(panels);

        await TestExport.MapAsync(
            main,
            format,
            Path.Combine(folder, "nivel.bin"),
            form =>
            {
                form.WantsExampleRom = true;
                form.Assembler = form.Assemblers.Single(one => one.Name == assembler);
            });
    }

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
