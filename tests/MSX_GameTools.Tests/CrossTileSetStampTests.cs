using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Llevarse un trozo de tiles de un juego a otro.
/// </summary>
/// <remarks>
/// <para>
/// Sin botón nuevo ni menú: el gesto ya existía —marcar aquí, estampar allí— y lo único que
/// le faltaba era sobrevivir al cambio de pestaña. Lo que se marcó se congela al dejar el
/// juego, no al marcarlo, para no cambiar nada de lo que ya funcionaba dentro de uno.
/// </para>
/// <para>
/// Los tiles son índices de color, así que lo que cruza son los índices y no los colores:
/// el mismo tile en un juego con otra paleta se ve de otro color. Es lo que significa «el
/// mismo tile» a nivel de bytes.
/// </para>
/// </remarks>
public class CrossTileSetStampTests
{
    /// <summary>Marca el tile con un patrón reconocible, para saber cuál ha llegado.</summary>
    private static void Draw(TileSet tileSet, int index, byte pattern)
    {
        foreach (TileRow row in tileSet.ListOfTiles[index].ArrayTileRows)
        {
            for (int column = 0; column < TileRow.Columns; column++)
                row.ArrayPattern[column] = (pattern & (1 << column)) != 0;
        }
    }

    private static byte PatternOf(TileSet tileSet, int index) =>
        tileSet.ListOfTiles[index].ArrayTileRows[0].PatternByte;

    /// <summary>Lo que pidió el usuario: marcar en un juego y soltarlo en otro.</summary>
    [AvaloniaFact]
    public void Lo_marcado_en_un_juego_se_estampa_en_otro()
    {
        var main = new MainWindowViewModel();

        var origin = new TileSet("Bosque");
        Draw(origin, 3, 0b1010_1010);

        TileSetEditorViewModel from = main.OpenTileSet(origin);
        TileSetEditorViewModel to = main.OpenTileSet(new TileSet("Cueva"));

        main.SelectedTab = from;
        from.SelectRegion(3, 0, 1, 1);

        // El cambio de pestaña es lo que se lleva lo marcado.
        main.SelectedTab = to;

        to.StampAt(10, 0);

        Assert.Equal(PatternOf(origin, 3), PatternOf(to.TileSet, 10));
    }

    /// <summary>
    /// Lo marcado en el juego de delante manda sobre lo traído.
    /// </summary>
    /// <remarks>
    /// Es lo que deja intacto el comportamiento de siempre: dentro de un juego, lo que cae
    /// es lo que se está viendo marcado, aunque se venga de otro con algo en la mano.
    /// </remarks>
    [AvaloniaFact]
    public void Lo_de_aqui_manda_sobre_lo_traido()
    {
        var main = new MainWindowViewModel();

        var origin = new TileSet("Bosque");
        Draw(origin, 3, 0b1010_1010);

        var target = new TileSet("Cueva");
        Draw(target, 5, 0b1111_0000);

        TileSetEditorViewModel from = main.OpenTileSet(origin);
        TileSetEditorViewModel to = main.OpenTileSet(target);

        main.SelectedTab = from;
        from.SelectRegion(3, 0, 1, 1);

        main.SelectedTab = to;
        to.SelectRegion(5, 0, 1, 1);

        to.StampAt(10, 0);

        Assert.Equal(PatternOf(target, 5), PatternOf(target, 10));
    }

    /// <summary>
    /// Lo que se lleva es una copia: retocar el origen después no cambia lo que se suelta.
    /// </summary>
    /// <remarks>
    /// Dentro de un juego es al revés a propósito —lo que cae es lo que se está viendo—,
    /// pero eso deja de poder cumplirse en cuanto el origen no está delante. Aquí lo
    /// honrado es soltar lo último que se vio.
    /// </remarks>
    [AvaloniaFact]
    public void Lo_que_se_lleva_es_una_copia_del_momento()
    {
        var main = new MainWindowViewModel();

        var origin = new TileSet("Bosque");
        Draw(origin, 3, 0b1010_1010);

        TileSetEditorViewModel from = main.OpenTileSet(origin);
        TileSetEditorViewModel to = main.OpenTileSet(new TileSet("Cueva"));

        main.SelectedTab = from;
        from.SelectRegion(3, 0, 1, 1);

        main.SelectedTab = to;

        // Lo que se llevo, leido antes de tocar el origen: el byte no es el patron que se
        // dibujo, porque la columna 0 va en el bit mas alto.
        byte taken = PatternOf(origin, 3);

        // El origen cambia despues de habernoslo traido.
        Draw(origin, 3, 0b0000_1111);

        to.StampAt(10, 0);

        Assert.Equal(taken, PatternOf(to.TileSet, 10));
        Assert.NotEqual(taken, PatternOf(origin, 3));
    }

