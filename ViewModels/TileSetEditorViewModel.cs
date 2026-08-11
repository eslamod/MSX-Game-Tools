using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>
/// Edición de un juego de tiles: el patrón actual en el lienzo y los 256 en la rejilla.
/// </summary>
public partial class TileSetEditorViewModel : PanelBaseViewModel
{
    private readonly TileSet _tileSet;
    private readonly PaletteLibrary _palettes;

    private ColorPalette _watchedPalette;

    [ObservableProperty]
    private Tile _currentTile;

    /// <summary>
    /// Miniatura seleccionada en la rejilla, enlazada en los dos sentidos.
    /// </summary>
    /// <remarks>
    /// Por identidad y no por índice, igual que en los sprites: un índice de vuelta
    /// coincidiría con el que el enlace ya cree tener y la selección no se movería.
    /// </remarks>
    [ObservableProperty]
    private ImageMini? _selectedThumbnail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextTileCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousTileCommand))]
    private int _currentTilePosition = 1;

    /// <summary>
    /// Color del borde, que es lo que se ve donde un tile use el código 0.
    /// </summary>
    /// <remarks>
    /// En GRAPHIC 2 el código de color 0 es transparente y deja pasar el color del borde
    /// (R#7). Sin esto el editor pintaba el 0 como un color más de la paleta y enseñaba
    /// algo que la máquina no iba a mostrar.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderColor))]
    private int _borderColorIndex;

    public TileSetEditorViewModel(TileSet tileSet, PaletteLibrary palettes, EditorPreferences? preferences = null)
    {
        Preferences = preferences ?? new EditorPreferences();
        _tileSet = tileSet;
        _palettes = palettes;
        _watchedPalette = palettes.ActivePalette;
        _borderColorIndex = _watchedPalette.DefaultBackgroundIndex;

        _currentTile = tileSet.ListOfTiles[0];

        foreach (Tile tile in tileSet.ListOfTiles)
        {
            if (tile.ImageMini is not null)
                Thumbnails.Add(tile.ImageMini);
        }

        _selectedThumbnail = _currentTile.ImageMini;

        for (int row = 0; row < Tile.Rows; row++)
            RowColors.Add(new TileRowColorViewModel(row, _currentTile.ArrayTileRows[row], palettes, OnRowColorPicked));

        PixelSurface = new TilePixelSurface(this);

        _palettes.PropertyChanged += OnLibraryPropertyChanged;
        _watchedPalette.ColorsChanged += OnActivePaletteColorsChanged;

        RenderAll();
    }

    /// <summary>La vista se resuscribe para repintar el lienzo al cambiar de tile.</summary>
    public event Action? RefreshRequested;

    public TileSet TileSet => _tileSet;

    /// <summary>Zoom y demás ajustes que sobreviven al cambio de pestaña.</summary>
    public EditorPreferences Preferences { get; }

    public ColorPalette ColorPalette => _palettes.ActivePalette;

    /// <summary>Los colores que puede tomar el borde. El 0 no, que es el transparente.</summary>
    public IReadOnlyList<PaletteColor> BorderChoices => ColorPalette.BackgroundChoices;

    public PaletteColor BorderColor => ColorPalette[BorderColorIndex];

    /// <summary>El tile actual visto por el lienzo de pintado.</summary>
    public IPixelSurface PixelSurface { get; }

    /// <summary>Las 256 miniaturas, que la vista reparte en una rejilla de 32 por 8.</summary>
    public ObservableCollection<ImageMini> Thumbnails { get; } = [];

    /// <summary>Una casilla por línea del tile, con sus dos colores.</summary>
    public ObservableCollection<TileRowColorViewModel> RowColors { get; } = [];

    public int TileCount => TileSet.TileCount;

    /// <summary>Lo que se lee al lado de las flechas: «12 / 256».</summary>
    public string TileLabel => $"{CurrentTilePosition} / {TileCount}";

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextTile() => GoTo(CurrentTilePosition);

    private bool CanGoNext() => CurrentTilePosition < TileCount;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousTile() => GoTo(CurrentTilePosition - 2);

    private bool CanGoPrevious() => CurrentTilePosition > 1;

    /// <summary>Lleva el lienzo al tile de esa posición, 0-255.</summary>
    public void GoTo(int index)
    {
        if ((uint)index >= (uint)TileCount)
            return;

        CurrentTile = _tileSet.ListOfTiles[index];
        CurrentTilePosition = index + 1;
        SelectedThumbnail = CurrentTile.ImageMini;

        for (int row = 0; row < RowColors.Count; row++)
            RowColors[row].Attach(CurrentTile.ArrayTileRows[row]);

        RefreshRequested?.Invoke();
    }

    /// <summary>Repinta la miniatura del tile actual entera.</summary>
    public void RenderCurrent()
    {
        if (CurrentTile.ImageMini is not null)
            TileRenderer.Render(CurrentTile, ColorPalette, BorderColor.Color, CurrentTile.ImageMini);
    }

    partial void OnSelectedThumbnailChanged(ImageMini? value)
    {
        if (value is null || ReferenceEquals(value, CurrentTile.ImageMini))
            return;

        int index = Thumbnails.IndexOf(value);
        if (index >= 0)
            GoTo(index);
    }

    partial void OnCurrentTilePositionChanged(int value) => OnPropertyChanged(nameof(TileLabel));

    /// <summary>El borde se ve en todos los tiles que usen el 0, no sólo en el actual.</summary>
    partial void OnBorderColorIndexChanged(int value)
    {
        RenderAll();
        RefreshRequested?.Invoke();
    }

    [RelayCommand]
    private void PickBorderColor(PaletteColor? color)
    {
        if (color is not null)
            BorderColorIndex = color.Index;
    }

    private void OnRowColorPicked(int rowIndex)
    {
        if (CurrentTile.ImageMini is not null)
            TileRenderer.RenderRow(CurrentTile, rowIndex, ColorPalette, BorderColor.Color, CurrentTile.ImageMini);

        RefreshRequested?.Invoke();
    }

    private void OnLibraryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PaletteLibrary.ActivePalette))
            return;

        _watchedPalette.ColorsChanged -= OnActivePaletteColorsChanged;
        _watchedPalette = _palettes.ActivePalette;
        _watchedPalette.ColorsChanged += OnActivePaletteColorsChanged;

        OnActivePaletteColorsChanged(_watchedPalette);
    }

    /// <summary>Cambiar de paleta o retocar un color repinta los 256.</summary>
    private void OnActivePaletteColorsChanged(ColorPalette? palette = null)
    {
        OnPropertyChanged(nameof(ColorPalette));
        OnPropertyChanged(nameof(BorderChoices));
        OnPropertyChanged(nameof(BorderColor));

        foreach (TileRowColorViewModel row in RowColors)
            row.Refresh();

        RenderAll();
        RefreshRequested?.Invoke();
    }

    private void RenderAll()
    {
        foreach (Tile tile in _tileSet.ListOfTiles)
        {
            if (tile.ImageMini is not null)
                TileRenderer.Render(tile, ColorPalette, BorderColor.Color, tile.ImageMini);
        }
    }

    /// <summary>
    /// El tile que se está editando, visto por el lienzo.
    /// </summary>
    /// <remarks>
    /// Lee siempre el tile actual del editor, así que navegar por el juego no obliga a
    /// rehacer nada. Aquí no hay color transparente: un pixel apagado se pinta con el
    /// color de fondo de su línea, que es un color de la paleta como cualquier otro.
    /// </remarks>
    private sealed class TilePixelSurface(TileSetEditorViewModel editor) : IPixelSurface
    {
        public int Size => Tile.Rows;

        public bool IsSet(int x, int y) => editor.CurrentTile.ArrayTileRows[y].ArrayPattern[x];

        public void Set(int x, int y, bool on)
        {
            TileRow row = editor.CurrentTile.ArrayTileRows[y];

            row.ArrayPattern[x] = on;

            editor.CurrentTile.ImageMini?.SetPixel(
                x, y, editor.ColorPalette.Resolve(on ? row.ForeColor : row.BackColor, editor.BorderColor.Color));
        }

        public IBrush BrushAt(int x, int y) =>
            TileRenderer.BrushAt(editor.CurrentTile, editor.ColorPalette, editor.BorderColor.Brush, x, y);

        // Un tile no lo compone nadie: nada que recalcular al soltar.
        public void EndStroke()
        {
        }
    }
}
