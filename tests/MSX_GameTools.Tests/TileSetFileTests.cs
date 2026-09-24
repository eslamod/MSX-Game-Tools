using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Guardar, cargar y exportar un juego de tiles.</summary>
public class TileSetFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxts-{Guid.NewGuid():N}");

    public TileSetFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Guardar_y_cargar_devuelve_los_mismos_tiles()
    {
        var tileSet = new TileSet("Bosque");

        Paint(tileSet.ListOfTiles[0], row: 0, pattern: 0b1010_0101, fore: 8, back: 4);
        Paint(tileSet.ListOfTiles[137], row: 7, pattern: 0xFF, fore: 15, back: 1);

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());
        LoadedTileSet loaded = TileSetSerializer.Deserialize(json);

        Assert.Equal("Bosque", loaded.TileSet.Name);
        Assert.Equal(256, loaded.TileSet.ListOfTiles.Count);

        TileRow first = loaded.TileSet.ListOfTiles[0].ArrayTileRows[0];
        Assert.Equal(0b1010_0101, first.PatternByte);
        Assert.Equal(8, first.ForeColor);
        Assert.Equal(4, first.BackColor);

        TileRow far = loaded.TileSet.ListOfTiles[137].ArrayTileRows[7];
        Assert.Equal(0xFF, far.PatternByte);
        Assert.Equal(0xF1, far.ColorByte);
    }

    /// <summary>
    /// Los 256 existen siempre, pero guardar los vacíos serían veinte kilobytes de ceros
    /// por juego. Sólo van los tocados, con su número delante.
    /// </summary>
    [AvaloniaFact]
    public void Solo_se_guardan_los_tiles_que_se_han_tocado()
    {
        var tileSet = new TileSet("Bosque");
        Paint(tileSet.ListOfTiles[5], row: 0, pattern: 0x01, fore: 15, back: 0);

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());

        Assert.Contains("\"index\": 5", json);
        Assert.DoesNotContain("\"index\": 6", json);

        // Y al cargar, los que no venían siguen estando y vacíos.
        LoadedTileSet loaded = TileSetSerializer.Deserialize(json);

        Assert.Equal(256, loaded.TileSet.ListOfTiles.Count);
        Assert.Equal(0, loaded.TileSet.ListOfTiles[6].ArrayTileRows[0].PatternByte);
    }

    [AvaloniaFact]
    public void Un_juego_recien_creado_no_escribe_ningun_tile()
    {
        string json = TileSetSerializer.Serialize(new TileSet("Vacio"), ColorPalette.CreateMsxStandard());

        Assert.DoesNotContain("\"index\"", json);
    }

    [AvaloniaFact]
    public void El_color_del_borde_viaja_con_el_juego()
    {
        string json = TileSetSerializer.Serialize(
            new TileSet("Bosque"), ColorPalette.CreateMsxStandard(), borderColorIndex: 7);

        Assert.Equal(7, TileSetSerializer.Deserialize(json).BorderColorIndex);
    }

    [AvaloniaFact]
    public void La_paleta_viaja_con_el_juego()
    {
        var palette = new PaletteLibrary().Add("Nocturna");
        palette[3].SetComponents(7, 0, 5);

        LoadedTileSet loaded = TileSetSerializer.Deserialize(
            TileSetSerializer.Serialize(new TileSet("Bosque"), palette));

        Assert.Equal("Nocturna", loaded.Palette.Name);
        Assert.Equal("705", loaded.Palette[3].HexRgb);
    }

    [AvaloniaFact]
    public void Un_fichero_roto_avisa_de_lo_que_pasa()
    {
        Assert.Throws<FileFormatException>(() => TileSetSerializer.Deserialize("{ esto no es un tileset }"));

        // Un tile fuera del juego dice cuál es y cuántos hay.
        FileFormatException error = Assert.Throws<FileFormatException>(() => TileSetSerializer.Deserialize(
            """{"version":1,"name":"X","palette":{"name":"MSX","colors":[]},"tiles":[{"index":900,"pattern":"0000000000000000","colors":"F0F0F0F0F0F0F0F0"}]}"""));

        Assert.Contains("900", error.Message);
    }

    // ------------------------------------------------------------------ exportación

    [AvaloniaFact]
    public void Cada_tabla_ocupa_2048_bytes()
    {
        var tileSet = new TileSet("Bosque");

        Assert.Equal(2048, TileSetExporter.PatternsToBinary(tileSet).Length);
        Assert.Equal(2048, TileSetExporter.ColorsToBinary(tileSet).Length);
    }

    [AvaloniaFact]
    public void La_tabla_de_patrones_lleva_la_mascara_y_la_de_colores_los_nibbles()
    {
        var tileSet = new TileSet("Bosque");
        Paint(tileSet.ListOfTiles[1], row: 2, pattern: 0b1000_0001, fore: 6, back: 11);

        byte[] patterns = TileSetExporter.PatternsToBinary(tileSet);
        byte[] colors = TileSetExporter.ColorsToBinary(tileSet);

        // El tile 1 empieza en el byte 8: ocho lineas por tile.
        Assert.Equal(0b1000_0001, patterns[(1 * Tile.Rows) + 2]);
        Assert.Equal(0x6B, colors[(1 * Tile.Rows) + 2]);
    }

    [AvaloniaFact]
    public void El_ensamblador_lleva_los_mismos_bytes_que_el_binario()
    {
        var tileSet = new TileSet("Bosque");
        Paint(tileSet.ListOfTiles[0], row: 0, pattern: 0x3C, fore: 15, back: 4);

        AssertSameBytes(TileSetExporter.PatternsToBinary(tileSet), TileSetExporter.PatternsToAssembler(tileSet));
        AssertSameBytes(TileSetExporter.ColorsToBinary(tileSet), TileSetExporter.ColorsToAssembler(tileSet));
    }

    /// <summary>
    /// Lo de las tres copias no está en los bytes, así que si no lo dice la cabecera
    /// quien use el fichero pintará un tercio de pantalla y no sabrá por qué.
    /// </summary>
    [AvaloniaFact]
    public void La_cabecera_avisa_de_que_hay_que_replicar_la_tabla_tres_veces()
    {
        string asm = TileSetExporter.PatternsToAssembler(new TileSet("Bosque"));

        Assert.Contains("Copy this table 3 times in VRAM", asm);
        Assert.Contains("bosque_patterns:", asm);
        Assert.Contains("bosque_patterns_end:", asm);
    }

    [AvaloniaFact]
    public async Task Guardar_esta_deshabilitado_sin_un_juego_de_tiles_delante()
    {
        var main = new MainWindowViewModel(new TestDialogService { SavePath = Path.Combine(_folder, "b.json") });

        Assert.False(main.SaveDocumentCommand.CanExecute(null));
        Assert.False(main.ExportTileSetCommand.CanExecute(null));

        main.OpenTileSet(new TileSet("Bosque"));

        Assert.True(main.SaveDocumentCommand.CanExecute(null));
        Assert.True(main.ExportTileSetCommand.CanExecute(null));

        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Guardar_y_cargar_desde_el_menu_abre_una_pestana_nueva()
    {
        string path = Path.Combine(_folder, "bosque.json");
        var dialogs = new TestDialogService { SavePath = path, OpenPath = path };
        var main = new MainWindowViewModel(dialogs);

        TileSetEditorViewModel editor = main.OpenTileSet(new TileSet("Bosque"));
        editor.PixelSurface.Set(3, 4, true);

        await main.SaveDocumentCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path));

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, main.Tabs.Count);

        var loaded = (TileSetEditorViewModel)main.Tabs[1];

        Assert.Equal("Bosque (TS)", loaded.Header);
        Assert.True(loaded.TileSet.ListOfTiles[0].ArrayTileRows[4].ArrayPattern[3]);
    }

    [AvaloniaFact]
    public async Task Exportar_escribe_los_dos_ficheros_y_recuerda_lo_de_los_tercios()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        main.OpenTileSet(new TileSet("Bosque"));

        await TestExport.TileSetAsync(main, ExportFormat.Binary, Path.Combine(_folder, "bosque.bin"));

        Assert.Equal(2048, new FileInfo(Path.Combine(_folder, "bosque_patterns.bin")).Length);
        Assert.Equal(2048, new FileInfo(Path.Combine(_folder, "bosque_colors.bin")).Length);

        Assert.Single(dialogs.Messages);
        Assert.Contains("bosque_patterns.bin", dialogs.Messages[0]);
        Assert.Contains("3 veces en VRAM", dialogs.Messages[0]);
    }

    // -------------------------------------------------------------------- bloques

    /// <summary>
    /// Los bloques van dentro del fichero del juego porque son números de tile, y esos
    /// números sólo significan algo con este juego delante.
    /// </summary>
    [AvaloniaFact]
    public void Los_bloques_van_dentro_del_fichero_del_juego()
    {
        var tileSet = new TileSet("Bosque");
        var block = new TileBlock("Arbol");

        block[1, 0] = 5;
        block[0, 1] = 0;     // el tile 0, que no es una celda vacia
        block[1, 1] = 200;

        tileSet.Blocks.Add(block);

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());

        // Dos digitos por celda y .. donde no hay nada, una cadena por fila.
        Assert.Contains("\"..05\"", json);
        Assert.Contains("\"00C8\"", json);

        LoadedTileSet loaded = TileSetSerializer.Deserialize(json);
        TileBlock read = Assert.Single(loaded.TileSet.Blocks);

        Assert.Equal("Arbol", read.Name);
        Assert.Equal((2, 2), (read.Width, read.Height));
        Assert.Null(read[0, 0]);
        Assert.Equal(5, read[1, 0]);
        Assert.Equal(0, read[0, 1]);
        Assert.Equal(200, read[1, 1]);
    }

    /// <summary>
    /// El tamaño lo dice la forma de las filas, no hasta dónde llega el último tile: un
    /// supertile de 2x2 con la esquina vacía tiene que volver midiendo 2x2.
    /// </summary>
    [AvaloniaFact]
    public void Un_bloque_con_la_esquina_vacia_vuelve_con_su_tamano()
    {
        var tileSet = new TileSet("Bosque");
        var block = new TileBlock("Supertile");

        block[1, 1] = 9;
        block[1, 1] = null;
        block[0, 0] = 3;

        tileSet.Blocks.Add(block);

        LoadedTileSet loaded = TileSetSerializer.Deserialize(
            TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard()));

        Assert.Equal((2, 2), (loaded.TileSet.Blocks[0].Width, loaded.TileSet.Blocks[0].Height));
    }

    /// <summary>Un juego guardado antes de que existieran los bloques se sigue abriendo.</summary>
    [AvaloniaFact]
    public void Un_fichero_sin_bloques_se_abre_igual()
    {
        var tileSet = new TileSet("Bosque");
        Paint(tileSet.ListOfTiles[0], row: 0, pattern: 0x0F, fore: 2, back: 3);

        string json = TileSetSerializer
            .Serialize(tileSet, ColorPalette.CreateMsxStandard())
            .Replace("\"version\": 2", "\"version\": 1");

        LoadedTileSet loaded = TileSetSerializer.Deserialize(json);

        Assert.Empty(loaded.TileSet.Blocks);
        Assert.Equal(0x0F, loaded.TileSet.ListOfTiles[0].ArrayTileRows[0].PatternByte);
    }

    [AvaloniaTheory]
    [InlineData("\"rows\": [\"0102\", \"03\"]", "no miden todas lo mismo")]
    [InlineData("\"rows\": [\"010\"]", "dos por celda")]
    [InlineData("\"rows\": [\"01ZZ\"]", "no es un número de tile")]
    public void Un_bloque_estropeado_se_rechaza_diciendo_que_pasa(string rows, string expected)
    {
        var tileSet = new TileSet("Bosque");
        tileSet.Blocks.Add(new TileBlock("Arbol") { [0, 0] = 1, [1, 0] = 2 });

        // Por regex y no por texto literal: el json va indentado y depende del salto de linea.
        string json = Regex.Replace(
            TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard()),
            @"""rows"": \[[^\]]*\]",
            rows);

        FileFormatException error = Assert.Throws<FileFormatException>(
            () => TileSetSerializer.Deserialize(json));

        Assert.Contains(expected, error.Message);
        Assert.Contains("Arbol", error.Message);
    }

    private static void Paint(Tile tile, int row, int pattern, int fore, int back)
    {
        TileRow line = tile.ArrayTileRows[row];

        for (int column = 0; column < TileRow.Columns; column++)
            line.ArrayPattern[column] = (pattern & (1 << (TileRow.Columns - 1 - column))) != 0;

        line.ForeColor = fore;
        line.BackColor = back;
    }

    /// <summary>Extrae los bytes de las líneas db y los compara con el binario.</summary>
    private static void AssertSameBytes(byte[] binary, string assembler)
    {
        List<byte> fromText = [];

        foreach (string line in assembler.Split(Environment.NewLine))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(AsmStyle.Default.Data, StringComparison.Ordinal))
                continue;

            foreach (string value in trimmed[AsmStyle.Default.Data.Length..]
                         .Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                fromText.Add(Convert.ToByte(value.Trim()[AsmHex.Prefix.Length..], 16));
            }
        }

        Assert.Equal(binary, fromText);
    }
}
