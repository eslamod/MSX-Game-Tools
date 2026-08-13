using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Una línea de la tabla de atributos del miembro seleccionado: su color y el bit CC.
/// </summary>
/// <remarks>
/// Es el equivalente de <see cref="SpriteRowColorViewModel"/> pero sobre el grupo. Son
/// dos sitios distintos a propósito: el color del patrón es la plantilla de la que se
/// siembran los miembros, y el del miembro es el que se dibuja.
/// </remarks>
public partial class SpriteMemberColorViewModel : ObservableObject
{
    private readonly Func<ColorPalette> _palette;
    private readonly Action<int, PaletteColor> _onPicked;

    private SpriteAttributeRow _row;

    /// <inheritdoc cref="TileRowColorViewModel(int, TileRow, Func{ColorPalette}, Action{int})" path="/param[@name='palette']"/>
    public SpriteMemberColorViewModel(
        int rowIndex,
        SpriteAttributeRow row,
        Func<ColorPalette> palette,
        Action<int, PaletteColor> onPicked)
    {
        RowIndex = rowIndex;
        Hex = rowIndex.ToString("X1");
        _row = row;
        _palette = palette;
        _onPicked = onPicked;
    }

    /// <summary>Línea del sprite, 0-15.</summary>
    public int RowIndex { get; }

    /// <summary>El número de línea en hexadecimal, para que quepa en una casilla.</summary>
    public string Hex { get; }

    /// <summary>La línea de atributos, para enlazar CC en los dos sentidos.</summary>
    public SpriteAttributeRow Row => _row;

    public IReadOnlyList<PaletteColor> Palette => _palette().Colors;

    public PaletteColor Color => _palette()[_row.Color];

    /// <summary>Repunta la casilla a la línea del miembro que se esté editando.</summary>
    public void Attach(SpriteAttributeRow row)
    {
        _row = row;

        OnPropertyChanged(nameof(Row));
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
