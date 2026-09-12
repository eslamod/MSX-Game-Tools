using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El mapa como tabla de nombres: binario y asm.</summary>
public class MapExporterTests
{
    /// <summary>
    /// Cuatro bytes de cabecera, ancho y alto, byte bajo primero. Un mapa de diez
    /// pantallas de ancho son 320 columnas, que en little-endian son 40 01.
    /// </summary>
    [AvaloniaFact]
    public void La_cabecera_lleva_el_tamano_con_el_byte_bajo_primero()
    {
        byte[] bytes = MapExporter.ToBinary(new TileMap("Ancho", 320, 24));

        Assert.Equal(0x40, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
        Assert.Equal(24, bytes[2]);
        Assert.Equal(0, bytes[3]);

        Assert.Equal(MapExporter.HeaderBytes + (320 * 24), bytes.Length);
    }

    [AvaloniaFact]
    public void El_mapa_sale_por_filas_detras_de_la_cabecera()
    {
        var map = new TileMap("Nivel", 3, 2);

        map.Stamp(0, 0, 0, TilePatch.Single(10));
        map.Stamp(0, 2, 1, TilePatch.Single(20));

        byte[] bytes = MapExporter.ToBinary(map);

        Assert.Equal(10, bytes[MapExporter.HeaderBytes]);
        Assert.Equal(20, bytes[MapExporter.HeaderBytes + 5]);
    }

    /// <summary>En la máquina hay una tabla de nombres, no capas.</summary>
    [AvaloniaFact]
    public void Las_capas_salen_aplastadas()
    {
        var map = new TileMap("Nivel", 2, 1);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(7));
        map.Stamp(1, 0, 0, TilePatch.Single(8));

        Assert.Equal(8, MapExporter.ToBinary(map)[MapExporter.HeaderBytes]);
    }

    /// <summary>
    /// En un byte no cabe el hueco: los 256 valores son tiles y la tabla de nombres
    /// siempre dibuja algo. Donde el editor no tiene nada va el tile de relleno.
    /// </summary>
    [AvaloniaFact]
    public void Las_celdas_vacias_salen_con_el_tile_de_relleno()
    {
        var map = new TileMap("Nivel", 2, 1) { EmptyTile = 32 };

        map.Stamp(0, 0, 0, TilePatch.Single(5));

        byte[] bytes = MapExporter.ToBinary(map);

        Assert.Equal(5, bytes[MapExporter.HeaderBytes]);
        Assert.Equal(32, bytes[MapExporter.HeaderBytes + 1]);
    }

    /// <summary>
    /// El .asm tiene que traer exactamente los mismos bytes que el binario, cabecera
    /// incluida. Es la comprobación que nos ha salvado en los otros exportadores.
    /// </summary>
    [AvaloniaFact]
    public void El_asm_trae_los_mismos_bytes_que_el_binario()
    {
        var map = new TileMap("Nivel 1", 5, 3) { EmptyTile = 1 };

        map.Stamp(0, 0, 0, TilePatch.Single(200));
        map.Stamp(0, 4, 2, TilePatch.Single(255));

        Assert.Equal(MapExporter.ToBinary(map), BytesOf(MapExporter.ToAssembler(map)));
    }

    [AvaloniaFact]
    public void El_asm_lleva_etiqueta_y_final_para_medirlo()
    {
        string asm = MapExporter.ToAssembler(new TileMap("Nivel 1", 2, 2));

        Assert.Contains("nivel_1_map:", asm);
        Assert.Contains("nivel_1_map_end:", asm);
    }

    // ------------------------------------------------------------------ volver

    [AvaloniaFact]
    public void Ida_y_vuelta_devuelve_el_mismo_mapa()
    {
        var map = new TileMap("Nivel", 6, 4) { EmptyTile = 3 };

        map.Stamp(0, 1, 1, TilePatch.Single(100));

        TileMap back = MapExporter.FromBinary(MapExporter.ToBinary(map), "Vuelta");

        Assert.Equal((6, 4), (back.Width, back.Height));
        Assert.Equal(100, back.Layers[0].Grid[1, 1]);

        // Lo que estaba vacio vuelve con el tile de relleno: en el binario no hay huecos.
        Assert.Equal(3, back.Layers[0].Grid[0, 0]);
        Assert.Single(back.Layers);
    }

