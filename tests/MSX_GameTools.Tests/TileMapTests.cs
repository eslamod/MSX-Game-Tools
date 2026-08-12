using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El mapa: capas, aplastado y deshacer.</summary>
public class TileMapTests
{
    [AvaloniaFact]
    public void Un_mapa_nace_con_una_capa_del_tamano_pedido()
    {
        var map = new TileMap("Bosque", 40, 20);

        Assert.Equal((40, 20), (map.Width, map.Height));

        MapLayer layer = Assert.Single(map.Layers);

        Assert.Equal("Capa 1", layer.Name);
        Assert.Equal((40, 20), (layer.Grid.Width, layer.Grid.Height));
        Assert.True(layer.IsVisible);
        Assert.False(layer.IsLocked);
    }

    [AvaloniaFact]
    public void No_se_puede_pedir_un_mapa_mas_grande_que_el_tope()
    {
        var map = new TileMap("Enorme", 5000, 5000);

        Assert.Equal((TileMap.MaxSide, TileMap.MaxSide), (map.Width, map.Height));
    }

    // ------------------------------------------------------------------ aplastado

    /// <summary>
    /// En la máquina hay una tabla de nombres, no capas: gana la celda no vacía más alta,
    /// y donde ninguna capa pone nada se queda el hueco para el color de fondo.
    /// </summary>
    [AvaloniaFact]
    public void Al_aplastar_gana_la_capa_de_arriba()
    {
        var map = new TileMap("Bosque", 4, 4);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(10));
        map.Stamp(0, 1, 0, TilePatch.Single(11));
        map.Stamp(1, 0, 0, TilePatch.Single(20));

        TileGrid flat = map.Flatten();

