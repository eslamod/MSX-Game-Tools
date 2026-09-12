using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El informe de desplazamiento en un mapa de varias bandas.
/// </summary>
/// <remarks>
/// Se parte por bandas sin perder nada: sólo compara cada celda con la de su derecha, que está
/// en la misma fila, así que nada de lo que mira cruza la frontera entre dos. Lo que no puede
/// es mezclarlas, porque el mismo número de tile es otro dibujo en cada banda y la tabla de
/// copias desplazadas es una por banda.
/// </remarks>
public class ShiftReportBandsTests
{
    /// <summary>El tile que se analiza.</summary>
    private const int Mirado = 5;

    /// <summary>Y el que el mapa le pone a la derecha, que es quien dicta el relleno.</summary>
    /// <remarks>
    /// No el siguiente del juego a propósito: si fuera el 6, el relleno «entra el tile
    /// siguiente» valdría siempre y las dos bandas pedirían lo mismo.
    /// </remarks>
    private const int Vecino = 9;

    /// <summary>
    /// El informe usa el juego y las filas de su banda.
    /// </summary>
    /// <remarks>
    /// El mismo par de tiles puesto en dos bandas pide un relleno distinto en cada una, porque
    /// el vecino no es el mismo dibujo: arriba es macizo y en medio está vacío.
    /// </remarks>
    [AvaloniaFact]
    public void El_informe_usa_el_juego_y_las_filas_de_su_banda()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel arriba = main.OpenTileSet(Juego("Arriba", vecinoMacizo: true));
        TileSetEditorViewModel medio = main.OpenTileSet(Juego("Medio", vecinoMacizo: false));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Banda(arriba), Banda(medio), Banda(arriba)]);

        Par(map, 0);
        Par(map, 8);

        MapEditorViewModel editor = main.OpenMap(map, arriba);

        var form = new ShiftReportViewModel(main, editor)
        {
            // Con el filtro de «sólo los que dan guerra» puesto, un tile limpio no sale en la
            // lista, y aquí lo que se mira es qué relleno le toca.
            OnlyDirty = false,
        };

        string deArriba = Relleno(form);

        form.Band = 1;

        Assert.NotEqual(deArriba, Relleno(form));
    }

    /// <summary>Y no mira las filas de las otras.</summary>
    [AvaloniaFact]
    public void El_informe_solo_mira_las_filas_de_su_banda()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel arriba = main.OpenTileSet(Juego("Arriba", vecinoMacizo: true));
        TileSetEditorViewModel abajo = main.OpenTileSet(Juego("Abajo", vecinoMacizo: true));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Banda(arriba), Banda(arriba), Banda(abajo)]);

        // El único par del mapa, en la banda de abajo.
        Par(map, 16);

        MapEditorViewModel editor = main.OpenMap(map, arriba);

        var form = new ShiftReportViewModel(main, editor) { OnlyDirty = false };

        Assert.DoesNotContain(form.Rows, row => row.Tile == Mirado);

        form.Band = 2;

        Assert.Contains(form.Rows, row => row.Tile == Mirado);
    }

    /// <summary>Con un solo juego se mira el mapa entero, que es lo de siempre.</summary>
    [AvaloniaFact]
    public void Con_un_solo_juego_se_mira_el_mapa_entero()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(Juego("Bosque", vecinoMacizo: true));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        var form = new ShiftReportViewModel(main, editor);

        Assert.False(form.ShowsBands);

        Assert.Equal(0, form.MinRow);
        Assert.Equal(23, form.MaxRow);
    }

    /// <summary>
    /// Lo exportado lleva el nombre de la banda.
    /// </summary>
    /// <remarks>
    /// Cada banda tiene su tabla de copias desplazadas, así que si las tres salieran con el
    /// mismo nombre la segunda se guardaría encima de la primera sin decir nada.
    /// </remarks>
    [AvaloniaFact]
    public void Lo_exportado_lleva_el_nombre_de_la_banda()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel arriba = main.OpenTileSet(Juego("Arriba", vecinoMacizo: true));
        TileSetEditorViewModel medio = main.OpenTileSet(Juego("Medio", vecinoMacizo: false));

        var map = new TileMap("Nivel", 32, 24);

        map.UseTileSets([Banda(arriba), Banda(medio), Banda(arriba)]);

        MapEditorViewModel editor = main.OpenMap(map, arriba);

        var form = new ShiftReportViewModel(main, editor);

        Assert.Equal("Nivel Arriba", form.ExportName);

        form.Band = 1;

        Assert.Equal("Nivel Medio", form.ExportName);
    }

    /// <summary>Y con un solo juego, el nombre del mapa y nada más.</summary>
    [AvaloniaFact]
    public void Con_un_solo_juego_lo_exportado_es_el_nombre_del_mapa()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(Juego("Bosque", vecinoMacizo: true));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        Assert.Equal("Nivel", new ShiftReportViewModel(main, editor).ExportName);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Lo que el informe dice que hay que meter por el borde del tile mirado.</summary>
    private static string Relleno(ShiftReportViewModel form) =>
        form.Rows.Single(row => row.Tile == Mirado).Fill;

    /// <summary>El par de tiles que ata el relleno, en esa fila.</summary>
    private static void Par(TileMap map, int row)
    {
        map.Stamp(0, 0, row, TilePatch.Single(Mirado));
        map.Stamp(0, 1, row, TilePatch.Single(Vecino));
    }

    private static TileSet Juego(string name, bool vecinoMacizo)
    {
        var tileSet = new TileSet(name);

        Macizo(tileSet, Mirado);

        if (vecinoMacizo)
            Macizo(tileSet, Vecino);

        return tileSet;
    }

    private static void Macizo(TileSet tileSet, int tile)
    {
        foreach (TileRow row in tileSet.ListOfTiles[tile].ArrayTileRows)
        {
            for (int column = 0; column < TileRow.Columns; column++)
                row.ArrayPattern[column] = true;
        }
    }

    private static TileSetRef Banda(TileSetEditorViewModel panel) =>
        new(panel.TileSet.Id, panel.TileSet.Name);
}
