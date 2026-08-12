using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Guardar y cargar un mapa.</summary>
public class MapFileTests
{
    [AvaloniaFact]
    public void Guardar_y_cargar_devuelve_el_mismo_mapa()
    {
        var map = new TileMap("Bosque", 6, 4) { TileSetName = "tileset1", BackgroundColorIndex = 4 };

        map.AddLayer("Decorado");

        map.Stamp(0, 0, 0, TilePatch.Single(10));
        map.Stamp(0, 5, 3, TilePatch.Single(200));
        map.Stamp(1, 2, 1, TilePatch.Single(0));

        map.Layers[1].IsVisible = false;
        map.Layers[1].IsLocked = true;

        TileMap loaded = MapSerializer.Deserialize(MapSerializer.Serialize(map));

        Assert.Equal("Bosque", loaded.Name);
        Assert.Equal((6, 4), (loaded.Width, loaded.Height));
        Assert.Equal("tileset1", loaded.TileSetName);
        Assert.Equal(4, loaded.BackgroundColorIndex);

        Assert.Equal(["Capa 1", "Decorado"], loaded.Layers.Select(layer => layer.Name));
        Assert.Equal(10, loaded.Layers[0].Grid[0, 0]);
        Assert.Equal(200, loaded.Layers[0].Grid[5, 3]);

        // El tile 0 tiene que volver como tile, no como hueco.
        Assert.Equal(0, loaded.Layers[1].Grid[2, 1]);
        Assert.Null(loaded.Layers[1].Grid[0, 0]);

        Assert.False(loaded.Layers[1].IsVisible);
        Assert.True(loaded.Layers[1].IsLocked);
    }

    /// <summary>
    /// Un mapa grande recién creado serían cientos de miles de celdas vacías escritas. Se
    /// guardan sólo las filas con algo, con su número delante.
    /// </summary>
    [AvaloniaFact]
    public void Solo_se_guardan_las_filas_con_algo()
    {
        var map = new TileMap("Bosque", 4, 100);

        map.Stamp(0, 0, 7, TilePatch.Single(3));

        string json = MapSerializer.Serialize(map);

        Assert.Contains("\"index\": 7", json);
        Assert.DoesNotContain("\"index\": 8", json);
        Assert.True(json.Length < 900, $"El fichero ocupa {json.Length}.");

        // Y al cargar, las filas que no venían siguen estando y vacías.
        TileMap loaded = MapSerializer.Deserialize(json);

        Assert.Equal(100, loaded.Layers[0].Grid.Height);
        Assert.Equal(3, loaded.Layers[0].Grid[0, 7]);
        Assert.Null(loaded.Layers[0].Grid[0, 8]);
    }

    /// <summary>
    /// Cargar no es editar: el mapa recién abierto no tiene nada que deshacer.
    /// </summary>
    /// <remarks>
    /// Hoy sale gratis porque el cargador escribe en la rejilla de cada capa y no llama a
    /// los métodos de edición del mapa. El test está para que siga siendo así: si algún
    /// día se carga estampando, cada fila del fichero se convertiría en un paso de
    /// deshacer y abrir un mapa dejaría veinte pasos falsos en la pila.
    /// </remarks>
    [AvaloniaFact]
    public void Un_mapa_recien_cargado_no_tiene_nada_que_deshacer()
    {
        var map = new TileMap("Bosque", 4, 4);
        map.Stamp(0, 0, 0, TilePatch.Single(1));

        TileMap loaded = MapSerializer.Deserialize(MapSerializer.Serialize(map));

        Assert.False(loaded.Undo.CanUndo);
        Assert.False(loaded.Undo.CanRedo);
    }

    [AvaloniaFact]
    public void Un_mapa_sin_capas_se_abre_con_una()
    {
        var map = new TileMap("Bosque", 4, 4);
        string json = MapSerializer.Serialize(map).Replace("\"layers\"", "\"otros\"");

        TileMap loaded = MapSerializer.Deserialize(json);

        Assert.Single(loaded.Layers);
    }

    [AvaloniaTheory]
    [InlineData("\"width\": 0", "entre 1 y")]
    [InlineData("\"width\": 9999", "entre 1 y")]
    [InlineData("\"version\": 99", "versión 99")]
    public void Un_mapa_imposible_se_rechaza_diciendo_que_pasa(string campo, string esperado)
    {
        string original = campo.Split(':')[0];
        string json = Regex.Replace(
            MapSerializer.Serialize(new TileMap("Bosque", 4, 4)),
            Regex.Escape(original) + @": *\d+",
            campo);

        FileFormatException error = Assert.Throws<FileFormatException>(() => MapSerializer.Deserialize(json));

        Assert.Contains(esperado, error.Message);
    }

    /// <summary>
    /// Una fila que no mide lo que mide el mapa deja las columnas descolocadas, así que se
    /// dice en vez de recortar en silencio.
    /// </summary>
    [AvaloniaFact]
    public void Una_fila_que_no_encaja_con_el_ancho_se_rechaza()
    {
        var map = new TileMap("Bosque", 4, 4);
        map.Stamp(0, 0, 0, TilePatch.Single(1));

        string json = MapSerializer.Serialize(map).Replace("\"01......\"", "\"0102\"");

        FileFormatException error = Assert.Throws<FileFormatException>(() => MapSerializer.Deserialize(json));

        Assert.Contains("2 celdas", error.Message);
        Assert.Contains("4 de ancho", error.Message);
    }

    [AvaloniaFact]
    public void Una_fila_fuera_del_mapa_se_rechaza()
    {
        var map = new TileMap("Bosque", 4, 4);
        map.Stamp(0, 0, 2, TilePatch.Single(1));

        string json = MapSerializer.Serialize(map).Replace("\"index\": 2", "\"index\": 40");

        FileFormatException error = Assert.Throws<FileFormatException>(() => MapSerializer.Deserialize(json));

        Assert.Contains("la fila 40", error.Message);
    }
}
