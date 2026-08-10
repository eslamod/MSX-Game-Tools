using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>
/// Los dos colores de una línea de un tile.
/// </summary>
/// <remarks>
/// Aquí van dos y no uno como en los sprites porque en GRAPHIC 2 y 3 cada línea trae su
/// color de frente y el de fondo, y un pixel es uno de los dos según su bit.
/// </remarks>
public partial class TileRowColorViewModel : ObservableObject
{
    private readonly PaletteLibrary _palettes;
    private readonly Action<int> _onPicked;

    private TileRow _row;

    public TileRowColorViewModel(int rowIndex, TileRow row, PaletteLibrary palettes, Action<int> onPicked)
    {
        RowIndex = rowIndex;
        _row = row;
        _palettes = palettes;
        _onPicked = onPicked;
    }

    /// <summary>Línea del tile, 0-7.</summary>
    public int RowIndex { get; }

    /// <summary>Los 16 colores que ofrecen los desplegables, los de la paleta activa.</summary>
    public IReadOnlyList<PaletteColor> Palette => _palettes.ActivePalette.Colors;

    public PaletteColor Foreground => _palettes.ActivePalette[_row.ForeColor];

    public PaletteColor Background => _palettes.ActivePalette[_row.BackColor];

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
