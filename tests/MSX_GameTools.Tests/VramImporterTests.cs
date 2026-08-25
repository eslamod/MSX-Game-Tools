using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Sacar tiles y sprites de un volcado de VRAM de openMSX.
/// </summary>
/// <remarks>
/// Las pruebas van de ida y vuelta: se exporta con el exportador de verdad, se mete lo
/// exportado en una VRAM falsa donde va, y se importa. Así no se comprueba contra mi idea del
/// formato del VDP sino contra la del programa, que es la que ya está validada con sasSX y en
/// openMSX. Si alguna de las dos direcciones se tuerce, la vuelta no cuadra.
/// </remarks>
public class VramImporterTests
{
    private const int VramSize = 16384;

    /// <summary>Lo que se exporta vuelve igual: dibujo y los dos colores de cada línea.</summary>
    [AvaloniaFact]
    public void Un_juego_de_tiles_vuelve_como_estaba()
    {
        TileSet original = Painted();

        byte[] vram = WithTiles(original, original, original);

        TileSet back = Assert.Single(VramImporter.Read(vram, new VramImporter.VramLayout()).TileSets);

        AssertSame(original, back);
    }

    /// <summary>
    /// Con los tres tercios iguales sale un juego, no tres.
    /// </summary>
    /// <remarks>
    /// Es lo corriente: muchos juegos repiten la misma tabla en los tres tercios de la
    /// pantalla, y traerlos por separado daría tres juegos idénticos que hay que borrar a mano.
    /// </remarks>
    [AvaloniaFact]
    public void Tres_tercios_iguales_dan_un_solo_juego()
    {
        TileSet one = Painted();

        Assert.Single(VramImporter.Read(WithTiles(one, one, one), new VramImporter.VramLayout()).TileSets);
    }

    /// <summary>Y si son distintos, tres.</summary>
    [AvaloniaFact]
    public void Tres_tercios_distintos_dan_tres_juegos()
    {
        TileSet first = Painted();
        TileSet second = Painted(shift: 1);
        TileSet third = Painted(shift: 2);

        IReadOnlyList<TileSet> sets =
            VramImporter.Read(WithTiles(first, second, third), new VramImporter.VramLayout()).TileSets;

        Assert.Equal(3, sets.Count);

        AssertSame(first, sets[0]);
        AssertSame(second, sets[1]);
        AssertSame(third, sets[2]);
    }

    /// <summary>
    /// Un tercio que repite el dibujo pero le cambia el color también cuenta como distinto.
    /// </summary>
    /// <remarks>
    /// Comparando sólo los patrones, este caso daría un único juego y se perdería el color de
    /// dos tercios enteros. En screen 2 el color es tan del tile como el dibujo.
    /// </remarks>
    [AvaloniaFact]
    public void Un_tercio_con_otro_color_ya_es_distinto()
    {
        TileSet first = Painted();
        TileSet repainted = Painted();

        repainted.ListOfTiles[0].ArrayTileRows[0].ForeColor = 3;

        Assert.Equal(
            3,
            VramImporter.Read(WithTiles(first, repainted, first), new VramImporter.VramLayout()).TileSets.Count);
    }

    /// <summary>Los patrones de sprite también vuelven como estaban.</summary>
    [AvaloniaFact]
    public void Los_patrones_de_sprite_vuelven_como_estaban()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        // Un dibujo que no sea simétrico: una esquina puesta y su opuesta no, que si se
        // leyeran las mitades al revés esta prueba pasaría igual.
        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;
        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[15] = false;
        bank.SpritesList[0].ArraySpriteRows[15].ArrayColumns[9] = true;
        bank.SpritesList[3].ArraySpriteRows[7].ArrayColumns[8] = true;

        var vram = new byte[VramSize];
        var layout = new VramImporter.VramLayout();

        SpriteBankExporter.PatternsToBinary(bank, 0, 3)
            .CopyTo(vram, layout.SpritePatterns);

        SpriteBank back = VramImporter.Read(vram, layout).Sprites!;

        foreach (int index in (int[])[0, 3])
        {
            for (int row = 0; row < Sprite.Rows; row++)
            {
                Assert.Equal(
                    bank.SpritesList[index].ArraySpriteRows[row].ArrayColumns,
                    back.SpritesList[index].ArraySpriteRows[row].ArrayColumns);
            }
        }
    }

    /// <summary>
    /// Una tabla que no cabe en el volcado se deja fuera y se dice, sin perder lo demás.
    /// </summary>
    /// <remarks>
    /// Un volcado corto —o unas direcciones que no son las de esa ROM— no puede tirar la
    /// importación entera: los tiles que sí están siguen valiendo.
    /// </remarks>
    [AvaloniaFact]
    public void Lo_que_no_cabe_se_dice_y_lo_demas_se_trae()
    {
        TileSet one = Painted();

        byte[] whole = WithTiles(one, one, one);

        // Justo hasta donde acaba la tabla de colores: los tiles caben enteros y los sprites,
        // que empiezan en 3800H, se quedan fuera.
        var layout = new VramImporter.VramLayout();
        byte[] cut = whole[..(layout.Colors + VramImporter.TableBytes)];

        VramImporter.VramImport import = VramImporter.Read(cut, layout);

        Assert.Single(import.TileSets);
        Assert.Single(import.Problems);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un juego con algo dibujado y colores que no son los de partida.</summary>
    private static TileSet Painted(int shift = 0)
    {
        var tileSet = new TileSet("Bosque");

        for (int index = 0; index < TileSet.TileCount; index++)
        {
            for (int row = 0; row < Tile.Rows; row++)
            {
                TileRow line = tileSet.ListOfTiles[index].ArrayTileRows[row];

                line.ArrayPattern[(index + row + shift) % TileRow.Columns] = true;
                line.ForeColor = ((index + shift) % 15) + 1;
                line.BackColor = (row + shift) % 16;
            }
        }

        return tileSet;
    }

    /// <summary>Una VRAM con los tres tercios de patrones y de colores puestos donde van.</summary>
    private static byte[] WithTiles(TileSet first, TileSet second, TileSet third)
    {
        var vram = new byte[VramSize];
        var layout = new VramImporter.VramLayout();

        foreach ((TileSet set, int index) in new[] { first, second, third }.Select((s, i) => (s, i)))
        {
            TileSetExporter.PatternsToBinary(set)
                .CopyTo(vram, layout.Patterns + (index * VramImporter.ThirdBytes));

            TileSetExporter.ColorsToBinary(set)
                .CopyTo(vram, layout.Colors + (index * VramImporter.ThirdBytes));
        }

        return vram;
    }

    private static void AssertSame(TileSet expected, TileSet actual)
    {
        for (int index = 0; index < TileSet.TileCount; index++)
        {
            for (int row = 0; row < Tile.Rows; row++)
            {
                TileRow one = expected.ListOfTiles[index].ArrayTileRows[row];
                TileRow other = actual.ListOfTiles[index].ArrayTileRows[row];

                Assert.Equal(one.ArrayPattern, other.ArrayPattern);
                Assert.Equal((one.ForeColor, one.BackColor), (other.ForeColor, other.BackColor));
            }
        }
    }
}