    [AvaloniaFact]
    public void Un_binario_mas_corto_que_la_cabecera_se_rechaza()
    {
        FileFormatException error = Assert.Throws<FileFormatException>(
            () => MapExporter.FromBinary([1, 2], "Malo"));

        Assert.Contains("cabecera", error.Message);
    }

    /// <summary>Un fichero cualquiera suele dar un tamaño imposible en los primeros bytes.</summary>
    [AvaloniaFact]
    public void Un_fichero_que_no_es_un_mapa_se_rechaza()
    {
        FileFormatException error = Assert.Throws<FileFormatException>(
            () => MapExporter.FromBinary([0, 0, 0, 0, 9, 9], "Malo"));

        Assert.Contains("entre 1 y", error.Message);
    }

    [AvaloniaFact]
    public void Un_binario_al_que_le_faltan_bytes_se_rechaza()
    {
        // Dice 4x4 pero solo trae dos celdas.
        FileFormatException error = Assert.Throws<FileFormatException>(
            () => MapExporter.FromBinary([4, 0, 4, 0, 1, 2], "Corto"));

        Assert.Contains("20 bytes", error.Message);
        Assert.Contains("tiene 6", error.Message);
    }

    /// <summary>
    /// El asm dice qué juego de tiles va en cada tercio de la pantalla.
    /// </summary>
    /// <remarks>
    /// La tabla de nombres no lo dice: el byte de una celda es el mismo 0-255 en los tres
    /// tercios, y en qué dibujo se convierte depende de la tabla de patrones que el VDP lea
    /// para ese tercio. Quien cargue el mapa tiene que poner cada tabla en su tercio, y esto es
    /// lo único que dice cuál va dónde.
    /// </remarks>
    [AvaloniaFact]
    public void El_asm_dice_que_juego_va_en_cada_tercio()
    {
        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([
            new TileSetRef(Guid.NewGuid(), "Cielo"),
            new TileSetRef(Guid.NewGuid(), "Ciudad"),
            new TileSetRef(Guid.NewGuid(), "Suelo")]);

        string asm = MapExporter.ToAssembler(map);

        Assert.Contains("rows 0-7: Cielo", asm);
        Assert.Contains("rows 8-15: Ciudad", asm);
        Assert.Contains("rows 16-23: Suelo", asm);
    }

    /// <summary>Y el último tercio se corta donde se acabe el mapa.</summary>
    [AvaloniaFact]
    public void El_ultimo_tercio_se_corta_donde_acaba_el_mapa()
    {
        var map = new TileMap("Nivel", 32, 20);

        map.UseTileSets([
            new TileSetRef(Guid.NewGuid(), "Cielo"),
            new TileSetRef(Guid.NewGuid(), "Ciudad"),
            new TileSetRef(Guid.NewGuid(), "Suelo")]);

        Assert.Contains("rows 16-19: Suelo", MapExporter.ToAssembler(map));
    }

    /// <summary>Un mapa de un solo juego no dice nada de tercios: no hay nada que repartir.</summary>
    [AvaloniaFact]
    public void Un_mapa_de_un_solo_juego_no_habla_de_tercios()
    {
        var map = new TileMap("Nivel", 32, 24) { TileSetName = "Bosque" };

        Assert.DoesNotContain("screen third", MapExporter.ToAssembler(map));
    }

    /// <summary>Los bytes de las líneas .db del asm, sin los comentarios.</summary>
    private static byte[] BytesOf(string assembler)
    {
        List<byte> bytes = [];

        foreach (string line in assembler.Split('\n'))
        {
            string trimmed = line.Trim();

            if (!trimmed.StartsWith(SpriteBankExporter.DataDirective, StringComparison.Ordinal))
                continue;

            string data = trimmed[SpriteBankExporter.DataDirective.Length..].Split(';')[0];

            foreach (string value in data.Split(',', StringSplitOptions.RemoveEmptyEntries))
                bytes.Add(Convert.ToByte(value.Trim()[SpriteBankExporter.HexPrefix.Length..], 16));
        }

        return [.. bytes];
    }
}