    /// <summary>Un rectángulo entero, no sólo un tile.</summary>
    [AvaloniaFact]
    public void Se_lleva_el_rectangulo_entero()
    {
        var main = new MainWindowViewModel();

        var origin = new TileSet("Bosque");

        Draw(origin, 0, 0b0000_0001);
        Draw(origin, 1, 0b0000_0010);
        Draw(origin, TileSet.Columns, 0b0000_0100);
        Draw(origin, TileSet.Columns + 1, 0b0000_1000);

        TileSetEditorViewModel from = main.OpenTileSet(origin);
        TileSetEditorViewModel to = main.OpenTileSet(new TileSet("Cueva"));

        main.SelectedTab = from;
        from.SelectRegion(0, 0, 2, 2);

        main.SelectedTab = to;

        // Los cuatro de origen leidos del propio juego: comparar contra el literal que se
        // dibujo no vale, porque el byte lleva la columna 0 en el bit mas alto.
        byte[] expected =
        [
            PatternOf(origin, 0),
            PatternOf(origin, 1),
            PatternOf(origin, TileSet.Columns),
            PatternOf(origin, TileSet.Columns + 1),
        ];

        to.StampAt(4, 1);

        int corner = (1 * TileSet.Columns) + 4;

        Assert.Equal(expected[0], PatternOf(to.TileSet, corner));
        Assert.Equal(expected[1], PatternOf(to.TileSet, corner + 1));
        Assert.Equal(expected[2], PatternOf(to.TileSet, corner + TileSet.Columns));
        Assert.Equal(expected[3], PatternOf(to.TileSet, corner + TileSet.Columns + 1));

        // Y son cuatro distintos, que si no esto pasaria con cualquier cosa que se copiara.
        Assert.Equal(4, expected.Distinct().Count());
    }

    /// <summary>
    /// El fantasma bajo el ratón enseña lo traído.
    /// </summary>
    /// <remarks>
    /// Es lo que sostiene que esto no necesite botón: sin ver lo que se lleva en la mano,
    /// estampar en otro juego seria a ciegas. Las miniaturas van con el trozo copiado
    /// porque son del hueco de su juego, no del dibujo, y el trozo no las lleva.
    /// </remarks>
    [AvaloniaFact]
    public void El_fantasma_ensena_lo_que_se_trae()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel from = main.OpenTileSet(new TileSet("Bosque"));
        TileSetEditorViewModel to = main.OpenTileSet(new TileSet("Cueva"));

        main.SelectedTab = from;
        from.SelectRegion(0, 0, 2, 3);

        main.SelectedTab = to;

        Assert.True(to.HasStamp);
        Assert.Equal(2, to.StampWidth);
        Assert.Equal(6, to.StampPreview.Count);

        // Y son las del juego de origen, no las del de destino.
        Assert.All(to.StampPreview, mini => Assert.Contains(mini, from.Thumbnails));
    }

    /// <summary>Sin nada marcado en ninguna parte no hay nada que estampar.</summary>
    [AvaloniaFact]
    public void Sin_nada_marcado_no_se_estampa_nada()
    {
        var main = new MainWindowViewModel();

        var target = new TileSet("Cueva");
        Draw(target, 10, 0b0110_0110);

        TileSetEditorViewModel to = main.OpenTileSet(target);

        main.SelectedTab = to;
        to.StampAt(10, 0);

        Assert.Equal(0b0110_0110, PatternOf(target, 10));
        Assert.False(to.HasStamp);
    }
}
