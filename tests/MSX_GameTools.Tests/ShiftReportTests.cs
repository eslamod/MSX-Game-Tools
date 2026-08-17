using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El informe de la tabla de desplazamiento, tal y como se usa.</summary>
public class ShiftReportTests
{
    [AvaloniaFact]
    public void Sin_un_mapa_delante_no_hay_informe()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        Assert.False(main.ShiftReportCommand.CanExecute(null));
    }

    /// <summary>
    /// Se abre con los que dan guerra, que son los que hay algo que decidir.
    /// </summary>
    /// <remarks>
    /// De los 256 tiles, en un mapa de verdad el mapa ata cincuenta y pico y se contradice en
    /// una docena. Abrir con la lista entera obligaría a buscar esa docena entre los limpios.
    /// </remarks>
    [AvaloniaFact]
    public void Empieza_ensenando_solo_los_que_dan_guerra()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        Paint(editor, 5, 7, 5, 9);
        Solid(editor, 5, 7);

        var panel = Open(main);

        // El 5 se contradice -junto al 7 pide unos y junto al 9 ceros-, el 7 no.
        Assert.True(panel.OnlyDirty);
        Assert.Equal(5, Assert.Single(panel.Rows).Tile);

        panel.OnlyDirty = false;

        Assert.Contains(panel.Rows, row => row.Tile == 7);
    }

    /// <summary>
    /// Ignorar un tile lo saca del informe y rehace las cuentas de todos.
    /// </summary>
    /// <remarks>
    /// Y no sólo quita la fila: las celdas que aportaba dejan de contar en el total. Si el
    /// número de arriba se quedara igual después de sacar el fondo, sacarlo no serviría de
    /// nada, que es justo para lo que está.
    /// </remarks>
    [AvaloniaFact]
    public void Ignorar_un_tile_lo_saca_y_rehace_las_cuentas()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        Paint(editor, 5, 7, 5, 9);
        Solid(editor, 5, 7);

        var panel = Open(main);

        string before = panel.Totals;

        Assert.Single(panel.Rows);

        panel.Rows[0].IsIgnored = true;

        Assert.Empty(panel.Rows);
        Assert.NotEqual(before, panel.Totals);
        Assert.NotNull(panel.IgnoredLabel);
    }

    /// <summary>
    /// El botón de ver lleva el mapa a la celda y la deja marcada.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que listar posiciones sirva de algo: en un mapa de 256 de ancho, «columna
    /// 143» no se encuentra arrastrando.
    /// </remarks>
    [AvaloniaFact]
    public void Ver_lleva_el_mapa_a_la_celda_que_falla()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        Paint(editor, 5, 7, 5, 9);
        Solid(editor, 5, 7);

        var panel = Open(main);

        (int Column, int Row)? asked = null;
        editor.ShowCellRequested += (column, row) => asked = (column, row);

        panel.Rows[0].ShowNextCommand.Execute(null);

        // El sitio que se queda sin desplazar es el 5 que tiene el hueco a la derecha.
        Assert.Equal((2, 0), asked);
        Assert.Equal(new MapRegion(2, 0, 1, 1), editor.Selection);
    }

    /// <summary>Y va pasando por todos, dando la vuelta al llegar al final.</summary>
    [AvaloniaFact]
    public void Ver_va_pasando_por_todos_los_sitios()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        // El 5 pide unos tres veces y ceros dos: se queda con los unos y falla en dos sitios.
        Paint(editor, 5, 7, 5, 7, 5, 7, 5, 9, 5, 9);
        Solid(editor, 5, 7);

        var panel = Open(main);

        List<(int Column, int Row)> asked = [];
        editor.ShowCellRequested += (column, row) => asked.Add((column, row));

        panel.Rows[0].ShowNextCommand.Execute(null);
        panel.Rows[0].ShowNextCommand.Execute(null);
        panel.Rows[0].ShowNextCommand.Execute(null);

        Assert.Equal([(6, 0), (8, 0), (6, 0)], asked);
    }

    /// <summary>
    /// Dejar fuera la fila del marcador limpia lo que allí no se iba a notar.
    /// </summary>
    /// <remarks>
    /// El caso que lo pidió: un tile con una sola posición rota de diez, y la posición estaba
    /// en la fila de arriba, que es el marcador y no se desplaza.
    /// </remarks>
    [AvaloniaFact]
    public void Sacar_la_fila_del_marcador_limpia_el_informe()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor, rows: 2);

        Solid(editor, 5, 7);

        // Arriba el 5 toca macizo y abajo toca vacío: se contradice.
        editor.Map.Stamp(0, 0, 0, TilePatch.Single(5));
        editor.Map.Stamp(0, 1, 0, TilePatch.Single(7));
        editor.Map.Stamp(0, 0, 1, TilePatch.Single(5));
        editor.Map.Stamp(0, 1, 1, TilePatch.Single(9));

        var panel = Open(main);

        Assert.Single(panel.Rows);

        panel.FirstRow = 1;

        Assert.Empty(panel.Rows);
    }

    /// <summary>Y el rango de tiles decide cuánto ocupa la tabla que se exporta.</summary>
    [AvaloniaFact]
    public void El_rango_de_tiles_decide_el_tamano_de_la_tabla()
    {
        MainWindowViewModel main = WithMap(out MapEditorViewModel editor);

        Paint(editor, 5, 7, 5, 9);
        Solid(editor, 5, 7);

        var panel = Open(main);

        Assert.Contains("256", panel.TableLabel);

        panel.FirstTile = 32;
        panel.LastTile = 127;

        Assert.Contains("96", panel.TableLabel);

        // Y el 5 se queda fuera del informe, que ya no se desplaza.
        Assert.Empty(panel.Rows);
    }

    /// <summary>La tabla exportada lleva un byte por tile con el código de la rutina.</summary>
    [AvaloniaFact]
    public void La_tabla_exportada_lleva_un_byte_por_tile()
    {
        var tileSet = new TileSet("Bosque");

        foreach (int tile in (int[])[5, 7])
        {
            foreach (TileRow row in tileSet.ListOfTiles[tile].ArrayTileRows)
            {
                for (int column = 0; column < TileRow.Columns; column++)
                    row.ArrayPattern[column] = true;
            }
        }

        var map = new TileMap("Nivel", 2, 1);
        map.AddLayer();
        map.Layers[0].Grid[0, 0] = 5;
        map.Layers[0].Grid[1, 0] = 7;

        MapShiftReport report = MapShiftAnalysis.Of(map, tileSet);
        byte[] bytes = MapShiftExporter.ToBinary(report);

        Assert.Equal(TileSet.TileCount, bytes.Length);
        Assert.Equal((byte)ShiftFill.Ones, bytes[5]);

        // Y el ensamblador saca los mismos números, con los codigos explicados arriba.
        string text = MapShiftExporter.ToAssembler(report, "Nivel");

        Assert.Contains("_shift:", text);
        Assert.Contains("_shift_ones:", text);
        Assert.Contains($"{SpriteBankExporter.DataDirective}  0,0,0,0,0,2,0,0", text);
    }

    // ------------------------------------------------------------------ los andamios

    private static ShiftReportViewModel Open(MainWindowViewModel main)
    {
        main.ShiftReportCommand.Execute(null);

        return (ShiftReportViewModel)main.RightPanViewModel!;
    }

    /// <summary>Pone los tiles que se le digan en la primera fila del mapa.</summary>
    private static void Paint(MapEditorViewModel editor, params int[] row)
    {
        for (int column = 0; column < row.Length; column++)
            editor.Map.Stamp(0, column, 0, TilePatch.Single(row[column]));
    }

    /// <summary>Deja macizos los tiles que se le digan, para que aporten columna izquierda.</summary>
    private static void Solid(MapEditorViewModel editor, params int[] tiles)
    {
        foreach (int tile in tiles)
        {
            foreach (TileRow row in editor.TileSet.ListOfTiles[tile].ArrayTileRows)
            {
                for (int column = 0; column < TileRow.Columns; column++)
                    row.ArrayPattern[column] = true;
            }
        }
    }

    private static MainWindowViewModel WithMap(out MapEditorViewModel editor, int rows = 1)
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = "Nivel 1";
        form.Columns = 12;
        form.Rows = rows;
        form.AcceptMapCommand.Execute(null);

        editor = main.Tabs.OfType<MapEditorViewModel>().Last();

        return main;
    }
}
