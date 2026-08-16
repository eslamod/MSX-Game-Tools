using Avalonia;
using Avalonia.Controls;
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
/// Un arrastre es un solo paso de deshacer.
/// </summary>
/// <remarks>
/// <para>
/// Antes cada celda que cruzaba el ratón era su propio paso. Un arrastre despistado
/// —creyendo estar en seleccionar y estando en estampar— ponía treinta tiles y se llevaba
/// por delante los veinte pasos de historia. El deshacer quedaba inservible justo en el
/// momento en que hacía falta.
/// </para>
/// <para>
/// Se prueba moviendo el ratón de verdad sobre el lienzo montado, y no llamando a la pila.
/// Lo que hay que vigilar no es que la pila sepa agrupar —eso ya lo sabía, con
/// <c>MapEditGroup</c>— sino que alguien le diga dónde empieza y dónde acaba el arrastre.
/// </para>
/// </remarks>
public class MapUndoStrokeTests : IDisposable
{
    /// <summary>Celdas que cruza el arrastre, contando la del botón pulsado.</summary>
    private const int Painted = 5;

    private readonly Window _window;
    private readonly MapEditorViewModel _editor;
    private readonly MapCanvas _canvas;

    public MapUndoStrokeTests()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        _editor = main.OpenMap(new TileMap("Nivel", 40, 30), tiles);

        var view = new MapEditorView { DataContext = _editor };

        _window = new Window { Content = view, Width = 1100, Height = 800 };
        _window.Show();
        Pump();

        _canvas = view.GetVisualDescendants().OfType<MapCanvas>().First();
    }

    public void Dispose()
    {
        _window.Close();
        Pump();
    }

    private TileMap Map => _editor.Map;

    private TileGrid Grid => Map.Layers[0].Grid;

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    /// <summary>Lo que el usuario describió: arrastrar y poder volver atrás con un deshacer.</summary>
    [AvaloniaFact]
    public void Un_arrastre_se_deshace_de_una_vez()
    {
        DragAcross();

        Assert.Equal(Painted, PaintedCells());

        _editor.UndoCommand.Execute(null);

        Assert.Equal(0, PaintedCells());
    }

    /// <summary>Y rehacer lo devuelve entero, no celda a celda.</summary>
    [AvaloniaFact]
    public void Y_se_rehace_de_una_vez()
    {
        DragAcross();

        _editor.UndoCommand.Execute(null);
        _editor.RedoCommand.Execute(null);

        Assert.Equal(Painted, PaintedCells());
    }

    /// <summary>
    /// El arrastre gasta un paso de los veinte, no cinco.
    /// </summary>
    /// <remarks>
    /// Es la queja original dicha con números: lo que dolía no era no poder deshacer el
    /// arrastre, sino que al deshacerlo ya no quedara nada detrás.
    /// </remarks>
    [AvaloniaFact]
    public void El_arrastre_solo_gasta_un_paso_de_la_historia()
    {
        // Un cambio anterior que tiene que seguir estando después del arrastre.
        Map.Stamp(0, 20, 20, TilePatch.Single(3));

        DragAcross();

        // Dos deshaceres: el arrastre y el cambio de antes. Si cada celda fuera un paso,
        // el segundo deshacer estaría todavía en mitad del arrastre.
        _editor.UndoCommand.Execute(null);
        _editor.UndoCommand.Execute(null);

        Assert.Null(Grid[20, 20]);
        Assert.False(_editor.UndoCommand.CanExecute(null));
    }

    /// <summary>
    /// Perder la captura del ratón cierra el trazo igual que soltarlo.
    /// </summary>
    /// <remarks>
    /// Si no, el trazo se queda abierto para siempre y deshacer deja de anotar en silencio,
    /// que es peor que el fallo que veníamos a arreglar: aquel al menos se veía.
    /// </remarks>
    [AvaloniaFact]
    public void Perder_el_raton_a_media_tambien_cierra_el_paso()
    {
        _window.MouseDown(Cell(1, 1), MouseButton.Left);
        _window.MouseMove(Cell(2, 1), RawInputModifiers.LeftMouseButton);
        Pump();

        // El sistema se lleva el puntero: no llega ningún soltar.
        _canvas.RaiseEvent(new PointerCaptureLostEventArgs(_canvas, null!));
        Pump();

        Assert.False(Map.Undo.IsStroking);

        _editor.UndoCommand.Execute(null);

        Assert.Equal(0, PaintedCells());
    }

    /// <summary>Arrastra en horizontal por <see cref="Painted" /> celdas seguidas.</summary>
    private void DragAcross()
    {
        _window.MouseDown(Cell(1, 1), MouseButton.Left);

        for (int column = 2; column < 1 + Painted; column++)
        {
            _window.MouseMove(Cell(column, 1), RawInputModifiers.LeftMouseButton);
            Pump();
        }

        _window.MouseUp(Cell(Painted, 1), MouseButton.Left);
        Pump();
    }

    private int PaintedCells()
    {
        int count = 0;

        for (int column = 0; column < Painted + 2; column++)
        {
            if (Grid[column, 1] is not null)
                count++;
        }

        return count;
    }

    /// <summary>El centro de esa celda, en coordenadas de la ventana.</summary>
    private Point Cell(int column, int row) =>
        _canvas.TranslatePoint(
            new Point(
                ((column + 0.5) * _canvas.CellWidth) - 0,
                ((row + 0.5) * _canvas.CellHeight) - 0),
            _window)!.Value;
}
