using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Dónde acaban los controles del editor de sprites.
/// </summary>
/// <remarks>
/// Se mide con la ventana montada porque es reparto de celdas: en el XAML, un
/// <c>Grid.Row</c> sólo significa algo si el padre es esa rejilla, y al anidar un control
/// en otro contenedor la propiedad se queda puesta sin hacer nada. Leyendo el fichero eso
/// no se ve.
/// </remarks>
public class SpriteEditorLayoutTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxlay-{Guid.NewGuid():N}");

    public SpriteEditorLayoutTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>
    /// Los controles de la referencia van debajo del lienzo y ocupan la columna entera.
    /// </summary>
    /// <remarks>
    /// Al envolver el lienzo en un ScrollViewer se colaron dentro de él, así que salían a
    /// la derecha del lienzo y recortados a 91 pixeles: el combo del fichero y la etiqueta
    /// de la celda quedaban cortados, y la opacidad medio fuera.
    /// </remarks>
    [AvaloniaFact]
    public void Los_controles_del_fondo_van_debajo_del_lienzo()
    {
        using Harness harness = Mount();

        StackPanel background = harness.View.GetVisualDescendants()
            .OfType<StackPanel>()
            .First(panel => panel.Margin == new Thickness(6, 54, 6, 0));

        ScrollViewer canvas = harness.View.GetVisualDescendants().OfType<ScrollViewer>().First();

        // Cuelga de la rejilla del editor, que es lo que hace valer su Grid.Row.
        Assert.IsType<Grid>(background.GetVisualParent());
        Assert.Equal(2, Grid.GetRow(background));
        Assert.Equal(0, Grid.GetColumn(background));

        double canvasBottom = canvas.TranslatePoint(new Point(0, 0), harness.View)!.Value.Y
                              + canvas.Bounds.Height;

        Assert.True(
            background.TranslatePoint(new Point(0, 0), harness.View)!.Value.Y > canvasBottom,
            "Los controles del fondo tienen que quedar por debajo del lienzo.");

        // Y con la columna entera: apretados no caben ni el combo ni la etiqueta.
        Assert.True(background.Bounds.Width > 200, $"Se han quedado en {background.Bounds.Width} de ancho.");
    }

    private Harness Mount()
    {
        string path = Path.Combine(_folder, "fondo.png");

        new WriteableBitmap(new PixelSize(64, 64), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul)
            .Save(path, new PngBitmapEncoderOptions());

        var backgrounds = new ReferenceImageLibrary();

        var editor = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard(), null, backgrounds);

        // Sin imagen cargada los controles del fondo ni existen.
        backgrounds.Load(path, cellSize: 16);

        var view = new SpritesEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 1400, Height = 900 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Harness(window, view);
    }

    private sealed record Harness(Window Window, SpritesEditorView View) : IDisposable
    {
        public void Dispose() => Window.Close();
    }
}
