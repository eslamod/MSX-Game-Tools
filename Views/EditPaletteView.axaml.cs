using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

/// <summary>
/// El panel de la paleta. Arrastrar un color sobre otro los intercambia de índice.
/// </summary>
/// <remarks>
/// El arrastre no empieza al pulsar sino al mover: pulsar es elegir el color que editan
/// los sliders, que es lo que más se hace, y arrancar el arrastre en el botón abajo
/// dejaría la lista imposible de usar para lo otro.
/// </remarks>
public partial class EditPaletteView : UserControl
{
    /// <summary>
    /// Lo que viaja en el arrastre: el color de origen, tal cual.
    /// </summary>
    /// <remarks>
    /// En proceso, que es lo que esto es: se arrastra dentro de nuestra propia lista y no
    /// hace falta serializar nada, así que al soltar llega el mismo objeto y se le pregunta
    /// el índice. El identificador sólo admite letras, dígitos, punto y guion —una barra
    /// revienta el inicializador de la clase con «Invalid application identifier»—.
    /// </remarks>
    private static readonly DataFormat<PaletteColor> ColorFormat =
        DataFormat.CreateInProcessFormat<PaletteColor>("msx-gametools.palette-color");

    /// <summary>Clase de la entrada marcada como destino, que le pone el recuadro.</summary>
    private const string DropClass = "drop";

    /// <summary>Lo que hay que mover para que sea un arrastre y no un clic tembloroso.</summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// Dónde y sobre qué color se pulsó, mientras no se sepa si es un arrastre.
    /// </summary>
    /// <remarks>
    /// Se guarda también el evento de la pulsación: <c>DoDragDropAsync</c> pide un
    /// <see cref="PointerPressedEventArgs"/>, y aquí el arrastre no arranca al pulsar sino
    /// al mover, que es lo que deja la lista usable para elegir color.
    /// </remarks>
    private (Point At, PaletteColor Color, PointerPressedEventArgs Args)? _pressed;

    private ListBoxItem? _marked;

    public EditPaletteView()
    {
        InitializeComponent();

        // Por la bajada y no por la subida: el ListBoxItem marca como manejado el
        // PointerPressed al seleccionar la fila, y un manejador normal —el que se pone
        // desde el XAML— no recibe los eventos ya manejados. Enganchado ahí, el arrastre
        // no llegaba a empezar nunca por mucho que se moviera el raton.
        AddHandler(PointerPressedEvent, OnColorPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnColorMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnColorReleased, RoutingStrategies.Tunnel);

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    /// <summary>Índice del color que se ha pulsado y todavía no se ha soltado.</summary>
    /// <remarks>
    /// Expuesto para poder comprobarlo desde fuera: que la pulsación llegue hasta aquí es
    /// justo lo que fallaba, y el arrastre de verdad no se puede simular sin sistema
    /// operativo debajo.
    /// </remarks>
    public int? PressedIndex => _pressed?.Color.Index;

    private EditPaletteViewModel? Editor => DataContext as EditPaletteViewModel;

    private void OnColorPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = null;

        if (Editor is not { } editor
            || !e.GetCurrentPoint(ColorList).Properties.IsLeftButtonPressed
            || ColorUnder(e.Source) is not { } color
            || !editor.Palette.CanSwap(color.Index))
        {
            return;
        }

        _pressed = (e.GetPosition(ColorList), color, e);
    }

    private void OnColorMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is not { } start || Editor is null)
            return;

        if (!e.GetCurrentPoint(ColorList).Properties.IsLeftButtonPressed)
        {
            _pressed = null;
            return;
        }

        Point now = e.GetPosition(ColorList);

        if (Math.Abs(now.X - start.At.X) < DragThreshold && Math.Abs(now.Y - start.At.Y) < DragThreshold)
            return;

        // Una sola vez por arrastre: DoDragDropAsync se queda dentro hasta que se suelta.
        _pressed = null;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ColorFormat, start.Color));

        _ = DragDrop.DoDragDropAsync(start.Args, data, DragDropEffects.Move);
    }

    private void OnColorReleased(object? sender, PointerReleasedEventArgs e) => _pressed = null;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TargetOf(e) is null ? DragDropEffects.None : DragDropEffects.Move;

        Mark(e.DragEffects == DragDropEffects.None ? null : ItemUnder(e.Source));
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => Mark(null);

    private void OnDrop(object? sender, DragEventArgs e)
    {
        Mark(null);

        if (TargetOf(e) is not (int from, int to))
            return;

        Editor?.SwapColors(from, to);

        e.Handled = true;
    }

    /// <summary>Origen y destino del arrastre, si el que hay debajo sirve de destino.</summary>
    private (int From, int To)? TargetOf(DragEventArgs e)
    {
        if (Editor is not { } editor
            || e.DataTransfer?.TryGetValue(ColorFormat) is not { } dragged
            || ColorUnder(e.Source) is not { } target
            || target.Index == dragged.Index
            || !editor.Palette.CanSwap(target.Index))
        {
            return null;
        }

        return (dragged.Index, target.Index);
    }

    /// <summary>Deja el recuadro sólo en ésa, quitándolo de la que lo tuviera.</summary>
    private void Mark(ListBoxItem? item)
    {
        if (ReferenceEquals(_marked, item))
            return;

        _marked?.Classes.Remove(DropClass);
        _marked = item;
        _marked?.Classes.Add(DropClass);
    }

    private static Entities.PaletteColor? ColorUnder(object? source) =>
        ItemUnder(source)?.DataContext as Entities.PaletteColor;

    private static ListBoxItem? ItemUnder(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
}
