using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El panel del mapa: herramientas, capas y lo que se estampa.</summary>
public class MapEditorTests
{
    [AvaloniaFact]
    public void Arranca_en_la_capa_de_arriba_y_con_algo_cogido()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.Equal(MapTool.Stamp, editor.Tool);
        Assert.Same(editor.Layers[^1], editor.ActiveLayer);
        Assert.True(editor.CanEdit);

        // Con algo cogido de partida, pulsar hace algo desde el principio.
        Assert.Equal(0, editor.Brush[0, 0]);
    }

    [AvaloniaFact]
    public void Estampar_deja_el_tile_en_la_capa_activa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.PickTile(TilePatch.Single(12), "Tile 12");
        editor.Paint(3, 2);

        Assert.Equal(12, editor.ActiveLayer!.Layer.Grid[3, 2]);
        Assert.True(editor.UndoCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void En_una_capa_bloqueada_no_se_pinta()
    {
        MapEditorViewModel editor = NewEditor();

        editor.ActiveLayer!.IsLocked = true;
        editor.Paint(1, 1);

        Assert.Null(editor.ActiveLayer.Layer.Grid[1, 1]);
        Assert.False(editor.CanEdit);
    }

    // ------------------------------------------------------------------ seleccion

    /// <summary>
    /// El rectángulo sale igual arrastrando en cualquier sentido, y se queda dentro del
    /// mapa aunque el ratón se salga.
    /// </summary>
    [AvaloniaFact]
    public void La_seleccion_se_normaliza_y_no_se_sale_del_mapa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.Select(5, 4, 2, 1);

        Assert.Equal(new MapRegion(2, 1, 4, 4), editor.Selection);

        editor.Select(0, 0, 999, 999);

        Assert.Equal(new MapRegion(0, 0, editor.Map.Width, editor.Map.Height), editor.Selection);
    }

    [AvaloniaFact]
    public void Sin_seleccion_no_se_puede_rellenar_ni_copiar()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.False(editor.FillSelectionCommand.CanExecute(null));
        Assert.False(editor.CopySelectionCommand.CanExecute(null));
        Assert.False(editor.EraseSelectionCommand.CanExecute(null));

        editor.Select(0, 0, 1, 1);

        Assert.True(editor.FillSelectionCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Rellenar_la_seleccion_embaldosa_lo_cogido()
    {
        MapEditorViewModel editor = NewEditor();

        editor.PickTile(TilePatch.Single(7), "Tile 7");
        editor.Select(1, 1, 2, 2);
        editor.FillSelectionCommand.Execute(null);

        TileGrid grid = editor.ActiveLayer!.Layer.Grid;

        Assert.Equal(7, grid[1, 1]);
        Assert.Equal(7, grid[2, 2]);
        Assert.Null(grid[0, 0]);
    }

    /// <summary>
    /// Copiar coge de la capa activa y no de lo que se ve: pegar tiene que devolver lo
    /// mismo que se cogió, y lo que se ve puede venir de otra capa.
    /// </summary>
    [AvaloniaFact]
    public void Copiar_coge_de_la_capa_activa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.AddLayerCommand.Execute(null);

        // Debajo hay otra cosa, que no debe colarse en la copia.
        editor.Map.Stamp(0, 0, 0, TilePatch.Single(99));

        editor.PickTile(TilePatch.Single(5), "Tile 5");
        editor.Paint(1, 0);

        editor.Select(0, 0, 1, 0);
        editor.CopySelectionCommand.Execute(null);

        Assert.Equal((2, 1), (editor.Brush.Width, editor.Brush.Height));
        Assert.Null(editor.Brush[0, 0]);
        Assert.Equal(5, editor.Brush[1, 0]);
        Assert.Contains("2x1", editor.BrushName);
    }

    // ------------------------------------------------------------------ deshacer

    [AvaloniaFact]
    public void Deshacer_y_rehacer_se_encienden_solos()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.False(editor.UndoCommand.CanExecute(null));

        editor.Paint(0, 0);

        Assert.True(editor.UndoCommand.CanExecute(null));
        Assert.False(editor.RedoCommand.CanExecute(null));

        editor.UndoCommand.Execute(null);

        Assert.False(editor.UndoCommand.CanExecute(null));
        Assert.True(editor.RedoCommand.CanExecute(null));
        Assert.Null(editor.ActiveLayer!.Layer.Grid[0, 0]);
    }

    // ------------------------------------------------------------------ capas

    [AvaloniaFact]
    public void Agregar_una_capa_la_pone_encima_y_activa()
    {
        MapEditorViewModel editor = NewEditor();

        editor.AddLayerCommand.Execute(null);

        Assert.Equal(2, editor.Layers.Count);
        Assert.Same(editor.Layers[^1], editor.ActiveLayer);
        Assert.Equal(1, editor.ActiveLayerIndex);
        Assert.True(editor.ActiveLayer!.IsActive);
        Assert.False(editor.Layers[0].IsActive);
    }

    /// <summary>Un mapa sin capas no se puede editar, así que la última no se borra.</summary>
    [AvaloniaFact]
    public void La_ultima_capa_no_se_puede_eliminar()
    {
        MapEditorViewModel editor = NewEditor();

        Assert.False(editor.DeleteLayerCommand.CanExecute(null));

        editor.AddLayerCommand.Execute(null);

        Assert.True(editor.DeleteLayerCommand.CanExecute(null));

        editor.DeleteLayerCommand.Execute(null);

        Assert.Single(editor.Layers);
        Assert.Single(editor.Map.Layers);
        Assert.NotNull(editor.ActiveLayer);
    }

    /// <summary>Apagar una capa cambia lo que se ve, así que el lienzo tiene que repintarse.</summary>
    [AvaloniaFact]
    public void Apagar_una_capa_pide_repintar()
    {
        MapEditorViewModel editor = NewEditor();

        int repaints = 0;
        editor.RefreshRequested += () => repaints++;

        editor.Layers[0].IsVisible = false;

        Assert.Equal(1, repaints);
    }

    // ------------------------------------------------------------------ zoom y pincel

    [AvaloniaFact]
    public void El_zoom_se_queda_entre_los_topes()
    {
        MapEditorViewModel editor = NewEditor();

        for (int i = 0; i < 20; i++)
            editor.ZoomInCommand.Execute(null);

        Assert.Equal(MapEditorViewModel.MaxZoom, editor.Zoom);

        for (int i = 0; i < 20; i++)
            editor.ZoomOutCommand.Execute(null);

        Assert.Equal(MapEditorViewModel.MinZoom, editor.Zoom);
    }

    /// <summary>El ajuste coge el zoom más grande con el que el mapa entero cabe.</summary>
    [AvaloniaFact]
    public void Ajustar_el_zoom_mete_el_mapa_entero()
    {
        MapEditorViewModel editor = NewEditor();   // 8 x 6 tiles = 64 x 48 pixeles

        editor.FitZoom(200, 200);

        Assert.Equal(3, editor.Zoom);

        editor.FitZoom(64, 48);

        Assert.Equal(1, editor.Zoom);
    }

    [AvaloniaFact]
    public void Coger_un_bloque_lo_deja_entero_como_pincel()
    {
        MapEditorViewModel editor = NewEditor(out TileSet tileSet);

        var block = new TileBlock("Arbol") { [0, 0] = 1, [1, 1] = 2 };
        tileSet.Blocks.Add(block);

        editor.PickBlock(block);

        Assert.Equal((2, 2), (editor.Brush.Width, editor.Brush.Height));
        Assert.Equal(2, editor.Brush[1, 1]);
        Assert.Contains("Arbol", editor.BrushName);
    }

    /// <summary>La rueda con shift pasa al bloque siguiente sin salirse de la lista.</summary>
    [AvaloniaFact]
    public void Pasar_de_bloque_se_para_en_los_extremos()
    {
        MapEditorViewModel editor = NewEditor(out TileSet tileSet);

        tileSet.Blocks.Add(new TileBlock("Uno") { [0, 0] = 1 });
        tileSet.Blocks.Add(new TileBlock("Dos") { [0, 0] = 2 });

        editor.PickBlock(tileSet.Blocks[0]);

        editor.StepBlock(1);
        Assert.Contains("Dos", editor.BrushName);

        editor.StepBlock(1);
        Assert.Contains("Dos", editor.BrushName);

        editor.StepBlock(-1);
        Assert.Contains("Uno", editor.BrushName);
    }

    private static MapEditorViewModel NewEditor() => NewEditor(out _);

    private static MapEditorViewModel NewEditor(out TileSet tileSet)
    {
        tileSet = new TileSet("Bosque");

        return new MapEditorViewModel(
            new TileMap("Mapa 1", 8, 6),
            new TileSetEditorViewModel(tileSet, new PaletteLibrary()));
    }
}
