using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El editor de un juego de tiles: navegación, colores por línea y pintado.</summary>
public class TileSetEditorTests
{
    [AvaloniaFact]
    public void Arranca_en_el_primer_tile_con_las_256_miniaturas()
    {
        TileSetEditorViewModel editor = NewEditor();

        Assert.Equal(256, editor.Thumbnails.Count);
        Assert.Equal(1, editor.CurrentTilePosition);
        Assert.Equal("1 / 256", editor.TileLabel);
        Assert.False(editor.PreviousTileCommand.CanExecute(null));
        Assert.True(editor.NextTileCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Las_flechas_recorren_el_juego_y_se_paran_en_los_extremos()
    {
        TileSetEditorViewModel editor = NewEditor();

        editor.NextTileCommand.Execute(null);

        Assert.Equal(2, editor.CurrentTilePosition);
        Assert.Same(editor.TileSet.ListOfTiles[1], editor.CurrentTile);

        editor.PreviousTileCommand.Execute(null);

        Assert.Equal(1, editor.CurrentTilePosition);

        editor.GoTo(255);

        Assert.Equal("256 / 256", editor.TileLabel);
        Assert.False(editor.NextTileCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Seleccionar_una_miniatura_lleva_el_lienzo_a_ese_tile()
    {
        TileSetEditorViewModel editor = NewEditor();

        editor.SelectedThumbnail = editor.Thumbnails[42];

        Assert.Equal(43, editor.CurrentTilePosition);
        Assert.Same(editor.TileSet.ListOfTiles[42], editor.CurrentTile);
    }

    [AvaloniaFact]
    public void Navegar_mueve_la_seleccion_de_la_rejilla()
    {
        TileSetEditorViewModel editor = NewEditor();

        editor.GoTo(7);

        Assert.Same(editor.TileSet.ListOfTiles[7].ImageMini, editor.SelectedThumbnail);
    }

    /// <summary>Las ocho casillas se reutilizan: al cambiar de tile se repuntan.</summary>
    [AvaloniaFact]
    public void La_tira_de_colores_sigue_al_tile_actual()
    {
        TileSetEditorViewModel editor = NewEditor();

        Assert.Equal(8, editor.RowColors.Count);

        editor.TileSet.ListOfTiles[3].ArrayTileRows[0].ForeColor = 6;
        editor.GoTo(3);

        Assert.Equal(6, editor.RowColors[0].Foreground.Index);
    }

    [AvaloniaFact]
    public void Elegir_los_colores_de_una_linea_repinta_su_miniatura()
    {
        TileSetEditorViewModel editor = NewEditor();
        ColorPalette palette = editor.ColorPalette;

        editor.CurrentTile.ArrayTileRows[0].ArrayPattern[0] = true;
        editor.RowColors[0].PickForegroundCommand.Execute(palette[8]);
        editor.RowColors[0].PickBackgroundCommand.Execute(palette[4]);

        ImageMini mini = editor.CurrentTile.ImageMini!;

        Assert.Equal(ToBgra(palette.GetColor(8)), PixelReader.At(mini, 0, 0));
        Assert.Equal(ToBgra(palette.GetColor(4)), PixelReader.At(mini, 1, 0));
    }

    /// <summary>El lienzo es de 8x8 aquí, y de eso se entera por la superficie.</summary>
    [AvaloniaFact]
    public void La_superficie_del_lienzo_mide_ocho()
        => Assert.Equal(8, NewEditor().PixelSurface.Size);

    [AvaloniaFact]
    public void Pintar_por_la_superficie_enciende_el_pixel_y_su_miniatura()
    {
        TileSetEditorViewModel editor = NewEditor();
        editor.RowColors[2].PickForegroundCommand.Execute(editor.ColorPalette[9]);

        editor.PixelSurface.Set(5, 2, true);

        Assert.True(editor.CurrentTile.ArrayTileRows[2].ArrayPattern[5]);
        Assert.True(editor.PixelSurface.IsSet(5, 2));
        Assert.Equal(ToBgra(editor.ColorPalette.GetColor(9)), PixelReader.At(editor.CurrentTile.ImageMini!, 5, 2));
    }

    [AvaloniaFact]
    public void Cambiar_de_paleta_repinta_los_256()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        // Un pixel encendido en el último tile, para mirar lejos del actual.
        editor.TileSet.ListOfTiles[255].ArrayTileRows[0].ArrayPattern[0] = true;
        editor.TileSet.ListOfTiles[255].ArrayTileRows[0].ForeColor = 5;

        ColorPalette nocturna = editor.ColorPalette.Clone("Nocturna");
        nocturna[5].SetComponents(7, 0, 7);
        editor.ColorPalette = nocturna;

        Assert.Equal(
            ToBgra(nocturna.GetColor(5)),
            PixelReader.At(editor.TileSet.ListOfTiles[255].ImageMini!, 0, 0));
    }

    /// <summary>
    /// El lienzo de verdad montado: es lo que confirma que el control extraído sirve
    /// para 8x8 sin tocarlo, que era el motivo de sacarlo.
    /// </summary>
    [AvaloniaFact]
    public void El_lienzo_montado_dibuja_una_rejilla_de_ocho_por_ocho()
    {
        TileSetEditorViewModel editor = NewEditor();
        var view = new TileSetEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // El del lienzo de edición, que ya no es el único: la rejilla de la derecha lleva
        // encima otro para marcar y estampar.
        Canvas canvas = view.GetVisualDescendants()
            .OfType<PixelCanvas>()
            .Single()
            .GetVisualDescendants()
            .OfType<Canvas>()
            .Single();

        Assert.Equal(64, canvas.Children.Count);
        Assert.Equal(256d / 8, view.CellSize);
    }

    /// <summary>
    /// La rejilla es la de GIMP: una línea de un pixel marcando el borde de cada tile.
    /// Va sólo arriba y a la izquierda para que dos vecinos la compartan; con borde por
    /// los cuatro lados habría dos líneas entre tile y tile y quedarían separados.
    /// </summary>
    [AvaloniaFact]
    public void La_rejilla_de_miniaturas_no_separa_los_tiles()
    {
        var view = new TileSetEditorView { DataContext = NewEditor() };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.ShowGrid);
        Assert.Equal(new Thickness(1, 1, 0, 0), view.GridThickness);
        Assert.Equal(Brushes.Black, view.GridBrush);

        view.ShowGrid = false;
        Dispatcher.UIThread.RunJobs();

        // A cero y no sólo transparente: si no, al apagarla quedaría una separación.
        Assert.Equal(default, view.GridThickness);
        Assert.Equal(Brushes.Transparent, view.GridBrush);
    }

    /// <summary>
    /// El interruptor es de las miniaturas. En el lienzo de edición la rejilla de
    /// pixeles se ve siempre: ahí es para lo que sirve, y apagarla no tiene sentido.
    /// </summary>
    [AvaloniaFact]
    public void El_lienzo_de_edicion_no_pierde_su_rejilla()
    {
        var view = new TileSetEditorView { DataContext = NewEditor() };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        PixelCanvas canvas = view.GetVisualDescendants().OfType<PixelCanvas>().Single();

        view.ShowGrid = false;
        Dispatcher.UIThread.RunJobs();

        Assert.True(canvas.ShowGrid);
    }

    /// <summary>
    /// La rejilla se dimensiona a su contenido, no al hueco.
    /// </summary>
    /// <remarks>
    /// La ventana va ancha a proposito: el fallo aparece cuando <b>sobra</b> sitio. Si la
    /// lista se estira, el UniformGrid reparte el hueco entre sus 32 columnas y las 8
    /// filas, y los tiles dejan de ser cuadrados. Con la ventana estrecha el contenido
    /// toma su tamaño natural de todos modos y no se nota nada.
    /// </remarks>
    [AvaloniaFact]
    public void Los_tiles_no_se_estiran_cuando_sobra_sitio()
    {
        var view = new TileSetEditorView { DataContext = NewEditor() };
        var window = new Window { Content = view, Width = 2000, Height = 1000 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        ListBox grid = view.FindControl<ListBox>("TileGrid")!;

        // 32 columnas y 8 filas del lado de la miniatura mas su linea de rejilla, y el
        // cierre de la derecha y de abajo que pone el contenedor.
        double cell = view.ThumbnailSize + view.GridThickness.Left;

        Assert.Equal((32 * cell) + view.GridEdgeThickness.Right, grid.Bounds.Width);
        Assert.Equal((8 * cell) + view.GridEdgeThickness.Bottom, grid.Bounds.Height);
    }

    /// <summary>
    /// Cada tile lleva linea solo arriba y a la izquierda para compartirla con su vecino,
    /// asi que la ultima fila y la ultima columna se quedarian abiertas. Las cierra el
    /// contenedor con dos lineas, en vez de dos por cada uno de los 256 tiles.
    /// </summary>
    [AvaloniaFact]
    public void La_reticula_queda_cerrada_por_la_derecha_y_por_abajo()
    {
        var view = new TileSetEditorView { DataContext = NewEditor() };
        var window = new Window { Content = view, Width = 2000, Height = 1000 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        ListBox grid = view.FindControl<ListBox>("TileGrid")!;

        Assert.Equal(new Thickness(0, 0, 1, 1), grid.BorderThickness);
        Assert.Equal(view.GridBrush, grid.BorderBrush);

        // Y se apaga con el mismo interruptor que el resto de la rejilla.
        view.ShowGrid = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(default, grid.BorderThickness);
    }

    /// <summary>
    /// Los botones de zoom son barra de herramientas: no se mueven con el contenido.
    /// </summary>
    /// <remarks>
    /// Se probaron los cuatro niveles porque el fallo no estaba en el ScrollViewer sino
    /// en la aritmetica de columnas, y solo se notaba con el contenido bien ancho.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public void Los_botones_de_zoom_siguen_visibles_con_las_miniaturas_ampliadas(string zoom)
    {
        var view = new TileSetEditorView { DataContext = NewEditor() };
        var window = new Window { Content = view, Width = 1000, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        RadioButton[] buttons = [.. view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Where(r => r.GroupName == "TilePreviewZoom")];

        Assert.Equal(4, buttons.Length);

        buttons.Single(r => (string?)r.Tag == zoom).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        RadioButton last = buttons[^1];
        Point right = last.TranslatePoint(new Point(last.Bounds.Width, 0), window)!.Value;

        Assert.True(right.X <= window.Width, $"A X{zoom} la barra acaba en {right.X} y la ventana mide {window.Width}.");
    }

    /// <summary>El borde se ve en todos los tiles que usen el 0, no solo en el actual.</summary>
    [AvaloniaFact]
    public void Cambiar_el_borde_repinta_todas_las_miniaturas()
    {
        TileSetEditorViewModel editor = NewEditor();

        // Un tile lejos del actual, con el fondo de su primera linea transparente.
        editor.TileSet.ListOfTiles[200].ArrayTileRows[0].BackColor = 0;
        editor.BorderColorIndex = 1;

        Assert.Equal(
            ToBgra(editor.ColorPalette.GetColor(1)),
            PixelReader.At(editor.TileSet.ListOfTiles[200].ImageMini!, 0, 0));

        editor.PickBorderColorCommand.Execute(editor.ColorPalette[10]);

        Assert.Equal(10, editor.BorderColorIndex);
        Assert.Equal(
            ToBgra(editor.ColorPalette.GetColor(10)),
            PixelReader.At(editor.TileSet.ListOfTiles[200].ImageMini!, 0, 0));
    }

    /// <summary>El 0 no se ofrece como borde: es el transparente, no un color.</summary>
    [AvaloniaFact]
    public void El_borde_no_puede_ser_el_color_0()
        => Assert.DoesNotContain(NewEditor().BorderChoices, color => color.Index == 0);

    private static TileSetEditorViewModel NewEditor() =>
        new(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

    private static int ToBgra(Color color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
}
