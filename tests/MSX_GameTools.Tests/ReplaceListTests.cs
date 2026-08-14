using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Sustituir tiles por lista, cuando no van seguidos.
/// </summary>
/// <remarks>
/// Es el caso de verdad: los tiles de un árbol pueden ser el 35, 36, 37, 67, 68 y 69, y
/// sus destinos tampoco tienen por qué ir en fila. Por rango harían falta tres pasadas, y
/// sólo si los números salieron alineados.
/// </remarks>
public class ReplaceListTests
{
    // ------------------------------------------------------------------ el modelo

    [AvaloniaFact]
    public void Cada_tile_va_a_donde_diga_la_tabla()
    {
        var map = new TileMap("Nivel", 4, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(35));
        map.Stamp(0, 1, 0, TilePatch.Single(67));
        map.Stamp(0, 2, 0, TilePatch.Single(99));
        map.Stamp(0, 3, 0, TilePatch.Single(12));

        var table = new Dictionary<int, int> { [35] = 77, [67] = 210, [99] = 230 };

        int changed = map.Replace(table, 0, 0, 4, 1, [0]);

        Assert.Equal(3, changed);
        Assert.Equal(77, map.Layers[0].Grid[0, 0]);
        Assert.Equal(210, map.Layers[0].Grid[1, 0]);
        Assert.Equal(230, map.Layers[0].Grid[2, 0]);

        // El que no está en la tabla se queda como estaba.
        Assert.Equal(12, map.Layers[0].Grid[3, 0]);
    }

    /// <summary>
    /// Las sustituciones se aplican todas a la vez, no en cadena.
    /// </summary>
    /// <remarks>
    /// Con 35→77 y 77→88 en la misma tabla, un 35 acaba en 77 y ahí se queda; sólo los 77
    /// que ya hubiera pasan a 88. Encadenándolas, el resultado dependería del orden de la
    /// tabla, que es de las cosas que no se entienden mirando el mapa.
    /// </remarks>
    [AvaloniaFact]
    public void Una_sustitucion_no_arrastra_a_la_siguiente()
    {
        var map = new TileMap("Nivel", 2, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(35));
        map.Stamp(0, 1, 0, TilePatch.Single(77));

        var table = new Dictionary<int, int> { [35] = 77, [77] = 88 };

        map.Replace(table, 0, 0, 2, 1, [0]);

        Assert.Equal(77, map.Layers[0].Grid[0, 0]);
        Assert.Equal(88, map.Layers[0].Grid[1, 0]);
    }

    [AvaloniaFact]
    public void Lo_que_se_saldria_del_juego_de_tiles_no_se_toca()
    {
        var map = new TileMap("Nivel", 2, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(10));
        map.Stamp(0, 1, 0, TilePatch.Single(11));

        var table = new Dictionary<int, int> { [10] = 300, [11] = 20 };

        int changed = map.Replace(table, 0, 0, 2, 1, [0]);

        Assert.Equal(1, changed);
        Assert.Equal(10, map.Layers[0].Grid[0, 0]);
        Assert.Equal(20, map.Layers[0].Grid[1, 0]);
    }

    /// <summary>Toda la lista se deshace de una vez: se pidió una vez.</summary>
    [AvaloniaFact]
    public void Deshacer_devuelve_la_lista_entera_de_un_paso()
    {
        var map = new TileMap("Nivel", 3, 1);

        map.Stamp(0, 0, 0, TilePatch.Single(1));
        map.Stamp(0, 1, 0, TilePatch.Single(2));
        map.Stamp(0, 2, 0, TilePatch.Single(3));

        map.Replace(new Dictionary<int, int> { [1] = 11, [2] = 22, [3] = 33 }, 0, 0, 3, 1, [0]);

        map.Undo.Undo(map);

        Assert.Equal(1, map.Layers[0].Grid[0, 0]);
        Assert.Equal(2, map.Layers[0].Grid[1, 0]);
        Assert.Equal(3, map.Layers[0].Grid[2, 0]);
    }

    // ------------------------------------------------------------------ el formulario

    [AvaloniaFact]
    public void Se_añaden_y_se_quitan_filas()
    {
        ReplaceTilesViewModel form = NewForm(out _);

        form.NewFrom = 35;
        form.NewTo = 77;
        form.AddSubstitutionCommand.Execute(null);

        form.NewFrom = 36;
        form.NewTo = 88;
        form.AddSubstitutionCommand.Execute(null);

        Assert.Equal(2, form.Substitutions.Count);
        Assert.Equal(35, form.Substitutions[0].From);
        Assert.Equal(88, form.Substitutions[1].To);

        form.SelectedSubstitution = form.Substitutions[0];
        form.RemoveSubstitutionCommand.Execute(null);

        TileSubstitution left = Assert.Single(form.Substitutions);

        Assert.Equal(36, left.From);
    }

    /// <summary>Sin fila elegida no hay nada que quitar.</summary>
    [AvaloniaFact]
    public void Quitar_esta_apagado_hasta_elegir_una_fila()
    {
        ReplaceTilesViewModel form = NewForm(out _);

        form.NewFrom = 35;
        form.AddSubstitutionCommand.Execute(null);

        Assert.False(form.RemoveSubstitutionCommand.CanExecute(null));

        form.SelectedSubstitution = form.Substitutions[0];

        Assert.True(form.RemoveSubstitutionCommand.CanExecute(null));
    }

