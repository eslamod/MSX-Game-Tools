using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>Cambiar unos tiles por otros en el mapa.</summary>
public class ReplaceTilesTests
{
    // ------------------------------------------------------------------ el modelo

    /// <summary>
    /// Por rango y con el mismo desplazamiento para todo él: del 10, 11 y 12 al 20 salen
    /// 20, 21 y 22 de una pasada, que es el caso real de recolocar tiles.
    /// </summary>
    [AvaloniaFact]
    public void Un_rango_entero_se_mueve_de_una_vez()
    {
        var map = new TileMap("Nivel", 4, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(10));
        map.Stamp(0, 1, 0, TilePatch.Single(11));
        map.Stamp(0, 2, 0, TilePatch.Single(12));
        map.Stamp(0, 3, 0, TilePatch.Single(50));

        int changed = map.Replace(10, 12, 20, 0, 0, 4, 1, [0]);

        Assert.Equal(3, changed);
        Assert.Equal(20, map.Layers[0].Grid[0, 0]);
        Assert.Equal(22, map.Layers[0].Grid[2, 0]);

        // Lo que no estaba en el rango no se toca.
        Assert.Equal(50, map.Layers[0].Grid[3, 0]);
    }

    [AvaloniaFact]
    public void Un_solo_tile_se_cambia_poniendo_el_mismo_dos_veces()
    {
        var map = new TileMap("Nivel", 2, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(7));
        map.Stamp(0, 1, 0, TilePatch.Single(8));

        map.Replace(7, 7, 90, 0, 0, 2, 1, [0]);

        Assert.Equal(90, map.Layers[0].Grid[0, 0]);
        Assert.Equal(8, map.Layers[0].Grid[1, 0]);
    }

    /// <summary>
    /// Un número que no existe sería peor que dejarlo como está: si el desplazamiento se
    /// sale del juego de tiles, esa celda no se toca.
    /// </summary>
    [AvaloniaFact]
    public void Lo_que_se_saldria_del_juego_de_tiles_no_se_toca()
    {
        var map = new TileMap("Nivel", 2, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(250));
        map.Stamp(0, 1, 0, TilePatch.Single(254));

        // Del 250 al 254 desplazando +4: el 250 llega al 254, pero el 254 daria 258.
        int changed = map.Replace(250, 254, 254, 0, 0, 2, 1, [0]);

        Assert.Equal(1, changed);
        Assert.Equal(254, map.Layers[0].Grid[0, 0]);
        Assert.Equal(254, map.Layers[0].Grid[1, 0]);
    }

    [AvaloniaFact]
    public void Solo_se_cambia_dentro_del_rectangulo()
    {
        var map = new TileMap("Nivel", 4, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(5));
        map.Stamp(0, 3, 0, TilePatch.Single(5));

        map.Replace(5, 5, 6, 0, 0, 2, 1, [0]);

        Assert.Equal(6, map.Layers[0].Grid[0, 0]);
        Assert.Equal(5, map.Layers[0].Grid[3, 0]);
    }

    [AvaloniaFact]
    public void Las_capas_bloqueadas_se_saltan()
    {
        var map = new TileMap("Nivel", 1, 1);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(4));
        map.Stamp(1, 0, 0, TilePatch.Single(4));

        map.Layers[0].IsLocked = true;

        int changed = map.Replace(4, 4, 5, 0, 0, 1, 1, [0, 1]);

