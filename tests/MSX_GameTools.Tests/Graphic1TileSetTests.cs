using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Juegos de tiles de screen 1, donde el color es de cada ocho tiles y no de cada línea.
/// </summary>
/// <remarks>
/// <para>
/// En GRAPHIC 1 la tabla de colores tiene 32 bytes para los 256 patrones: uno por cada ocho,
/// con el frente en el nibble alto y el fondo en el bajo. El dibujo se guarda igual que en
/// GRAPHIC 2 —ocho bytes de máscara— y lo único que cambia es de quién es el color.
/// </para>
/// <para>
/// El par sigue bajando a las líneas de sus ocho tiles, y eso no es un apaño: es lo que hace
/// que todo lo que ya sabía dibujar un tile —el lienzo, las miniaturas, el mapa, los bloques,
/// el png— siga funcionando sin enterarse de que existe el screen 1. Lo que cambia es quién
/// escribe esos colores, y en este modo el único que los escribe es el grupo.
/// </para>
/// </remarks>
public class Graphic1TileSetTests
{
    private static TileSet Graphic1(string name = "Mazmorra") =>
        new(name, TileSet.GraphicMode.Graphic1);

    private static void Draw(TileSet tileSet, int index, byte pattern)
    {
        foreach (TileRow row in tileSet.ListOfTiles[index].ArrayTileRows)
        {
            for (int column = 0; column < TileRow.Columns; column++)
                row.ArrayPattern[column] = (pattern & (1 << column)) != 0;
        }
    }

    // ------------------------------------------------------------------ el modelo

    /// <summary>Los 32 grupos existen desde que nace el juego, como la tabla del VDP.</summary>
    [Fact]
    public void Un_juego_tiene_treinta_y_dos_grupos_de_color()
    {
        TileSet tileSet = Graphic1();

        Assert.Equal(32, tileSet.ColorGroups.Count);
        Assert.Equal(TileSet.TileCount, tileSet.ColorGroups.Count * TileSet.ColorGroupSize);
    }

    /// <summary>Cada grupo dice a qué tiles pinta, que es lo que se lee a su lado.</summary>
    [Theory]
    [InlineData(0, 0, 7, "0-7")]
    [InlineData(1, 8, 15, "8-15")]
    [InlineData(31, 248, 255, "248-255")]
    public void Cada_grupo_sabe_a_que_tiles_pinta(int index, int first, int last, string range)
    {
        TileSet tileSet = Graphic1();
        TileColorGroup group = tileSet.ColorGroups[index];

        Assert.Equal(first, group.FirstTile);
        Assert.Equal(last, group.LastTile);
        Assert.Equal(range, group.Range);
    }

    /// <summary>Y un tile sabe de qué grupo cuelga por el hueco en el que está.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(255, 31)]
    public void Un_tile_cuelga_del_grupo_de_su_hueco(int tile, int group)
    {
        TileSet tileSet = Graphic1();

        Assert.Same(tileSet.ColorGroups[group], tileSet.GroupOf(tile));
    }

    /// <summary>
    /// Pintar un grupo pinta sus ocho tiles y no toca a los demás.
    /// </summary>
    /// <remarks>
    /// Es la limitación del modo puesta en el modelo: no se puede dar color a un tile solo. Y
    /// es lo que hace que el hueco donde pones un dibujo sea una decisión y no sólo un número.
    /// </remarks>
    [Fact]
    public void Pintar_un_grupo_pinta_sus_ocho_tiles()
    {
        TileSet tileSet = Graphic1();

        tileSet.ColorGroups[1].Set(foreColor: 4, backColor: 12);

        for (int tile = 8; tile <= 15; tile++)
        {
            Assert.All(
                tileSet.ListOfTiles[tile].ArrayTileRows,
                row =>
                {
                    Assert.Equal(4, row.ForeColor);
                    Assert.Equal(12, row.BackColor);
                });
        }

        // El de al lado se queda como estaba.
        Assert.Equal(15, tileSet.ListOfTiles[7].ArrayTileRows[0].ForeColor);
        Assert.Equal(15, tileSet.ListOfTiles[16].ArrayTileRows[0].ForeColor);
    }

    /// <summary>El byte del grupo es el que va al VDP: frente arriba y fondo abajo.</summary>
    [Fact]
    public void El_byte_del_grupo_lleva_los_dos_nibbles()
    {
        TileSet tileSet = Graphic1();

        tileSet.ColorGroups[0].Set(foreColor: 4, backColor: 12);

        Assert.Equal(0x4C, tileSet.ColorGroups[0].ColorByte);
    }

