using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

public partial class MapEditorView : UserControl
{
    /// <summary>Lado de un tile en el selector de abajo.</summary>
    public static readonly StyledProperty<double> TileSizeProperty =
        AvaloniaProperty.Register<MapEditorView, double>(nameof(TileSize), defaultValue: 16);

    /// <summary>Tiles por fila del selector, los mismos que el editor y el png.</summary>
    private const int TilesPerRow = 32;

    /// <summary>El tile por el que se empezó a arrastrar, para coger el rectángulo.</summary>
    private int _anchor = -1;

    private MapEditorViewModel? _subscribed;

    public MapEditorView() => InitializeComponent();

    public double TileSize
    {
        get => GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    private MapEditorViewModel? Editor => DataContext as MapEditorViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        Canvas.CellPressed += OnCellPressed;
        Canvas.CellDragged += OnCellDragged;
        Canvas.HoverChanged += OnHoverChanged;

        if (Editor is { } editor)
        {
            _subscribed = editor;
            editor.RefreshRequested += OnRefreshRequested;

            // Los bloques se editan en otro panel: al volver aqui pueden ser otros.
            editor.RefreshBlocks();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        Canvas.CellPressed -= OnCellPressed;
        Canvas.CellDragged -= OnCellDragged;
        Canvas.HoverChanged -= OnHoverChanged;

        if (_subscribed is not null)
        {
            _subscribed.RefreshRequested -= OnRefreshRequested;
            _subscribed = null;
        }

        base.OnUnloaded(e);
    }

    private void OnRefreshRequested() => Canvas.InvalidateVisual();

    // ------------------------------------------------------------------ el mapa

    /// <summary>Dónde empezó a marcarse la selección.</summary>
    private (int Column, int Row)? _selectingFrom;

    private void OnCellPressed(int column, int row)
    {
        if (Editor is not { } editor)
            return;

        if (editor.Tool == MapTool.Select)
        {
            _selectingFrom = (column, row);
            editor.Select(column, row, column, row);

            return;
        }

        editor.Paint(column, row);
    }

    private void OnCellDragged(int column, int row)
    {
        if (Editor is not { } editor)
            return;

        if (editor.Tool == MapTool.Select)
        {
            if (_selectingFrom is { } from)
                editor.Select(from.Column, from.Row, column, row);

            return;
        }

        editor.Paint(column, row);
    }

    private void OnHoverChanged((int Column, int Row)? cell)
    {
        if (Editor is { } editor)
            editor.Hover = cell;
    }

    private void OnToolChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag }
            && Enum.TryParse(tag, out MapTool tool)
            && Editor is { } editor)
        {
            editor.Tool = tool;
        }
    }

    private void OnFitZoom(object? sender, RoutedEventArgs e) =>
        Editor?.FitZoom(Canvas.Bounds.Width, Canvas.Bounds.Height);

    /// <summary>
    /// Control y rueda para el zoom, mayúsculas y rueda para pasar de bloque.
    /// </summary>
    /// <remarks>
    /// Va en la vista entera y no en el lienzo: al elegir bloques abajo también se quiere
    /// pasar al siguiente sin subir el ratón al mapa.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (Editor is not { } editor)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            editor.Zoom = Math.Clamp(
                editor.Zoom + Math.Sign(e.Delta.Y),
                MapEditorViewModel.MinZoom,
                MapEditorViewModel.MaxZoom);

            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            editor.StepBlock(-Math.Sign(e.Delta.Y));
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ el selector

    private void OnTilesPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Under<ImageMini>(TileChoices, e.GetPosition(TileChoices)) is not { } image)
            return;

        _anchor = Editor?.Tiles.IndexOf(image) ?? -1;

        if (_anchor >= 0)
            Editor?.PickTile(TilePatch.Single(_anchor), $"Tile {_anchor}");
    }

    /// <summary>Arrastrar coge el rectángulo, igual que en el panel de bloques.</summary>
    private void OnTilesMoved(object? sender, PointerEventArgs e)
    {
        if (_anchor < 0 || Editor is not { } editor)
            return;

        if (Under<ImageMini>(TileChoices, e.GetPosition(TileChoices)) is not { } image)
            return;

        int index = editor.Tiles.IndexOf(image);
        if (index < 0)
            return;

        editor.PickTile(Rectangle(_anchor, index), $"Tiles {_anchor}-{index}");
    }

    private void OnBlockPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: BlockChoiceViewModel choice })
            Editor?.PickBlock(choice.Block);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _anchor = -1;
        _selectingFrom = null;
    }

    /// <summary>El rectángulo de tiles que va de uno a otro en la disposición de 32.</summary>
    private static TilePatch Rectangle(int from, int to)
    {
        int left = Math.Min(from % TilesPerRow, to % TilesPerRow);
        int right = Math.Max(from % TilesPerRow, to % TilesPerRow);
        int top = Math.Min(from / TilesPerRow, to / TilesPerRow);
        int bottom = Math.Max(from / TilesPerRow, to / TilesPerRow);

        var patch = new TilePatch(right - left + 1, bottom - top + 1);

        for (int row = 0; row < patch.Height; row++)
        {
            for (int column = 0; column < patch.Width; column++)
                patch[column, row] = ((top + row) * TilesPerRow) + left + column;
        }

        return patch;
    }

    /// <inheritdoc cref="TileBlocksView.Under{T}"/>
    private static T? Under<T>(Control list, Point position)
        where T : class =>
        list.InputHitTest(position) is Visual hit
            ? hit.GetSelfAndVisualAncestors()
                .OfType<Control>()
                .Select(control => control.DataContext)
                .OfType<T>()
                .FirstOrDefault()
            : null;
}