    /// <summary>
    /// Un origen no puede estar dos veces.
    /// </summary>
    /// <remarks>
    /// La segunda fila no llegaría a pasar nunca, porque cada celda se mira una sola vez.
    /// Mejor no dejar añadirla que aceptar una fila muerta que parece que hace algo.
    /// </remarks>
    [AvaloniaFact]
    public void El_mismo_origen_no_se_puede_poner_dos_veces()
    {
        ReplaceTilesViewModel form = NewForm(out _);

        form.NewFrom = 35;
        form.NewTo = 77;
        form.AddSubstitutionCommand.Execute(null);

        form.NewTo = 88;

        Assert.True(form.AlreadyListed);
        Assert.False(form.AddSubstitutionCommand.CanExecute(null));

        // Y con otro origen se vuelve a poder.
        form.NewFrom = 36;

        Assert.False(form.AlreadyListed);
        Assert.True(form.AddSubstitutionCommand.CanExecute(null));
    }

    /// <summary>Cada fila se queda con el dibujo de sus dos tiles.</summary>
    [AvaloniaFact]
    public void Las_filas_traen_el_dibujo_de_los_dos_tiles()
    {
        ReplaceTilesViewModel form = NewForm(out MapEditorViewModel editor);

        form.NewFrom = 35;
        form.NewTo = 77;

        Assert.Same(editor.Tiles[35], form.NewFromTile);
        Assert.Same(editor.Tiles[77], form.NewToTile);

        form.AddSubstitutionCommand.Execute(null);

        TileSubstitution row = Assert.Single(form.Substitutions);

        Assert.Same(editor.Tiles[35], row.FromTile);
        Assert.Same(editor.Tiles[77], row.ToTile);
    }

    [AvaloniaFact]
    public void Sustituir_por_lista_cambia_el_mapa()
    {
        ReplaceTilesViewModel form = NewForm(out MapEditorViewModel editor);

        editor.PickTile(TilePatch.Single(35), "Tile 35");
        editor.Paint(0, 0);
        editor.PickTile(TilePatch.Single(67), "Tile 67");
        editor.Paint(1, 0);

        form.IsListMode = true;

        form.NewFrom = 35;
        form.NewTo = 77;
        form.AddSubstitutionCommand.Execute(null);

        form.NewFrom = 67;
        form.NewTo = 210;
        form.AddSubstitutionCommand.Execute(null);

        form.AcceptReplaceCommand.Execute(null);

        Assert.Equal(77, editor.Map.Layers[0].Grid[0, 0]);
        Assert.Equal(210, editor.Map.Layers[0].Grid[1, 0]);
        Assert.Contains("2", form.ResultMessage!);
    }

    /// <summary>
    /// Con el modo por rango, la lista no pinta nada aunque tenga filas.
    /// </summary>
    /// <remarks>
    /// Los dos modos comparten formulario, así que lo que decide es el interruptor y no
    /// lo que haya rellenado en el otro lado.
    /// </remarks>
    [AvaloniaFact]
    public void En_modo_rango_la_lista_no_se_aplica()
    {
        ReplaceTilesViewModel form = NewForm(out MapEditorViewModel editor);

        editor.PickTile(TilePatch.Single(35), "Tile 35");
        editor.Paint(0, 0);

        form.NewFrom = 35;
        form.NewTo = 77;
        form.AddSubstitutionCommand.Execute(null);

        // Sigue en rango, que es como abre.
        Assert.True(form.IsRangeMode);

        form.FromFirst = 1;
        form.FromLast = 1;
        form.ToFirst = 2;
        form.AcceptReplaceCommand.Execute(null);

        Assert.Equal(35, editor.Map.Layers[0].Grid[0, 0]);
    }

    [AvaloniaFact]
    public void Con_la_lista_vacia_se_dice_y_no_se_toca_el_mapa()
    {
        ReplaceTilesViewModel form = NewForm(out MapEditorViewModel editor);

        editor.PickTile(TilePatch.Single(35), "Tile 35");
        editor.Paint(0, 0);

        form.IsListMode = true;
        form.AcceptReplaceCommand.Execute(null);

        Assert.Equal("La lista está vacía: añade alguna sustitución.", form.ResultMessage);
        Assert.Equal(35, editor.Map.Layers[0].Grid[0, 0]);
    }

    [AvaloniaFact]
    public void Los_dos_modos_son_uno_u_otro()
    {
        ReplaceTilesViewModel form = NewForm(out _);

        Assert.True(form.IsRangeMode);
        Assert.False(form.IsListMode);

        form.IsListMode = true;

        Assert.False(form.IsRangeMode);
    }

    /// <summary>El formulario abierto sobre un mapa con su juego de tiles.</summary>
    private static ReplaceTilesViewModel NewForm(out MapEditorViewModel editor)
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));
        main.AddMapCommand.Execute(null);

        var newMap = (EditMapViewModel)main.RightPanViewModel!;
        newMap.Name = "Nivel 1";
        newMap.Columns = 10;
        newMap.Rows = 10;
        newMap.AcceptMapCommand.Execute(null);

        editor = main.Tabs.OfType<MapEditorViewModel>().Last();

        main.ReplaceTilesCommand.Execute(null);

        return (ReplaceTilesViewModel)main.RightPanViewModel!;
    }
}
