using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El mapa como csv: llevárselo a otra herramienta y traerlo.</summary>
public class MapCsvTests
{
    /// <summary>
    /// Sale aplastado, que es lo que hay en la máquina: una sola rejilla con la celda no
    /// vacía más alta de cada sitio.
    /// </summary>
    [AvaloniaFact]
    public void Las_capas_salen_aplastadas()
    {
        var map = new TileMap("Nivel", 3, 2);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(10));
        map.Stamp(0, 1, 0, TilePatch.Single(11));
        map.Stamp(1, 0, 0, TilePatch.Single(20));

        Assert.Equal("20,11,-1\n-1,-1,-1\n", MapCsv.Write(map));
    }

    /// <summary>El hueco es -1, que es lo que usa Tiled, y el tile 0 es un tile.</summary>
    [AvaloniaFact]
    public void El_hueco_se_escribe_menos_uno_y_el_tile_0_es_un_cero()
    {
        var map = new TileMap("Nivel", 2, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(0));

        Assert.Equal("0,-1\n", MapCsv.Write(map));
    }

    [AvaloniaFact]
    public void Ida_y_vuelta_devuelve_el_mismo_dibujo()
    {
        var map = new TileMap("Nivel", 5, 4);

        map.Stamp(0, 0, 0, TilePatch.Single(255));
        map.Stamp(0, 4, 3, TilePatch.Single(7));

        TileMap back = MapCsv.Read(MapCsv.Write(map), "Vuelta");

        Assert.Equal((5, 4), (back.Width, back.Height));
        Assert.Equal(255, back.Layers[0].Grid[0, 0]);
        Assert.Equal(7, back.Layers[0].Grid[4, 3]);
        Assert.Null(back.Layers[0].Grid[1, 1]);
    }

    /// <summary>Al volver hay una sola capa: el csv no las guarda.</summary>
    [AvaloniaFact]
    public void Lo_que_vuelve_tiene_una_sola_capa()
    {
        var map = new TileMap("Nivel", 2, 2);
        map.AddLayer();

        TileMap back = MapCsv.Read(MapCsv.Write(map), "Vuelta");

        Assert.Single(back.Layers);
        Assert.False(back.Undo.CanUndo);
    }

    /// <summary>Tiled cierra cada fila con una coma; sus ficheros tienen que abrirse.</summary>
    [AvaloniaFact]
    public void Se_admite_la_coma_final_de_cada_fila()
    {
        TileMap map = MapCsv.Read("1,2,3,\n4,5,6,\n", "De Tiled");

        Assert.Equal((3, 2), (map.Width, map.Height));
        Assert.Equal(6, map.Layers[0].Grid[2, 1]);
    }

    [AvaloniaFact]
    public void Se_admiten_espacios_y_lineas_en_blanco_al_final()
    {
        TileMap map = MapCsv.Read(" 1 , 2 \r\n 3 , 4 \r\n\r\n", "Con espacios");

        Assert.Equal((2, 2), (map.Width, map.Height));
        Assert.Equal(4, map.Layers[0].Grid[1, 1]);
    }

    // ------------------------------------------------------------------ lo que se rechaza

    [AvaloniaTheory]
    [InlineData("", "ninguna fila")]
    [InlineData("1,2\n3\n", "todas las filas tienen que medir lo mismo")]
    [InlineData("1,dos\n", "no es un número de tile")]
    [InlineData("1,256\n", "se sale")]
    [InlineData("1,-2\n", "se sale")]
    public void Un_csv_que_no_cuadra_se_rechaza_diciendo_que_pasa(string csv, string expected)
    {
        FileFormatException error = Assert.Throws<FileFormatException>(() => MapCsv.Read(csv, "Malo"));

        Assert.Contains(expected, error.Message);
    }

    /// <summary>El mensaje dice en qué fila está el problema, que si no hay que buscarlo.</summary>
    [AvaloniaFact]
    public void El_error_dice_en_que_fila_esta()
    {
        FileFormatException error = Assert.Throws<FileFormatException>(
            () => MapCsv.Read("1,2\n3,4\n5,x\n", "Malo"));

        Assert.Contains("fila 2", error.Message);
    }

    [AvaloniaFact]
    public void Un_csv_mas_grande_que_el_tope_se_rechaza()
    {
        string row = string.Join(",", Enumerable.Repeat("1", TileMap.MaxSide + 1)) + "\n";

        FileFormatException error = Assert.Throws<FileFormatException>(() => MapCsv.Read(row, "Enorme"));

        Assert.Contains("columnas", error.Message);
    }
}
