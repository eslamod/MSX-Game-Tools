using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Cambiar el tamaño de un mapa, con su ancla y su deshacer.</summary>
public class ResizeMapTests
{
    // ------------------------------------------------------------------ el modelo

    [AvaloniaFact]
    public void Anclado_arriba_a_la_izquierda_lo_de_antes_se_queda_donde_estaba()
    {
        var map = new TileMap("Nivel", 4, 4);
        map.Stamp(0, 0, 0, TilePatch.Single(7));

        map.Resize(8, 8);

        Assert.Equal((8, 8), (map.Width, map.Height));
        Assert.Equal(7, map.Layers[0].Grid[0, 0]);
    }

    /// <summary>
    /// Alargar un nivel por el principio es lo que no se podía hacer sin ancla: había que
    /// repintarlo entero.
    /// </summary>
    [AvaloniaFact]
    public void Anclado_a_la_derecha_lo_de_antes_se_corre_al_crecer()
    {
        var map = new TileMap("Nivel", 4, 2);
        map.Stamp(0, 0, 0, TilePatch.Single(7));

        map.Resize(10, 2, offsetColumn: 6);

        Assert.Null(map.Layers[0].Grid[0, 0]);
        Assert.Equal(7, map.Layers[0].Grid[6, 0]);
    }

    [AvaloniaFact]
    public void Encoger_pierde_lo_que_queda_fuera_y_se_puede_deshacer()
    {
        var map = new TileMap("Nivel", 6, 6);

        map.Stamp(0, 0, 0, TilePatch.Single(1));
        map.Stamp(0, 5, 5, TilePatch.Single(2));

        map.Resize(3, 3);

        Assert.Null(map.Layers[0].Grid[5, 5]);

        map.Undo.Undo(map);

        Assert.Equal((6, 6), (map.Width, map.Height));
        Assert.Equal(2, map.Layers[0].Grid[5, 5]);
        Assert.Equal(1, map.Layers[0].Grid[0, 0]);
    }

    /// <summary>
    /// Rehacer tiene que devolver también el desplazamiento, no sólo el tamaño: si no, lo
    /// de antes reaparecería en otro sitio.
    /// </summary>
    [AvaloniaFact]
    public void Rehacer_devuelve_el_tamano_y_el_desplazamiento()
    {
        var map = new TileMap("Nivel", 4, 2);
        map.Stamp(0, 0, 0, TilePatch.Single(7));

        map.Resize(10, 2, offsetColumn: 6);
        map.Undo.Undo(map);

        Assert.Equal(7, map.Layers[0].Grid[0, 0]);

        map.Undo.Redo(map);

        Assert.Equal((10, 2), (map.Width, map.Height));
        Assert.Equal(7, map.Layers[0].Grid[6, 0]);
        Assert.Null(map.Layers[0].Grid[0, 0]);
    }

    [AvaloniaFact]
    public void Todas_las_capas_se_mueven_juntas()
    {
        var map = new TileMap("Nivel", 2, 2);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(1));
        map.Stamp(1, 0, 0, TilePatch.Single(2));

        map.Resize(4, 2, offsetColumn: 2);

        Assert.Equal(1, map.Layers[0].Grid[2, 0]);
        Assert.Equal(2, map.Layers[1].Grid[2, 0]);
        Assert.All(map.Layers, layer => Assert.Equal(4, layer.Grid.Width));
    }

    // ------------------------------------------------------------------ el ancla

    [AvaloniaTheory]
    [InlineData(MapAnchor.TopLeft, 0, 0)]
    [InlineData(MapAnchor.TopRight, 6, 0)]
    [InlineData(MapAnchor.BottomLeft, 0, 4)]
    [InlineData(MapAnchor.BottomRight, 6, 4)]
    [InlineData(MapAnchor.Centre, 3, 2)]
    public void El_ancla_dice_cuanto_se_corre_lo_que_habia(MapAnchor anchor, int column, int row)
    {
        Assert.Equal(
            (column, row),
            ResizeMapViewModel.OffsetFor(anchor, oldWidth: 4, oldHeight: 4, newWidth: 10, newHeight: 8));
    }

    /// <summary>Al encoger sale negativo: anclando a la derecha se pierde lo de la izquierda.</summary>
    [AvaloniaFact]
    public void Al_encoger_el_ancla_a_la_derecha_corre_hacia_atras()
    {
        (int column, _) = ResizeMapViewModel.OffsetFor(MapAnchor.TopRight, 10, 4, 6, 4);

        Assert.Equal(-4, column);
    }

    // ------------------------------------------------------------------ el formulario

    [AvaloniaFact]
    public void El_formulario_arranca_con_el_tamano_de_ahora()
    {
        MainWindowViewModel main = WithMap(out _, 12, 9);

        main.ResizeMapCommand.Execute(null);

        var form = Assert.IsType<ResizeMapViewModel>(main.RightPanViewModel);

        Assert.Equal((12, 9), (form.Columns, form.Rows));
        Assert.Contains("12 x 9", form.CurrentLabel);
        Assert.Equal(MapAnchor.TopLeft, form.Anchor);
    }

    [AvaloniaFact]
    public void Sin_un_mapa_delante_no_se_puede_redimensionar()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        Assert.False(main.ResizeMapCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Aceptar_cambia_el_mapa_y_cierra_el_formulario()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor, 4, 4);

        editor.PickTile(TilePatch.Single(3), "Tile 3");
        editor.Paint(0, 0);

        main.ResizeMapCommand.Execute(null);

        var form = (ResizeMapViewModel)main.RightPanViewModel!;
        form.Columns = 8;
        form.Rows = 4;
        form.Anchor = MapAnchor.TopRight;
        form.AcceptResizeCommand.Execute(null);

        Assert.Equal((8, 4), (editor.Map.Width, editor.Map.Height));
        Assert.Equal(3, editor.Map.Layers[0].Grid[4, 0]);
        Assert.Empty(main.RightPanels);
    }

    /// <summary>La selección señalaba celdas que pueden ya no existir.</summary>
    [AvaloniaFact]
    public void Redimensionar_olvida_la_seleccion()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor, 8, 8);

        editor.Select(4, 4, 7, 7);

        Assert.True(editor.HasSelection);

        main.ResizeMapCommand.Execute(null);

        var form = (ResizeMapViewModel)main.RightPanViewModel!;
        form.Columns = 2;
        form.Rows = 2;
        form.AcceptResizeCommand.Execute(null);

        Assert.False(editor.HasSelection);
    }

    [AvaloniaFact]
    public void Un_tamano_imposible_no_se_aplica()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor, 4, 4);

        main.ResizeMapCommand.Execute(null);

        var form = (ResizeMapViewModel)main.RightPanViewModel!;
        form.Columns = 0;
        form.AcceptResizeCommand.Execute(null);

        Assert.True(form.HasError);
        Assert.Equal(4, editor.Map.Width);
    }

    private static MainWindowViewModel WithMap(out MapEditorViewModel editor, int columns, int rows)
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = "Nivel 1";
        form.Columns = columns;
        form.Rows = rows;
        form.AcceptMapCommand.Execute(null);

        editor = main.Tabs.OfType<MapEditorViewModel>().Last();

        return main;
    }
}
