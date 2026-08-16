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
        var editor = Editor(mode);
        var view = new TileSetEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 1400, Height = 900 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            Control strip = view.GetVisualDescendants()
                .OfType<ItemsControl>()
                .First(control => control.Name == "RowColorStrip");

            Assert.Equal(visible, strip.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
