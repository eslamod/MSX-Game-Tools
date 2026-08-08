using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Las paletas del proyecto y cuál está activa. Es un recurso común: los bancos de
/// sprites, y en su día los tilesets y los mapas, dibujan todos con la paleta activa.
/// </summary>
public sealed partial class PaletteLibrary : ObservableObject
{
    [ObservableProperty]
    private ColorPalette _activePalette;

    public PaletteLibrary()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();

        Palettes = [standard];
        _activePalette = standard;
    }

    public ObservableCollection<ColorPalette> Palettes { get; }

    /// <summary>
    /// Añade una copia editable de la paleta activa y la deja seleccionada. Partir de
    /// la activa es más útil que partir de una paleta en negro.
    /// </summary>
    public ColorPalette Add(string? name = null)
    {
        ColorPalette created = ActivePalette.Clone(name ?? NextAvailableName());

        Palettes.Add(created);
        ActivePalette = created;

        return created;
    }

    /// <summary>La estándar no se toca, y la biblioteca nunca se queda vacía.</summary>
    public bool CanRemove(ColorPalette? palette) =>
        palette is { IsReadOnly: false } && Palettes.Contains(palette) && Palettes.Count > 1;

    public bool Remove(ColorPalette palette)
    {
        if (!CanRemove(palette))
            return false;

        int index = Palettes.IndexOf(palette);
        Palettes.Remove(palette);

        if (ReferenceEquals(ActivePalette, palette))
            ActivePalette = Palettes[Math.Min(index, Palettes.Count - 1)];

        return true;
    }

    private string NextAvailableName()
    {
        int number = 1;
        while (Palettes.Any(p => p.Name == $"Palette {number}"))
            number++;

        return $"Palette {number}";
    }
}
