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
/// El fantasma que se ve bajo el ratón antes de estampar.
/// </summary>
/// <remarks>
/// Tiene que salir con la misma forma que lo que se va a soltar. Si no, enseña una cosa y cae
/// otra, que es peor que no enseñar nada: el fantasma existe justo para no tener que estampar
/// y deshacer hasta acertar.
/// </remarks>
public class StampGhostLayoutTests : IDisposable
{
    private readonly Window _window;
    private readonly TileSetEditorView _view;

    public StampGhostLayoutTests()
    {
        Editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        _view = new TileSetEditorView { DataContext = Editor };
        _window = new Window { Content = _view, Width = 1400, Height = 900 };

        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    private TileSetEditorViewModel Editor { get; }

    public void Dispose() => _window.Close();

    /// <summary>
    /// Una tira horizontal de tres se ve como una tira de tres, no repartida en dos filas.
    /// </summary>
    /// <remarks>
    /// Un UniformGrid al que no se le dice cuántas columnas tiene se reparte solo lo más
    /// cuadrado que puede: con tres miniaturas hace dos filas y el tercer tile aparece debajo
    /// del primero, en un sitio donde no va a caer nada.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void El_fantasma_de_una_tira_sale_en_una_sola_fila(int width)
    {
        Editor.Tool = TileTool.Stamp;
        Editor.SelectRegion(0, 0, width, 1);

        Hover(10, 3);

        UniformGrid grid = GhostPanel();

        Assert.Equal(width, grid.Columns);

        // Y lo que se ve, no sólo lo que se le pide: el alto del fantasma tiene que ser el de
        // una fila de miniaturas.
        Assert.True(
            grid.Bounds.Height <= _view.ThumbnailSize + 1,
            $"el fantasma mide {grid.Bounds.Height} de alto y una fila son {_view.ThumbnailSize}");
    }

    /// <summary>Y un rectángulo de dos filas se ve con dos filas.</summary>
    [AvaloniaFact]
    public void El_fantasma_de_un_rectangulo_conserva_su_forma()
    {
        Editor.Tool = TileTool.Stamp;
        Editor.SelectRegion(0, 0, 3, 2);

        Hover(10, 3);

        UniformGrid grid = GhostPanel();

        Assert.Equal(3, grid.Columns);
        Assert.True(
            grid.Bounds.Height <= (_view.ThumbnailSize * 2) + 1,
            $"el fantasma mide {grid.Bounds.Height} de alto y dos filas son {_view.ThumbnailSize * 2}");
    }

    /// <summary>Lo mismo con un trozo traído de otro juego, que es de donde salió el fallo.</summary>
    [AvaloniaFact]
    public void El_fantasma_de_un_trozo_traido_tambien()
    {
        var origin = new TileSet("Cueva");

        Editor.Tool = TileTool.Stamp;
        Editor.InHand = new CopiedTiles(
            origin.Copy(0, 0, 3, 1),
            [.. origin.ListOfTiles.Take(3).Select(tile => tile.ImageMini!)],
            origin.Name);

        Hover(10, 3);

        Assert.Equal(3, GhostPanel().Columns);
    }

    /// <summary>El panel que el ItemsControl del fantasma acaba creando de verdad.</summary>
    private UniformGrid GhostPanel()
    {
        ItemsControl ghost = _view.GetVisualDescendants()
            .OfType<ItemsControl>()
            .First(control => control.Name == "StampGhost");

        Assert.True(ghost.IsVisible, "el fantasma no se ha llegado a enseñar");

        return ghost.GetVisualDescendants().OfType<UniformGrid>().First();
    }

    /// <summary>Pasa el ratón por el centro de una celda de la rejilla de tiles.</summary>
    private void Hover(int column, int row)
    {
        var grid = (ListBox)_view.GetVisualDescendants()
            .OfType<ListBox>()
            .First(list => list.Name == "TileGrid");

        Control cell = (Control)grid.ContainerFromIndex((row * TileSet.Columns) + column)!;

        Point point = cell
            .TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), _window)!
            .Value;

        _window.MouseMove(point);

        Dispatcher.UIThread.RunJobs();
    }
}
