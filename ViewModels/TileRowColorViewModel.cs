using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Los dos colores de una línea de un tile.
/// </summary>
/// <remarks>
/// Aquí van dos y no uno como en los sprites porque en GRAPHIC 2 y 3 cada línea trae su
/// color de frente y el de fondo, y un pixel es uno de los dos según su bit.
/// </remarks>
public partial class TileRowColorViewModel : ObservableObject
{
    private readonly Func<ColorPalette> _palette;
    private readonly Action<int> _onPicked;

    private TileRow _row;

    /// <param name="palette">
    /// La paleta del juego, preguntada cada vez: es suya y puede cambiar, y entonces el
    /// editor llama a <see cref="Refresh"/>.
    /// </param>
    public TileRowColorViewModel(int rowIndex, TileRow row, Func<ColorPalette> palette, Action<int> onPicked)
    {
        RowIndex = rowIndex;
        _row = row;
        _palette = palette;
        _onPicked = onPicked;
    }

    /// <summary>Línea del tile, 0-7.</summary>
    public int RowIndex { get; }

    /// <summary>Los 16 colores que ofrecen los desplegables, los del juego.</summary>
    public IReadOnlyList<PaletteColor> Palette => _palette().Colors;

    public PaletteColor Foreground => _palette()[_row.ForeColor];

    public PaletteColor Background => _palette()[_row.BackColor];

    /// <summary>
    /// Repunta la casilla a la línea del tile que se esté editando. Las casillas se
    /// crean una vez y se reutilizan al cambiar de tile.
    /// </summary>
    public void Attach(TileRow row)
    {
        _row = row;
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(Palette));
    }

    [RelayCommand]
    private void PickForeground(PaletteColor? color)
    {
        if (color is null)
            return;

        _row.ForeColor = color.Index;
        Refresh();
        _onPicked(RowIndex);
    }

    /// <summary>
    /// La otra forma de escribir la misma línea, desde el menú del botón derecho.
    /// </summary>
    /// <remarks>
    /// Pasa por el mismo aviso que elegir un color, aunque el dibujo no cambie: el fichero
    /// sí cambia, y hay que repintar la miniatura porque el color 0 es transparente y
    /// mover un color al otro lado puede cambiar qué pixeles dejan ver el borde.
    /// </remarks>
    [RelayCommand]
    private void SwapColors()
    {
        _row.SwapColors();
        Refresh();
        _onPicked(RowIndex);
    }

    [RelayCommand]
    private void PickBackground(PaletteColor? color)
    {
        if (color is null)
            return;

        _row.BackColor = color.Index;
        Refresh();
        _onPicked(RowIndex);
    }
}
