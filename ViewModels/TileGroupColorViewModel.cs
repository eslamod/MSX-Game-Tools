using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// El par de colores de un grupo de ocho tiles, en el editor de screen 1.
/// </summary>
/// <remarks>
/// El hermano de <see cref="TileRowColorViewModel"/> para GRAPHIC 1. Se parecen porque los dos
/// enseñan un frente y un fondo, pero lo que hay detrás no tiene nada que ver: allí es la
/// línea de un tile y aquí son ocho tiles enteros, así que elegir un color repinta ocho
/// miniaturas y no una.
/// </remarks>
public partial class TileGroupColorViewModel : ObservableObject
{
    private readonly Func<ColorPalette> _palette;
    private readonly Action<TileColorGroup> _onPicked;

    /// <param name="palette">
    /// La paleta del juego, preguntada cada vez: es suya y puede cambiar, y entonces el
    /// editor llama a <see cref="Refresh"/>.
    /// </param>
    public TileGroupColorViewModel(
        TileColorGroup group, Func<ColorPalette> palette, Action<TileColorGroup> onPicked)
    {
        Group = group;
        _palette = palette;
        _onPicked = onPicked;

        // El par no sólo cambia por estas dos muestras: estampar un trozo traído de otro juego
        // puede dejarle a un grupo sin estrenar el color de lo que le llega, y deshacer ese
        // estampado lo devuelve. Escuchando al grupo, la muestra se entera de todas esas veces
        // en vez de sólo de cuando se pulsa aquí, que era lo que la dejaba con el color viejo.
        Group.PropertyChanged += (_, _) => Refresh();
    }

    public TileColorGroup Group { get; }

    /// <summary>A qué tiles pinta: «0-7», «8-15»… Es lo que se lee al lado del par.</summary>
    public string Range => Group.Range;

    /// <summary>Los 16 colores que ofrecen los desplegables, los del juego.</summary>
    public IReadOnlyList<PaletteColor> Palette => _palette().Colors;

    public PaletteColor Foreground => _palette()[Group.ForeColor];

    public PaletteColor Background => _palette()[Group.BackColor];

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

        Group.Set(color.Index, Group.BackColor);

        Refresh();
        _onPicked(Group);
    }

    [RelayCommand]
    private void PickBackground(PaletteColor? color)
    {
        if (color is null)
            return;

        Group.Set(Group.ForeColor, color.Index);

        Refresh();
        _onPicked(Group);
    }

    /// <summary>
    /// La otra forma de escribir lo mismo: intercambia el par e invierte los bits de sus
    /// ocho tiles.
    /// </summary>
    /// <remarks>
    /// Igual que en una línea de GRAPHIC 2, un dibujo se puede escribir de dos maneras y en
    /// pantalla no se distinguen. Aquí además hace falta para poder juntar en un mismo grupo
    /// dibujos que vengan escritos al revés unos de otros.
    /// </remarks>
    [RelayCommand]
    private void SwapColors()
    {
        foreach (Tile tile in Group.Tiles)
        {
            foreach (TileRow row in tile.ArrayTileRows)
            {
                for (int column = 0; column < TileRow.Columns; column++)
                    row.ArrayPattern[column] = !row.ArrayPattern[column];
            }
        }

        Group.Set(Group.BackColor, Group.ForeColor);

        Refresh();
        _onPicked(Group);
    }
}
