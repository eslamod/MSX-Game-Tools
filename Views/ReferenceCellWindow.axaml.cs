using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Views;

/// <summary>
/// Enseña una hoja de sprites con su retícula encima para elegir una celda con el ratón.
/// </summary>
/// <remarks>
/// Un desplegable no vale para esto: una hoja de 256x256 a 16x16 son 256 fondos, y
/// nadie sabe cuál es «Background 137». Aquí se señala el dibujo directamente.
/// </remarks>
public partial class ReferenceCellWindow : Window
{
    private const int DefaultZoom = 2;

    private readonly ReferenceImage? _image;
    private readonly int _currentCell;

    private int _zoom = DefaultZoom;

    public ReferenceCellWindow() => InitializeComponent();

    public ReferenceCellWindow(ReferenceImage image, int currentCell)
        : this()
    {
        _image = image;
        _currentCell = currentCell;

        Title = $"Elegir celda - {System.IO.Path.GetFileName(image.Path)}";

        HeadingText.Text =
            $"{image.Columns}x{image.Rows} celdas de {image.CellSize} pixeles. Pulsa la que quieras de fondo.";

        Build();
    }

    /// <summary>
    /// Lo que devuelve <c>ShowDialog</c>: el índice elegido, <see cref="BackgroundRef.None"/>
    /// como celda negativa para quitar el fondo, o <c>null</c> al cancelar.
    /// </summary>
    private void Build()
    {
        if (_image is null)
            return;

        SheetImage.Source = _image.Source;
        SheetImage.Width = _image.Size.Width * _zoom;
        SheetImage.Height = _image.Size.Height * _zoom;

        SheetPanel.Width = SheetImage.Width;
        SheetPanel.Height = SheetImage.Height;

        CellOverlay.Children.Clear();

        for (int index = 0; index < _image.Tiles.Count; index++)
        {
            ReferenceTile tile = _image.Tiles[index];

            var box = new Border
            {
                Theme = Resources["CellBox"] as ControlTheme,
                Width = tile.Size.Width * _zoom,
                Height = tile.Size.Height * _zoom,
                Tag = index,
            };

            // La que ya está puesta se marca en rojo, igual que la miniatura
            // seleccionada en la tira de sprites.
            if (index == _currentCell)
            {
                box.BorderBrush = Brushes.Red;
                box.BorderThickness = new Thickness(2);
            }

            Canvas.SetLeft(box, tile.Column * _image.CellSize * _zoom);
            Canvas.SetTop(box, tile.Row * _image.CellSize * _zoom);

            box.PointerEntered += OnCellEntered;
            box.PointerPressed += OnCellPressed;

            CellOverlay.Children.Add(box);
        }
    }

    private void OnCellEntered(object? sender, PointerEventArgs e)
    {
        if (_image is not null && sender is Border { Tag: int index })
            HoverText.Text = $"{_image.Tiles[index].Column},{_image.Tiles[index].Row}";
    }

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: int index })
            Close(index);
    }

    private void OnZoom(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int zoom))
        {
            _zoom = zoom;
            Build();
        }
    }

    /// <summary>Un índice negativo es la forma de decir «ninguna».</summary>
    private void OnClearBackground(object? sender, RoutedEventArgs e) => Close(-1);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
