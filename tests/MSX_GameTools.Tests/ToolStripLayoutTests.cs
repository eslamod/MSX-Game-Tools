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

/// <summary>
/// Las tiras de herramientas: que el texto de los botones quepa y que la línea de la
/// pestaña seleccionada no se meta encima del texto.
/// </summary>
public class ToolStripLayoutTests : IDisposable
{
    /// <summary>Pixeles de aire que se le exigen al texto dentro de su botón.</summary>
    /// <remarks>
    /// No basta con que quepa justo. Antes cabía con cero de margen —el hueco medía 16 y
    /// el texto pedía 16.00— y en pantalla salía cortado: cualquier redondeo o una fuente
    /// un pelo más ancha se lo come.
    /// </remarks>
    private const double MinimumSlack = 3;

    private readonly Window _window;
    private readonly MapEditorView _view;

    public ToolStripLayoutTests()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 16, 16), tiles);

        _view = new MapEditorView { DataContext = editor };
        _window = new Window { Content = _view, Width = 1100, Height = 800 };

        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        _window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void El_texto_de_los_botones_de_zoom_cabe_con_holgura()
    {
        RadioButton[] buttons = [.. _view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Where(button => button.GroupName == "MapTileZoom")];

        Assert.NotEmpty(buttons);

        foreach (RadioButton button in buttons)
        {
            TextBlock text = button.GetVisualDescendants().OfType<TextBlock>().First();

            double room = button.Bounds.Width
                - button.Padding.Left - button.Padding.Right
                - button.BorderThickness.Left - button.BorderThickness.Right;

            double needed = Unconstrained(text);

            Assert.True(
                room >= needed + MinimumSlack,
                $"«{button.Content}» tiene {room:0.0} de hueco y el texto pide {needed:0.00}: "
                + $"se queda en {room - needed:0.00} de aire y hacen falta {MinimumSlack}.");
        }
    }

    /// <summary>
    /// La línea de la pestaña seleccionada va debajo del texto, no encima.
    /// </summary>
    /// <remarks>
    /// Fluent la mete dentro de la caja del contenido contando con pestañas altas. Con las
    /// nuestras, esa caja mide justo lo que el texto y la línea le caía encima: medido, el
    /// texto ocupaba de 6 a 22 y la línea de 18 a 20.
    /// </remarks>
    [AvaloniaFact]
    public void La_linea_de_la_pestaña_no_se_mete_encima_del_texto()
    {
        TabControl tabs = _view.GetVisualDescendants().OfType<TabControl>().First();

        TabItem tab = tabs.GetVisualDescendants().OfType<TabItem>().First(item => item.IsSelected);

        TextBlock text = tab.GetVisualDescendants().OfType<TextBlock>().First();
        Border pipe = tab.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_SelectedPipe");

        double bottomOfText = text.TranslatePoint(new Point(0, text.Bounds.Height), tab)!.Value.Y;
        double topOfPipe = pipe.TranslatePoint(new Point(0, 0), tab)!.Value.Y;

        Assert.True(
            topOfPipe >= bottomOfText,
            $"El texto acaba en {bottomOfText:0.0} y la línea empieza en {topOfPipe:0.0}: se solapan.");

        // Y sigue dentro de la pestaña, no colgando por debajo.
        Assert.True(
            topOfPipe + pipe.Bounds.Height <= tab.Bounds.Height,
            $"La línea acaba en {topOfPipe + pipe.Bounds.Height:0.0} y la pestaña mide {tab.Bounds.Height:0.0}.");
    }

    /// <summary>
    /// Lo que mide el texto sin que nadie lo apriete.
    /// </summary>
    /// <remarks>
    /// El <c>DesiredSize</c> del que está montado ya viene recortado a lo que le dejaron,
    /// así que preguntándole a él siempre parece que cabe. Ése fue el primer intento de
    /// esta prueba, y pasaba con los botones cortados delante.
    /// </remarks>
    private static double Unconstrained(TextBlock text)
    {
        var loose = new TextBlock
        {
            Text = text.Text,
            FontSize = text.FontSize,
            FontFamily = text.FontFamily,
            FontWeight = text.FontWeight,
        };

        loose.Measure(Size.Infinity);

        return loose.DesiredSize.Width;
    }
}
