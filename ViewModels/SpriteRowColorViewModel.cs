using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Una casilla de la columna de colores: el color de frente de una línea del sprite.
/// Sólo tiene sentido en MSX2; en MSX1 todas las líneas comparten color.
/// </summary>
public partial class SpriteRowColorViewModel : ObservableObject
{
    private readonly Func<ColorPalette> _palette;
    private readonly Action<int, PaletteColor> _onPicked;

    private SpriteRow _row;

    /// <inheritdoc cref="TileRowColorViewModel(int, TileRow, Func{ColorPalette}, Action{int})" path="/param[@name='palette']"/>
    public SpriteRowColorViewModel(
        int rowIndex,
        SpriteRow row,
        Func<ColorPalette> palette,
        Action<int, PaletteColor> onPicked)
    {
        RowIndex = rowIndex;
        _row = row;
        _palette = palette;
        _onPicked = onPicked;
    }

    /// <summary>Línea del sprite, 0-15.</summary>
    public int RowIndex { get; }

    /// <summary>Los 16 colores que ofrece el desplegable, los del banco.</summary>
    public IReadOnlyList<PaletteColor> Palette => _palette().Colors;

    public PaletteColor Color => _palette()[_row.Color];

    /// <summary>
    /// Repunta la casilla a la línea correspondiente del sprite que se esté editando.
    /// Las casillas se crean una vez y se reutilizan al cambiar de sprite.
    /// </summary>
    public void Attach(SpriteRow row)
    {
        _row = row;
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(Palette));
    }

    [RelayCommand]
    private void Pick(PaletteColor? color)
    {
        if (color is not null)
            _onPicked(RowIndex, color);
    }
}