        Assert.Equal(1, changed);
        Assert.Equal(4, map.Layers[0].Grid[0, 0]);
        Assert.Equal(5, map.Layers[1].Grid[0, 0]);
    }

    /// <summary>
    /// Sobre varias capas son varios rectángulos, pero para quien lo pidió es una sola
    /// cosa: deshacerlo capa por capa sería desconcertante.
    /// </summary>
    [AvaloniaFact]
    public void Deshacer_una_sustitucion_de_varias_capas_es_un_solo_paso()
    {
        var map = new TileMap("Nivel", 1, 1);
        map.AddLayer();

        map.Stamp(0, 0, 0, TilePatch.Single(4));
        map.Stamp(1, 0, 0, TilePatch.Single(4));

        map.Replace(4, 4, 9, 0, 0, 1, 1, [0, 1]);

        map.Undo.Undo(map);

        Assert.Equal(4, map.Layers[0].Grid[0, 0]);
        Assert.Equal(4, map.Layers[1].Grid[0, 0]);

        map.Undo.Redo(map);

        Assert.Equal(9, map.Layers[0].Grid[0, 0]);
        Assert.Equal(9, map.Layers[1].Grid[0, 0]);
    }

    [AvaloniaFact]
    public void Si_no_cambia_nada_no_se_apunta_nada_que_deshacer()
    {
        var map = new TileMap("Nivel", 2, 2);

        int changed = map.Replace(10, 12, 20, 0, 0, 2, 2, [0]);

        Assert.Equal(0, changed);
        Assert.False(map.Undo.CanUndo);
    }

    // ------------------------------------------------------------------ el formulario

    [AvaloniaFact]
    public void El_formulario_dice_lo_que_va_a_pasar()
    {
        MainWindowViewModel main = WithMap(out _);

        main.ReplaceTilesCommand.Execute(null);

        var form = (ReplaceTilesViewModel)main.RightPanViewModel!;
        form.FromFirst = 10;
        form.FromLast = 12;
        form.ToFirst = 20;

        Assert.Contains("del 10 al 12", form.RangeLabel);
        Assert.Contains("del 20 al 22", form.RangeLabel);

        form.FromLast = 10;

        Assert.Contains("El tile 10 pasa a ser el 20", form.RangeLabel);
    }

    [AvaloniaFact]
    public void Sustituir_cambia_el_mapa_y_dice_cuantas_celdas()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        editor.PickTile(TilePatch.Single(10), "Tile 10");
        editor.Paint(0, 0);
        editor.Paint(1, 0);

        main.ReplaceTilesCommand.Execute(null);

        var form = (ReplaceTilesViewModel)main.RightPanViewModel!;
        form.FromFirst = 10;
        form.FromLast = 10;
        form.ToFirst = 30;
        form.AcceptReplaceCommand.Execute(null);

        Assert.Equal(30, editor.Map.Layers[0].Grid[0, 0]);
        Assert.Contains("2 celdas", form.ResultMessage);

        // Se queda abierto: sustituir suele hacerse varias veces seguidas.
        Assert.Contains(form, main.RightPanels);
    }

    [AvaloniaFact]
    public void Si_no_habia_ninguna_lo_dice()
    {
        MainWindowViewModel main = WithMap(out _);

        main.ReplaceTilesCommand.Execute(null);

        var form = (ReplaceTilesViewModel)main.RightPanViewModel!;
        form.FromFirst = 99;
        form.FromLast = 99;
        form.ToFirst = 1;
        form.AcceptReplaceCommand.Execute(null);

        Assert.Contains("No había ninguna", form.ResultMessage);
    }

    /// <summary>Con algo marcado se propone limitarlo a eso, que es lo que se espera.</summary>
    [AvaloniaFact]
    public void Con_seleccion_se_propone_limitarlo_a_ella()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        editor.PickTile(TilePatch.Single(4), "Tile 4");
        editor.Paint(0, 0);
        editor.Paint(5, 5);

        editor.Select(0, 0, 1, 1);

        main.ReplaceTilesCommand.Execute(null);

        var form = (ReplaceTilesViewModel)main.RightPanViewModel!;

        Assert.True(form.HasSelection);
        Assert.True(form.OnlySelection);

        form.FromFirst = 4;
        form.FromLast = 4;
        form.ToFirst = 9;
        form.AcceptReplaceCommand.Execute(null);

        Assert.Equal(9, editor.Map.Layers[0].Grid[0, 0]);
        Assert.Equal(4, editor.Map.Layers[0].Grid[5, 5]);
    }

    [AvaloniaFact]
    public void Sin_un_mapa_delante_no_se_puede_sustituir()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        Assert.False(main.ReplaceTilesCommand.CanExecute(null));
    }

    private static MainWindowViewModel WithMap(out MapEditorViewModel editor)
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = "Nivel 1";
        form.Columns = 10;
        form.Rows = 10;
        form.AcceptMapCommand.Execute(null);

        editor = main.Tabs.OfType<MapEditorViewModel>().Last();

        return main;
    }
}