    /// <summary>
    /// Estampar un trozo deja el dibujo pero el color lo pone el grupo de destino.
    /// </summary>
    /// <remarks>
    /// Es la consecuencia directa de que el color sea del hueco: un tile traído de otro sitio
    /// se pinta con el par de donde cae, se dibujara con los colores que se dibujara. Si el
    /// dibujo se llevara sus colores, el juego tendría tiles con colores que su tabla no dice.
    /// </remarks>
    [Fact]
    public void Estampar_deja_el_dibujo_pero_el_color_lo_pone_el_destino()
    {
        TileSet origin = Graphic1("Bosque");
        Draw(origin, 3, 0b1010_1010);
        origin.ColorGroups[0].Set(foreColor: 2, backColor: 3);

        TileSet target = Graphic1("Cueva");
        target.ColorGroups[1].Set(foreColor: 6, backColor: 7);

        // El tile 3 del origen cae en el 8 del destino, que es del grupo 1.
        target.Stamp(8, 0, origin.Copy(3, 0, 1, 1));

        Assert.Equal(
            origin.ListOfTiles[3].ArrayTileRows[0].PatternByte,
            target.ListOfTiles[8].ArrayTileRows[0].PatternByte);

        Assert.All(
            target.ListOfTiles[8].ArrayTileRows,
            row =>
            {
                Assert.Equal(6, row.ForeColor);
                Assert.Equal(7, row.BackColor);
            });
    }

    /// <summary>Mover los colores de sitio en la paleta también mueve los del grupo.</summary>
    /// <remarks>
    /// Sin esto el grupo se quedaba con el índice viejo y el siguiente repintado —cualquier
    /// cosa que tocara sus tiles— devolvía los colores de antes de reordenar la paleta.
    /// </remarks>
    [Fact]
    public void Reordenar_la_paleta_mueve_los_colores_del_grupo()
    {
        TileSet tileSet = Graphic1();
        tileSet.ColorGroups[0].Set(foreColor: 4, backColor: 12);

        // Una tabla que manda el 4 al 9 y deja el resto donde está.
        int[] table = [.. Enumerable.Range(0, ColorPalette.Size)];
        table[4] = 9;

        tileSet.RemapColors(table);

        Assert.Equal(9, tileSet.ColorGroups[0].ForeColor);
        Assert.Equal(9, tileSet.ListOfTiles[0].ArrayTileRows[0].ForeColor);
    }

    // ------------------------------------------------------------------ el fichero

    /// <summary>El modo y la tabla de colores sobreviven a guardar y volver a abrir.</summary>
    [Fact]
    public void El_modo_y_los_colores_van_y_vuelven()
    {
        TileSet tileSet = Graphic1();
        Draw(tileSet, 10, 0b1100_0011);
        tileSet.ColorGroups[1].Set(foreColor: 4, backColor: 12);

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());
        TileSet read = TileSetSerializer.Deserialize(json).TileSet;

        Assert.Equal(TileSet.GraphicMode.Graphic1, read.Mode);
        Assert.True(read.IsGraphic1);
        Assert.Equal(4, read.ColorGroups[1].ForeColor);
        Assert.Equal(12, read.ColorGroups[1].BackColor);

