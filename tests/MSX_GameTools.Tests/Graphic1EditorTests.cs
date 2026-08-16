using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que el editor enseña de un juego de screen 1.
/// </summary>
/// <remarks>
/// Las dos formas de enseñar el color no salen nunca a la vez: en GRAPHIC 2 hay una columna
/// con los dos colores de cada línea y en GRAPHIC 1 los 32 pares de los grupos. Enseñar la
/// columna en screen 1 sería ofrecer una decisión que la máquina no puede cumplir.
/// </remarks>
public class Graphic1EditorTests
{
    private static TileSetEditorViewModel Editor(TileSet.GraphicMode mode) =>
        new(new TileSet("Mazmorra", mode), ColorPalette.CreateMsxStandard());

    // ------------------------------------------------------------------ el view model

    [AvaloniaFact]
    public void En_screen_1_hay_treinta_y_dos_pares_y_no_columna_de_lineas()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic1);

        Assert.True(editor.IsGraphic1);
        Assert.False(editor.ShowsRowColors);
        Assert.Equal(TileSet.ColorGroupCount, editor.GroupColors.Count);
    }

    /// <summary>Y en screen 2 sigue todo como estaba: la columna sí, los pares no.</summary>
    [AvaloniaFact]
    public void En_screen_2_no_hay_pares_y_si_columna_de_lineas()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic2);

        Assert.False(editor.IsGraphic1);
        Assert.True(editor.ShowsRowColors);
        Assert.Empty(editor.GroupColors);
        Assert.Equal(Tile.Rows, editor.RowColors.Count);
    }

    /// <summary>Cada par dice a qué tiles pinta, que es lo que se lee a su lado.</summary>
    [AvaloniaFact]
    public void Cada_par_dice_a_que_tiles_pinta()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic1);

        Assert.Equal("0-7", editor.GroupColors[0].Range);
        Assert.Equal("248-255", editor.GroupColors[31].Range);
    }

    /// <summary>
    /// Elegir un color repinta las ocho miniaturas del grupo, no una.
    /// </summary>
    /// <remarks>
    /// Es la diferencia con elegir el color de una línea, y es lo que hace visible la
    /// limitación del modo: se ve al momento que el color no era sólo del tile que se estaba
    /// mirando.
    /// </remarks>
    [AvaloniaFact]
    public void Elegir_un_color_repinta_las_ocho_miniaturas()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic1);
        TileSet tileSet = editor.TileSet;

        // Un pixel encendido en cada uno de los ocho, para que el frente se vea.
        for (int tile = 8; tile <= 15; tile++)
            tileSet.ListOfTiles[tile].ArrayTileRows[0].ArrayPattern[0] = true;

        editor.GroupColors[1].PickForegroundCommand.Execute(editor.ColorPalette[6]);

        for (int tile = 8; tile <= 15; tile++)
        {
            Assert.Equal(6, tileSet.ListOfTiles[tile].ArrayTileRows[0].ForeColor);
            Assert.Equal(
                PixelReader.Bgra(editor.ColorPalette[6].Color),
                PixelReader.At(tileSet.ListOfTiles[tile].ImageMini!, 0, 0));
        }
    }

    /// <summary>Y deja el juego sin guardar, que la tabla de colores va en su fichero.</summary>
    [AvaloniaFact]
    public void Elegir_un_color_deja_el_juego_sin_guardar()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic1);
        editor.MarkClean();

        editor.GroupColors[0].PickBackgroundCommand.Execute(editor.ColorPalette[3]);

        Assert.True(editor.IsModified);
    }

    /// <summary>
    /// Intercambiar el par de un grupo deja los ocho tiles viéndose igual.
    /// </summary>
    /// <remarks>
    /// Es la misma propiedad que en una línea de GRAPHIC 2: un dibujo se puede escribir de
    /// dos maneras —frente A sobre fondo B con unos bits, o frente B sobre fondo A con esos
    /// bits al revés— y en pantalla no se distinguen. Aquí hace falta para poder juntar en un
    /// grupo dibujos que vengan escritos al revés unos de otros.
    /// </remarks>
    [AvaloniaFact]
    public void Intercambiar_el_par_deja_los_tiles_viendose_igual()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic1);
        TileSet tileSet = editor.TileSet;

        editor.GroupColors[0].PickForegroundCommand.Execute(editor.ColorPalette[6]);
        editor.GroupColors[0].PickBackgroundCommand.Execute(editor.ColorPalette[3]);

        // Encender el bit en la entidad no repinta nada por sí solo, y la miniatura se
        // quedaría con el pixel apagado: lo que se compararía después sería con eso.
        tileSet.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[0] = true;
        editor.RenderCurrent();

        int before = PixelReader.At(tileSet.ListOfTiles[0].ImageMini!, 0, 0);
        int beforeOff = PixelReader.At(tileSet.ListOfTiles[0].ImageMini!, 1, 0);

        editor.GroupColors[0].SwapColorsCommand.Execute(null);

        Assert.Equal(3, tileSet.ColorGroups[0].ForeColor);
        Assert.Equal(6, tileSet.ColorGroups[0].BackColor);

        // El bit se ha dado la vuelta con el par, así que el pixel se ve igual que antes.
        Assert.False(tileSet.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[0]);
        Assert.Equal(before, PixelReader.At(tileSet.ListOfTiles[0].ImageMini!, 0, 0));
        Assert.Equal(beforeOff, PixelReader.At(tileSet.ListOfTiles[0].ImageMini!, 1, 0));
    }

    /// <summary>
    /// Estampar un trozo traído de otro juego refresca la muestra del par.
    /// </summary>
    /// <remarks>
    /// La muestra lee el color del grupo cuando se le pregunta, así que el valor siempre está
    /// bien; lo que faltaba era el aviso. Sin él, la fila de 32 pares se quedaba con los
    /// colores de antes hasta que tocaras algo, y lo que se veía abajo no era lo que se veía
    /// en la rejilla.
    /// </remarks>
    [AvaloniaFact]
    public void Estampar_refresca_la_muestra_del_grupo()
    {
        TileSetEditorViewModel editor = Editor(TileSet.GraphicMode.Graphic1);

        var origin = new TileSet("Bosque");

        foreach (TileRow row in origin.ListOfTiles[0].ArrayTileRows)
        {
            row.ForeColor = 2;
            row.BackColor = 3;
            row.ArrayPattern[0] = true;
        }

        TileGroupColorViewModel swatch = editor.GroupColors[0];

        int notices = 0;
        swatch.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TileGroupColorViewModel.Foreground))
                notices++;
        };

        editor.InHand = new CopiedTiles(origin.Copy(0, 0, 1, 1), [], origin.Name);
        editor.StampAt(0, 0);

        Assert.Equal(2, editor.TileSet.ColorGroups[0].ForeColor);
        Assert.Equal(editor.ColorPalette[2], swatch.Foreground);
        Assert.True(notices > 0, "la muestra no se ha enterado de que su grupo ha cambiado de color");
    }

    // ------------------------------------------------------------------ la vista

    /// <summary>
    /// La columna de colores por línea no está en pantalla en screen 1.
    /// </summary>
    /// <remarks>
    /// Se mira si se ve de verdad y no sólo la propiedad del view model: entre las dos hay un
    /// enlace, y es el enlace lo que se estaría dando por bueno sin comprobarlo.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(TileSet.GraphicMode.Graphic1, false)]
    [InlineData(TileSet.GraphicMode.Graphic2, true)]
    public void La_columna_de_lineas_solo_se_ve_en_screen_2(TileSet.GraphicMode mode, bool visible)
    {
        Mounted(mode, (view, _) =>
        {
            Control strip = Named<ItemsControl>(view, "RowColorStrip");

            Assert.Equal(visible, strip.IsEffectivelyVisible);
        });
    }

    /// <summary>
    /// Los 32 pares salen debajo de la rejilla de tiles, no al lado del lienzo.
    /// </summary>
    /// <remarks>
    /// Debajo de los tiles a los que pintan, que es donde se mira para decidir en qué grupo
    /// cae un dibujo. Se comprueba con las posiciones en pantalla y no con la fila y la
    /// columna del markup: lo que importa es dónde acaba viéndose, y la rejilla ocupa varias
    /// filas, así que el número de fila por sí solo no dice si queda encima o debajo.
    /// </remarks>
    [AvaloniaFact]
    public void Los_pares_salen_debajo_de_la_rejilla_de_tiles()
    {
        Mounted(TileSet.GraphicMode.Graphic1, (view, window) =>
        {
            Control pairs = Named<StackPanel>(view, "GroupColorStrip");

            Assert.True(pairs.IsEffectivelyVisible);

            Rect grid = Bounds(TileGrid(view), window);
            Rect strip = Bounds(pairs, window);

            Assert.True(
                strip.Top >= grid.Bottom - 1,
                $"los pares empiezan en {strip.Top} y la rejilla acaba en {grid.Bottom}");

            // Y a lo ancho van con la rejilla, que es de donde sale el sitio para ponerlos.
            Assert.True(
                strip.Left >= grid.Left - 8,
                $"los pares empiezan en x={strip.Left} y la rejilla en x={grid.Left}");
        });
    }

    /// <summary>Y en screen 2 no están, ni ocupan sitio.</summary>
    [AvaloniaFact]
    public void En_screen_2_los_pares_no_estan()
    {
        Mounted(TileSet.GraphicMode.Graphic2, (view, _) =>
            Assert.False(Named<StackPanel>(view, "GroupColorStrip").IsEffectivelyVisible));
    }

    /// <summary>
    /// El recuadro de la rejilla, no el ListBox de dentro.
    /// </summary>
    /// <remarks>
    /// El ListBox se dimensiona a sus 256 tiles y a zoom x1 son 64 pixeles de alto, así que
    /// cabe de sobra en el hueco y su borde inferior no dice nada de hasta dónde llega la
    /// rejilla. Lo que reserva el sitio es este recuadro, y es con lo que hay que comparar:
    /// midiendo el ListBox, devolverle a la rejilla la fila de abajo pasaba desapercibido.
    /// </remarks>
    private static Control TileGrid(TileSetEditorView view) =>
        Named<Border>(view, "TileGridPanel");

    private static T Named<T>(TileSetEditorView view, string name)
        where T : Control =>
        view.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    /// <summary>Dónde cae un control dentro de la ventana, ya con todo colocado.</summary>
    private static Rect Bounds(Control control, Window window)
    {
        Point origin = control.TranslatePoint(default, window)
                       ?? throw new InvalidOperationException("el control no está en la ventana");

        return new Rect(origin, control.Bounds.Size);
    }

    private static void Mounted(TileSet.GraphicMode mode, Action<TileSetEditorView, Window> check)
    {
        var view = new TileSetEditorView { DataContext = Editor(mode) };
        var window = new Window { Content = view, Width = 1400, Height = 900 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            check(view, window);
        }
        finally
        {
            window.Close();
        }
    }
}