        Assert.Equal(20, flat[0, 0]);   // la de arriba tapa
        Assert.Equal(11, flat[1, 0]);   // aqui solo hay una
        Assert.Null(flat[3, 3]);        // aqui ninguna
    }

    /// <summary>El tile 0 tapa igual que cualquier otro: no es un hueco.</summary>
    [AvaloniaFact]
    public void El_tile_0_de_una_capa_alta_tapa_a_la_de_abajo()
    {
        var map = new TileMap("Bosque", 2, 2);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(7));
        map.Stamp(1, 0, 0, TilePatch.Single(0));

        Assert.Equal(0, map.Flatten()[0, 0]);
    }

    /// <summary>
    /// Apagar una capa es una ayuda de edición, no quiere decir que sobre: al exportar
    /// cuenta igual, y sólo se salta al componer lo que se ve.
    /// </summary>
    [AvaloniaFact]
    public void Una_capa_apagada_se_exporta_pero_no_se_ve()
    {
        var map = new TileMap("Bosque", 2, 2);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(5));
        map.Stamp(1, 0, 0, TilePatch.Single(9));

        map.Layers[1].IsVisible = false;

        Assert.Equal(9, map.Flatten()[0, 0]);
        Assert.Equal(5, map.Flatten(onlyVisible: true)[0, 0]);
    }

    // ------------------------------------------------------------------ edicion

    [AvaloniaFact]
    public void Una_capa_bloqueada_no_se_deja_tocar()
    {
        var map = new TileMap("Bosque", 4, 4);
        map.Layers[0].IsLocked = true;

        Assert.False(map.Stamp(0, 0, 0, TilePatch.Single(3)));
        Assert.False(map.Erase(0, 0, 0));
        Assert.False(map.Fill(0, 0, 0, 2, 2, TilePatch.Single(3)));

        Assert.Null(map.Layers[0].Grid[0, 0]);
        Assert.False(map.Undo.CanUndo);
    }

    /// <summary>Rellenar repite el trozo, no lo estira.</summary>
    [AvaloniaFact]
    public void Rellenar_embaldosa_el_patron()
    {
        var map = new TileMap("Bosque", 4, 4);

        var pattern = new TilePatch(2, 1);
        pattern[0, 0] = 1;
        pattern[1, 0] = 2;

        map.Fill(0, 0, 0, 4, 2, pattern);

        TileGrid grid = map.Layers[0].Grid;

        Assert.Equal([1, 2, 1, 2], new[] { grid[0, 0], grid[1, 0], grid[2, 0], grid[3, 0] });
        Assert.Equal(1, grid[0, 1]);
    }

    // ------------------------------------------------------------------ deshacer

    [AvaloniaFact]
    public void Deshacer_devuelve_lo_que_habia()
    {
        var map = new TileMap("Bosque", 4, 4);

        map.Stamp(0, 1, 1, TilePatch.Single(5));
        map.Stamp(0, 1, 1, TilePatch.Single(6));

        Assert.Equal(6, map.Layers[0].Grid[1, 1]);

        map.Undo.Undo(map);

        Assert.Equal(5, map.Layers[0].Grid[1, 1]);

        map.Undo.Undo(map);

        Assert.Null(map.Layers[0].Grid[1, 1]);
        Assert.False(map.Undo.CanUndo);
    }

    [AvaloniaFact]
    public void Deshacer_un_borrado_devuelve_los_tiles()
    {
        var map = new TileMap("Bosque", 4, 4);

        map.Fill(0, 0, 0, 2, 2, TilePatch.Single(8));
        map.Erase(0, 0, 0, 2, 2);

        Assert.Null(map.Layers[0].Grid[1, 1]);

        map.Undo.Undo(map);

        Assert.Equal(8, map.Layers[0].Grid[1, 1]);
    }

    [AvaloniaFact]
    public void Rehacer_vuelve_a_aplicar_lo_deshecho()
    {
        var map = new TileMap("Bosque", 4, 4);

        map.Stamp(0, 0, 0, TilePatch.Single(4));
        map.Undo.Undo(map);

        Assert.True(map.Undo.CanRedo);

        map.Undo.Redo(map);

        Assert.Equal(4, map.Layers[0].Grid[0, 0]);
    }

    /// <summary>
    /// Hacer algo nuevo después de deshacer tira lo que quedaba por rehacer: a partir de
    /// ahí la historia es otra.
    /// </summary>
    [AvaloniaFact]
    public void Hacer_algo_nuevo_tira_lo_que_habia_para_rehacer()
    {
        var map = new TileMap("Bosque", 4, 4);

        map.Stamp(0, 0, 0, TilePatch.Single(4));
        map.Undo.Undo(map);
        map.Stamp(0, 1, 1, TilePatch.Single(7));

        Assert.False(map.Undo.CanRedo);
    }

    [AvaloniaFact]
    public void La_pila_se_queda_en_veinte_pasos()
    {
        var map = new TileMap("Bosque", 64, 4);

        for (int i = 0; i < UndoStack.MaxSteps + 5; i++)
            map.Stamp(0, i, 0, TilePatch.Single(1));

        for (int i = 0; i < UndoStack.MaxSteps; i++)
            map.Undo.Undo(map);

        Assert.False(map.Undo.CanUndo);

        // Los cinco primeros ya no se pueden deshacer: se cayeron de la pila.
        Assert.Equal(1, map.Layers[0].Grid[0, 0]);
        Assert.Null(map.Layers[0].Grid[24, 0]);
    }

    // ------------------------------------------------------------------ redimensionar

    [AvaloniaFact]
    public void Redimensionar_afecta_a_todas_las_capas()
    {
        var map = new TileMap("Bosque", 4, 4);
        map.AddLayer();

        map.Resize(8, 6);

        Assert.Equal((8, 6), (map.Width, map.Height));
        Assert.All(map.Layers, layer => Assert.Equal((8, 6), (layer.Grid.Width, layer.Grid.Height)));
    }

    /// <summary>Encoger pierde filas enteras, así que deshacerlo tiene que devolverlas.</summary>
    [AvaloniaFact]
    public void Deshacer_un_encogido_devuelve_lo_que_se_habia_perdido()
    {
        var map = new TileMap("Bosque", 4, 4);

        map.Stamp(0, 3, 3, TilePatch.Single(9));
        map.Stamp(0, 0, 0, TilePatch.Single(1));

        map.Resize(2, 2);

        Assert.Null(map.Layers[0].Grid[3, 3]);

        map.Undo.Undo(map);

        Assert.Equal((4, 4), (map.Width, map.Height));
        Assert.Equal(9, map.Layers[0].Grid[3, 3]);
        Assert.Equal(1, map.Layers[0].Grid[0, 0]);
    }

    [AvaloniaFact]
    public void Las_capas_nuevas_se_numeran_sin_repetir()
    {
        var map = new TileMap("Bosque", 4, 4);

        map.Layers[0].Name = "Suelo";
        map.AddLayer();
        map.AddLayer();

        Assert.Equal(["Suelo", "Capa 1", "Capa 2"], map.Layers.Select(layer => layer.Name));
    }
}
