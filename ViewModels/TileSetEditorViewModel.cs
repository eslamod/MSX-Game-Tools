using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Edición de un juego de tiles: el patrón actual en el lienzo y los 256 en la rejilla.
/// </summary>
public partial class TileSetEditorViewModel : PanelBaseViewModel, IPaletteDocument
{
    private readonly TileSet _tileSet;

    /// <summary>La paleta del juego, a cuyos cambios de color estamos suscritos.</summary>
    private ColorPalette _palette;

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

    /// <param name="palette">
    /// Con qué colores se dibuja y se guarda. Es del juego: la trae su fichero, y si es
    /// nuevo, la que estuviera en la barra al crearlo.
    /// </param>
    public TileSetEditorViewModel(TileSet tileSet, ColorPalette palette, EditorPreferences? preferences = null)
    {
        Preferences = preferences ?? new EditorPreferences();
        _tileSet = tileSet;
        _palette = palette;
        _borderColorIndex = palette.DefaultBackgroundIndex;

        _currentTile = tileSet.ListOfTiles[0];

        foreach (Tile tile in tileSet.ListOfTiles)
        {
            if (tile.ImageMini is not null)
                Thumbnails.Add(tile.ImageMini);
        }

        _selectedThumbnail = _currentTile.ImageMini;

        for (int row = 0; row < Tile.Rows; row++)
        {
            RowColors.Add(new TileRowColorViewModel(
                row, _currentTile.ArrayTileRows[row], () => ColorPalette, OnRowColorPicked));
        }

        PixelSurface = new TilePixelSurface(this);

        _palette.ColorsChanged += OnPaletteColorsChanged;

        RenderAll();
    }

    /// <summary>La vista se resuscribe para repintar el lienzo al cambiar de tile.</summary>
    public event Action? RefreshRequested;

    /// <summary>
    /// Ha cambiado la paleta del juego, o alguno de sus colores.
    /// </summary>
    /// <remarks>
    /// Lo escuchan los mapas que se dibujan con este juego: sus tiles acaban de cambiar de
    /// color y su fondo también, que es un índice de esta paleta.
    /// </remarks>
    public event Action? PaletteChanged;

    public override bool IsDocument => true;

    public override string DocumentName
    {
        get => _tileSet.Name;
        set => _tileSet.Name = value;
    }

    public override string HeaderTag => "TS";

    public override string DocumentKind => "juego de tiles";

    /// <summary>
    /// El juego con su paleta y sus bloques, que es lo que va al fichero.
    /// </summary>
    /// <remarks>
    /// La paleta que se escribe es la del juego, no la que esté mirando la ventana: con
    /// dos juegos abiertos con paletas distintas, guardar uno escribía los colores del
    /// otro dentro de su fichero.
    /// </remarks>
    public override string ToFileText() =>
        TileSetSerializer.Serialize(_tileSet, ColorPalette, BorderColorIndex);

    public TileSet TileSet => _tileSet;

    /// <summary>Zoom y demás ajustes que sobreviven al cambio de pestaña.</summary>
    public EditorPreferences Preferences { get; }

    /// <inheritdoc/>
    public ColorPalette ColorPalette
    {
        get => _palette;
        set
        {
            if (ReferenceEquals(_palette, value))
                return;

            _palette.ColorsChanged -= OnPaletteColorsChanged;
            _palette = value;
            _palette.ColorsChanged += OnPaletteColorsChanged;

            RefreshPalette();
        }
    }

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
        Touch();

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
        Touch();

        if (CurrentTile.ImageMini is not null)
            TileRenderer.RenderRow(CurrentTile, rowIndex, ColorPalette, BorderColor.Color, CurrentTile.ImageMini);

        RefreshRequested?.Invoke();
    }

    private void OnPaletteColorsChanged(ColorPalette palette) => RefreshPalette();

    /// <summary>Cambiar de paleta o retocar un color repinta los 256.</summary>
    private void RefreshPalette()
    {
        // Y deja el juego sin guardar: la paleta va dentro de su fichero.
        Touch();

        OnPropertyChanged(nameof(ColorPalette));
        OnPropertyChanged(nameof(BorderChoices));
        OnPropertyChanged(nameof(BorderColor));

        foreach (TileRowColorViewModel row in RowColors)
            row.Refresh();

        RenderAll();
        RefreshRequested?.Invoke();
        PaletteChanged?.Invoke();
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

            editor.Touch();

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
