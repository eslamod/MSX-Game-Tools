using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using MSX_SpritesEditor.Views;
using Xunit;

namespace MSX_SpritesEditor.Tests;

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
        var palettes = new PaletteLibrary();
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), palettes);

        // Un pixel encendido en el último tile, para mirar lejos del actual.
        editor.TileSet.ListOfTiles[255].ArrayTileRows[0].ArrayPattern[0] = true;
        editor.TileSet.ListOfTiles[255].ArrayTileRows[0].ForeColor = 5;

        ColorPalette nocturna = palettes.Add("Nocturna");
        nocturna[5].SetComponents(7, 0, 7);
        palettes.ActivePalette = nocturna;

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

        Canvas canvas = view.GetVisualDescendants().OfType<Canvas>().Single();

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

    private static TileSetEditorViewModel NewEditor() =>
        new(new TileSet("Bosque"), new PaletteLibrary());

    private static int ToBgra(Color color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
}
