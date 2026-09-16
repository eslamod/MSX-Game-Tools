using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La tabla de supertiles tal como la va a leer la máquina.
/// </summary>
/// <remarks>
/// Tres bytes de cabecera —ancho, alto y cuántos— y después los números de tile de cada
/// supertile, de izquierda a derecha y de arriba abajo, el mismo orden que las celdas de un
/// mapa y las líneas de un patrón.
/// </remarks>
public class SuperTileExportTests
{
    [AvaloniaFact]
    public void La_cabecera_trae_el_tamaño_y_cuantos_hay()
    {
        TileSet tileSet = WithSuperTiles(2, 2, count: 3);

        byte[] bytes = SuperTileExporter.ToBinary(tileSet);

        Assert.Equal(2, bytes[0]);
        Assert.Equal(2, bytes[1]);
        Assert.Equal(3, bytes[2]);

        // Cabecera mas tres supertiles de cuatro tiles.
        Assert.Equal(SuperTileExporter.HeaderBytes + (3 * 4), bytes.Length);
    }

    /// <summary>
    /// Los tiles salen de izquierda a derecha y de arriba abajo.
    /// </summary>
    /// <remarks>
    /// El mismo orden que las celdas de un mapa: el cargador recorre la tabla de nombres
    /// así, y una tabla en otro orden le saldría del revés sin que nada lo avise.
    /// </remarks>
    [AvaloniaFact]
    public void Los_tiles_salen_por_filas()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2, SuperTileHeight = 2 };

        tileSet.Blocks.Add(new TileBlock("Uno")
        {
            [0, 0] = 10,
            [1, 0] = 11,
            [0, 1] = 12,
            [1, 1] = 13,
        });

        byte[] bytes = SuperTileExporter.ToBinary(tileSet);

        Assert.Equal([10, 11, 12, 13], bytes.Skip(SuperTileExporter.HeaderBytes));
    }

    /// <summary>
    /// Una celda sin tile sale como el tile 0.
    /// </summary>
    /// <remarks>
    /// En la máquina no hay huecos: toda celda de la tabla de nombres dibuja algo, así que
    /// el hueco tiene que convertirse en algún número.
    /// </remarks>
    [AvaloniaFact]
    public void Los_huecos_salen_como_el_tile_cero()
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = 2, SuperTileHeight = 1 };

        tileSet.Blocks.Add(new TileBlock("Medio") { [1, 0] = 9 });

        byte[] bytes = SuperTileExporter.ToBinary(tileSet);

        Assert.Equal([0, 9], bytes.Skip(SuperTileExporter.HeaderBytes));
    }

    /// <summary>
    /// Con 256 supertiles la cuenta se escribe como 0.
    /// </summary>
    /// <remarks>
    /// Es el tope que un mapa puede nombrar, porque una celda es un byte, y en un byte no
    /// cabe el 256. Con el convenio de que 0 son 256 no hace falta un segundo byte.
    /// </remarks>
    [AvaloniaFact]
    public void Doscientos_cincuenta_y_seis_se_escriben_como_cero()
    {
        TileSet tileSet = WithSuperTiles(1, 1, count: 256);

        byte[] bytes = SuperTileExporter.ToBinary(tileSet);

        Assert.Equal(0, bytes[2]);
        Assert.Equal(SuperTileExporter.HeaderBytes + 256, bytes.Length);
    }

    /// <summary>Lo que pase de 256 no sale: ningún mapa podría nombrarlo.</summary>
    [AvaloniaFact]
    public void De_256_para_arriba_no_salen()
    {
        TileSet tileSet = WithSuperTiles(1, 1, count: 300);

        Assert.Equal(256, SuperTileExporter.CountOf(tileSet));
        Assert.Equal(SuperTileExporter.HeaderBytes + 256, SuperTileExporter.ToBinary(tileSet).Length);
    }

    /// <summary>El asm trae exactamente los mismos bytes que el binario.</summary>
    [AvaloniaFact]
    public void El_asm_trae_los_mismos_bytes_que_el_binario()
    {
        TileSet tileSet = WithSuperTiles(3, 2, count: 4);

        Assert.Equal(SuperTileExporter.ToBinary(tileSet), BytesOf(SuperTileExporter.ToAssembler(tileSet)));
    }

    [AvaloniaFact]
    public void El_asm_lleva_etiqueta_y_final_para_medirlo()
    {
        string asm = SuperTileExporter.ToAssembler(WithSuperTiles(2, 2, count: 1));

        Assert.Contains("bosque_supertiles:", asm);
        Assert.Contains("bosque_supertiles_end:", asm);
    }

    // ------------------------------------------------------------------ el comando

    /// <summary>
    /// Exportar el juego escribe también la tabla, y sólo si va de supertiles.
    /// </summary>
    /// <remarks>
    /// Sale con el juego y no con el mapa aunque sea el mapa quien la indexa: los
    /// supertiles son del juego, y sacándola con cada mapa se repetiría igual en todos.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Exportar_el_juego_escribe_la_tabla_si_la_hay(bool superTiles)
    {
        string folder = Path.Combine(Path.GetTempPath(), $"msxsuper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var main = new MainWindowViewModel(new TestDialogService());

            TileSet tileSet = superTiles ? WithSuperTiles(2, 2, count: 2) : new TileSet("Bosque");

            main.OpenTileSet(tileSet);

            await TestExport.TileSetAsync(main, ExportFormat.Binary, Path.Combine(folder, "bosque.bin"));

            Assert.True(File.Exists(Path.Combine(folder, "bosque_patterns.bin")));
            Assert.Equal(superTiles, File.Exists(Path.Combine(folder, "bosque_supertiles.bin")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Un juego con sus supertiles ya puestos.</summary>
    private static TileSet WithSuperTiles(int width, int height, int count)
    {
        var tileSet = new TileSet("Bosque") { SuperTileWidth = width, SuperTileHeight = height };

        for (int index = 0; index < count; index++)
        {
            var block = new TileBlock($"S{index}")
            {
                Width = width,
                Height = height,
                [0, 0] = index % TileSet.TileCount,
            };

            tileSet.Blocks.Add(block);
        }

        return tileSet;
    }

    /// <summary>Los bytes de las líneas .db del asm, sin comentarios ni etiquetas.</summary>
    private static byte[] BytesOf(string asm) =>
    [
        .. asm.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(SpriteBankExporter.DataDirective, StringComparison.Ordinal))
            .SelectMany(line => line[SpriteBankExporter.DataDirective.Length..]
                .Split(';')[0]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Convert.ToByte(value.Replace(SpriteBankExporter.HexPrefix, string.Empty), 16))),
    ];
}
