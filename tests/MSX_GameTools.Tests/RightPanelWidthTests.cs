using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El ancho del panel lateral.
/// </summary>
/// <remarks>
/// Se mide con la ventana montada porque es puro reparto de columnas: mirar el XAML no
/// dice si el separador llega a mover algo ni si el lateral desaparece cuando no hay
/// ningún panel.
/// </remarks>
public class RightPanelWidthTests : IDisposable
{
    private const double Minimum = 380;
    private const double Maximum = 760;

    private readonly MainWindowViewModel _main = new();
    private readonly MainWindow _window;

    public RightPanelWidthTests()
    {
        _window = new MainWindow { DataContext = _main, Width = 1400, Height = 800 };
        _window.Show();
        Pump();
    }

    public void Dispose() => _window.Close();

    /// <summary>Sin nada abierto el lateral no ocupa, que si no se comería la pantalla.</summary>
    [AvaloniaFact]
    public void Sin_panel_abierto_el_lateral_no_ocupa_nada()
    {
        Assert.Equal(0, PanelWidth);
        Assert.False(Splitter.IsVisible);
    }

    [AvaloniaFact]
    public void Al_abrir_un_panel_arranca_con_el_ancho_minimo()
    {
        OpenBlocks();

        Assert.Equal(Minimum, PanelWidth);
        Assert.True(Splitter.IsVisible);
    }

    [AvaloniaFact]
    public void Arrastrar_el_separador_a_la_izquierda_ensancha_el_lateral()
    {
        OpenBlocks();

        Drag(-120);

        Assert.Equal(Minimum + 120, PanelWidth, precision: 0);
    }

    /// <summary>Estrechar por debajo del mínimo dejaría el panel inservible.</summary>
    [AvaloniaFact]
    public void No_se_puede_estrechar_por_debajo_del_minimo()
    {
        OpenBlocks();

        Drag(200);

        Assert.Equal(Minimum, PanelWidth, precision: 0);
    }

    [AvaloniaFact]
    public void No_se_puede_ensanchar_por_encima_del_maximo()
    {
        OpenBlocks();

        Drag(-600);

        Assert.Equal(Maximum, PanelWidth, precision: 0);
    }

    /// <summary>Al cerrar el último panel el lateral vuelve a no ocupar.</summary>
    [AvaloniaFact]
    public void Cerrar_el_panel_devuelve_el_sitio()
    {
        OpenBlocks();
        Drag(-100);

        _main.CloseRightPanelCommand.Execute(_main.RightPanViewModel);
        Pump();

        Assert.Equal(0, PanelWidth);
    }

    /// <summary>
    /// Lo que ocupa el lateral. Se mide la columna y no el TabControl: al ocultarse, sus
    /// Bounds se quedan con lo ultimo que se le repartio y mentirian.
    /// </summary>
    private double PanelWidth => _window.FindControl<Grid>("MainArea")!.ColumnDefinitions[4].ActualWidth;

    private GridSplitter Splitter => _window.GetVisualDescendants()
        .OfType<GridSplitter>()
        .Single(splitter => splitter.ResizeDirection == GridResizeDirection.Columns
                            && Grid.GetColumn(splitter) == 3);

    private void OpenBlocks()
    {
        _main.OpenTileSet(new TileSet("Bosque"));
        _main.OpenTreeItemCommand.Execute(
            _main.TreeGeneralVm.PrimaryNodes[1].Childs[0].Childs[0]);

        Pump();
    }

    /// <summary>Mueve el separador. Hacia la izquierda son valores negativos.</summary>
    private void Drag(double dx)
    {
        Point origin = Splitter.TranslatePoint(new Point(0, 0), _window)
                       ?? throw new InvalidOperationException("El separador no está montado.");

        var from = new Point(origin.X + (Splitter.Bounds.Width / 2), origin.Y + 100);

        _window.MouseDown(from, MouseButton.Left);
        Pump();
        _window.MouseMove(new Point(from.X + dx, from.Y));
        Pump();
        _window.MouseUp(new Point(from.X + dx, from.Y), MouseButton.Left);
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