        // Y el par ha bajado a los tiles del grupo, que es lo que se pinta.
        Assert.Equal(4, read.ListOfTiles[10].ArrayTileRows[0].ForeColor);
        Assert.Equal(
            tileSet.ListOfTiles[10].ArrayTileRows[0].PatternByte,
            read.ListOfTiles[10].ArrayTileRows[0].PatternByte);
    }

    /// <summary>
    /// Elegir un color no da por dibujados los 256 tiles.
    /// </summary>
    /// <remarks>
    /// El fichero sólo escribe los tiles que se han tocado. Como pintar un grupo cambia los
    /// colores de sus ocho tiles, mirar los colores para decidir si un tile está tocado daba
    /// los 256 por tocados en cuanto se elegía un color, y el fichero pasaba a ser los
    /// veintitantos kilobytes de ceros que el formato disperso evita.
    /// </remarks>
    [Fact]
    public void Pintar_un_grupo_no_llena_el_fichero_de_tiles_vacios()
    {
        TileSet tileSet = Graphic1();

        foreach (TileColorGroup group in tileSet.ColorGroups)
            group.Set(foreColor: 4, backColor: 12);

        Draw(tileSet, 10, 0b1100_0011);

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());
        TileSet read = TileSetSerializer.Deserialize(json).TileSet;

        // Un solo tile dibujado: el fichero no puede traer los 256.
        Assert.Equal(1, CountTilesInFile(json));

        // Y aun así los 256 salen con el color de su grupo, porque lo pone la tabla.
        Assert.Equal(4, read.ListOfTiles[200].ArrayTileRows[0].ForeColor);
    }

    private static int CountTilesInFile(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);

        return document.RootElement.GetProperty("tiles").GetArrayLength();
    }

    /// <summary>Un juego de GRAPHIC 2 sigue guardándose y abriéndose como siempre.</summary>
    [Fact]
    public void Un_juego_de_graphic2_no_cambia()
    {
        var tileSet = new TileSet("Bosque");
        Draw(tileSet, 3, 0b1010_1010);
        tileSet.ListOfTiles[3].ArrayTileRows[0].ForeColor = 7;

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard());
        TileSet read = TileSetSerializer.Deserialize(json).TileSet;

        Assert.Equal(TileSet.GraphicMode.Graphic2, read.Mode);
        Assert.False(read.IsGraphic1);

        // Aquí el color sí es de la línea, y sólo de esa.
        Assert.Equal(7, read.ListOfTiles[3].ArrayTileRows[0].ForeColor);
        Assert.Equal(15, read.ListOfTiles[3].ArrayTileRows[1].ForeColor);
    }

    // ------------------------------------------------------------------ la exportación

    /// <summary>La tabla de colores mide 32 bytes, no 2048.</summary>
    /// <remarks>
    /// No es la misma tabla más corta: es otra tabla, con un byte por cada ocho tiles en vez
    /// de uno por línea. Es la diferencia que define el modo.
    /// </remarks>
    [Fact]
    public void La_tabla_de_colores_mide_treinta_y_dos_bytes()
    {
        TileSet tileSet = Graphic1();
        tileSet.ColorGroups[0].Set(foreColor: 4, backColor: 12);
        tileSet.ColorGroups[31].Set(foreColor: 1, backColor: 2);

        byte[] colors = TileSetExporter.ColorsToBinary(tileSet);

        Assert.Equal(TileSet.ColorGroupCount, colors.Length);
        Assert.Equal(0x4C, colors[0]);
        Assert.Equal(0x12, colors[31]);
    }

    /// <summary>Y la de patrones sigue midiendo lo mismo: el dibujo no cambia entre modos.</summary>
    [Fact]
    public void La_tabla_de_patrones_no_cambia()
    {
        TileSet tileSet = Graphic1();
        Draw(tileSet, 0, 0b1010_1010);

        Assert.Equal(TileSetExporter.TableBytes, TileSetExporter.PatternsToBinary(tileSet).Length);
    }

    /// <summary>El asm de colores dice a qué tiles pinta cada byte.</summary>
    /// <remarks>
    /// El byte 3 no dice por sí solo que pinta los tiles 24 a 31, y equivocarse de grupo
    /// repinta ocho tiles que estaban bien.
    /// </remarks>
    [Fact]
    public void El_asm_de_colores_dice_el_rango_de_cada_byte()
    {
        TileSet tileSet = Graphic1();
        tileSet.ColorGroups[3].Set(foreColor: 4, backColor: 12);

        string asm = TileSetExporter.ColorsToAssembler(tileSet);

        Assert.Contains("; tiles 24-31", asm);
        Assert.Contains("; tiles 0-7", asm);
        Assert.Contains("GRAPHIC 1", asm);

        // 32 bytes de datos, uno por grupo.
        Assert.Equal(
            TileSet.ColorGroupCount,
            asm.Split('\n').Count(line => line.Contains("; tiles ")));
    }

    /// <summary>
    /// Y el asm de patrones no dice que se copie tres veces.
    /// </summary>
    /// <remarks>
    /// Los tres tercios son de GRAPHIC 2 y 3. En screen 1 hay una sola tabla para toda la
    /// pantalla, y copiarla tres veces son cuatro kilobytes de VRAM tirados.
    /// </remarks>
    [Fact]
    public void El_asm_de_patrones_no_habla_de_los_tres_tercios()
    {
        TileSet tileSet = Graphic1();

        string asm = TileSetExporter.PatternsToAssembler(tileSet);

        Assert.DoesNotContain("one per screen third", asm);
        Assert.Contains("copy it once", asm);

        // Y en GRAPHIC 2 se sigue diciendo, que allí sí son tres.
        Assert.Contains("one per screen third", TileSetExporter.PatternsToAssembler(new TileSet("Bosque")));
    }
}
