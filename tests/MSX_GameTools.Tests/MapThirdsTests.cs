using MSX_GameTools.Entities;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Un juego de tiles por cada tercio de la pantalla.
/// </summary>
/// <remarks>
/// En GRAPHIC 2 la tabla de patrones son tres bancos de 256, y el tercio en el que cae una fila
/// es lo que decide cuál de los tres la dibuja. Un mapa que quepa en una pantalla puede
/// aprovecharlo —los cañones apuntando hacia abajo en la banda de arriba y hacia arriba en la de
/// abajo—, y uno más alto no, porque al desplazarse en vertical sus filas van cambiando de
/// tercio.
/// </remarks>
public class MapThirdsTests
{
    /// <summary>Cuántos juegos admite un mapa sale de lo alto que sea.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 3)]
    [InlineData(24, 3)]
    public void Los_tercios_salen_del_alto_del_mapa(int height, int thirds) =>
        Assert.Equal(thirds, Map(height).Thirds);

    /// <summary>
    /// Y un mapa más alto que la pantalla no se reparte.
    /// </summary>
    /// <remarks>
    /// Los tercios son de la pantalla y no del mapa: en cuanto hay desplazamiento vertical, una
    /// misma fila del mapa cae en un tercio distinto según por dónde vaya la pantalla, así que
    /// no queda banda que atar a un banco.
    /// </remarks>
    [Theory]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(200)]
    public void Un_mapa_mas_alto_que_la_pantalla_no_se_reparte(int height) =>
        Assert.Equal(1, Map(height).Thirds);

    /// <summary>
    /// En screen 1 y con supertiles sólo cabe uno, por mucho que el mapa dé para tres.
    /// </summary>
    /// <remarks>
    /// En GRAPHIC 1 hay una sola tabla de patrones para toda la pantalla, así que no hay bancos
    /// que distinguir. Y con supertiles la celda es un bloque de su juego: tres juegos serían
    /// tres numeraciones solapadas.
    /// </remarks>
    [Fact]
    public void En_screen_1_y_con_supertiles_solo_cabe_un_juego()
    {
        TileMap map = Map(24);

        Assert.Equal(3, map.TileSetSlots(new TileSet("Bosque")));
        Assert.Equal(1, map.TileSetSlots(new TileSet("Marcador", TileSet.GraphicMode.Graphic1)));

        var ciudad = new TileSet("Ciudad");
        ciudad.UseSuperTiles(2, 2);

        Assert.Equal(1, map.TileSetSlots(ciudad));
    }

    /// <summary>Cada tercio se dibuja con el suyo.</summary>
    [Theory]
    [InlineData(0, "Arriba")]
    [InlineData(7, "Arriba")]
    [InlineData(8, "Medio")]
    [InlineData(15, "Medio")]
    [InlineData(16, "Abajo")]
    [InlineData(23, "Abajo")]
    public void Cada_tercio_se_dibuja_con_su_juego(int row, string expected)
    {
        TileMap map = Map(24);

        map.UseTileSets([Ref("Arriba"), Ref("Medio"), Ref("Abajo")]);

        Assert.Equal(expected, map.TileSetFor(row).Name);
    }

    /// <summary>Con uno solo los tres tercios son el mismo, que es lo que hace hoy cualquier mapa.</summary>
    [Fact]
    public void Con_un_solo_juego_los_tres_tercios_son_el_mismo()
    {
        TileMap map = Map(24);

        map.UseTileSets([Ref("Bosque")]);

        foreach (int row in (int[])[0, 9, 23])
            Assert.Equal("Bosque", map.TileSetFor(row).Name);
    }

    /// <summary>El tercio que no se llegó a elegir tira del primero.</summary>
    [Fact]
    public void El_tercio_sin_elegir_usa_el_primero()
    {
        TileMap map = Map(24);

        map.UseTileSets([Ref("Arriba"), Ref("Medio")]);

        Assert.Equal("Medio", map.TileSetFor(8).Name);
        Assert.Equal("Arriba", map.TileSetFor(16).Name);
    }

    /// <summary>
    /// Un mapa alto lo dibuja todo con el primero aunque tenga tres guardados.
    /// </summary>
    /// <remarks>
    /// Pasa al agrandar uno que ya los tenía elegidos. No se le borran —encoger y volver a
    /// crecer no tiene por qué costar volver a elegirlos—, pero mientras no quepa en una
    /// pantalla no reparte nada.
    /// </remarks>
    [Fact]
    public void Un_mapa_alto_dibuja_todo_con_el_primero()
    {
        TileMap map = Map(30);

        map.UseTileSets([Ref("Arriba"), Ref("Medio"), Ref("Abajo")]);

        foreach (int row in (int[])[0, 10, 20, 29])
            Assert.Equal("Arriba", map.TileSetFor(row).Name);
    }

    /// <summary>Y encoger el mapa no se lleva por delante lo elegido.</summary>
    [Fact]
    public void Encoger_el_mapa_no_olvida_los_juegos_de_los_tercios()
    {
        TileMap map = Map(24);

        map.UseTileSets([Ref("Arriba"), Ref("Medio"), Ref("Abajo")]);

        map.Resize(32, 8);

        Assert.Equal(1, map.Thirds);
        Assert.Equal("Arriba", map.TileSetFor(7).Name);

        map.Resize(32, 24);

        Assert.Equal("Abajo", map.TileSetFor(16).Name);
    }

    /// <summary>
    /// El primer juego sigue siendo el de siempre.
    /// </summary>
    /// <remarks>
    /// Todo lo que ata un mapa a su juego —abrirlo, buscar los mapas de un juego, renombrar—
    /// pasa por TileSetId, así que escribirlo tiene que seguir valiendo y no puede llevarse por
    /// delante los otros dos tercios.
    /// </remarks>
    [Fact]
    public void Escribir_el_juego_de_siempre_no_toca_los_otros_tercios()
    {
        TileMap map = Map(24);

        map.UseTileSets([Ref("Arriba"), Ref("Medio"), Ref("Abajo")]);

        var otro = Guid.NewGuid();

        map.TileSetId = otro;
        map.TileSetName = "Otro";

        Assert.Equal(otro, map.TileSets[0].Id);
        Assert.Equal("Otro", map.TileSetFor(0).Name);

        Assert.Equal(3, map.TileSets.Count);
        Assert.Equal("Medio", map.TileSetFor(8).Name);
    }

    /// <summary>Un mapa recién hecho tiene un hueco, y vacío.</summary>
    [Fact]
    public void Un_mapa_nuevo_tiene_un_solo_juego_sin_estrenar()
    {
        TileMap map = Map(24);

        Assert.Single(map.TileSets);
        Assert.Equal(Guid.Empty, map.TileSetId);
        Assert.Equal(string.Empty, map.TileSetName);
    }

    /// <summary>No hay cuarto tercio: lo que pase de tres se queda fuera.</summary>
    [Fact]
    public void No_caben_mas_de_tres()
    {
        TileMap map = Map(24);

        map.UseTileSets([Ref("Arriba"), Ref("Medio"), Ref("Abajo"), Ref("Sobra")]);

        Assert.Equal(3, map.TileSets.Count);
        Assert.DoesNotContain(map.TileSets, tileSet => tileSet.Name == "Sobra");
    }

    // ------------------------------------------------------------------ los andamios

    private static TileMap Map(int height) => new("Nivel", 32, height);

    /// <summary>Un juego cualquiera: aquí sólo hace falta que se distinga de los otros.</summary>
    private static TileSetRef Ref(string name) => new(Guid.NewGuid(), name);
}
