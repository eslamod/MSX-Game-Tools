using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Las paletas del proyecto y cuál está activa. Es un recurso común: los bancos de
/// sprites, y en su día los tilesets y los mapas, dibujan todos con la paleta activa.
/// </summary>
public sealed partial class PaletteLibrary : ObservableObject
{
    private ColorPalette _activePalette;

    public PaletteLibrary()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();

        Palettes = [standard];
        _activePalette = standard;
    }

    /// <summary>
    /// La paleta con la que se dibuja. Nunca es <c>null</c>: un ComboBox enlazado a
    /// ella escribe null en cuanto el elemento seleccionado desaparece de la
    /// colección, y la biblioteca no puede quedarse sin paleta activa.
    /// </summary>
    public ColorPalette ActivePalette
    {
        get => _activePalette;
        set
        {
            if (value is null)
                return;

            SetProperty(ref _activePalette, value);
        }
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

    /// <summary>
    /// Mete en la biblioteca una paleta venida de fichero y la deja seleccionada. Si el
    /// nombre ya está cogido se numera, para que el desplegable no muestre dos iguales.
    /// </summary>
    public ColorPalette Import(ColorPalette palette)
    {
        palette.Name = UniqueName(palette.Name);

        Palettes.Add(palette);
        ActivePalette = palette;

        return palette;
    }

    /// <summary>La estándar no se toca, y la biblioteca nunca se queda vacía.</summary>
    public bool CanRemove(ColorPalette? palette) =>
        palette is { IsReadOnly: false } && Palettes.Contains(palette) && Palettes.Count > 1;

    public bool Remove(ColorPalette palette)
    {
        if (!CanRemove(palette))
            return false;

        int index = Palettes.IndexOf(palette);

        // Mover la selección ANTES de quitarla de la colección: cuando llega el
        // Remove, la paleta que desaparece ya no es la activa, así que un ComboBox
        // enlazado no se queda sin selección ni la escribe de vuelta.
        if (ReferenceEquals(ActivePalette, palette))
            ActivePalette = Palettes[index > 0 ? index - 1 : index + 1];

        Palettes.Remove(palette);

        return true;
    }

    private string NextAvailableName()
    {
        int number = 1;
        while (Palettes.Any(p => p.Name == $"Palette {number}"))
            number++;

        return $"Palette {number}";
    }

    private string UniqueName(string name)
    {
        if (Palettes.All(p => p.Name != name))
            return name;

        int number = 2;
        while (Palettes.Any(p => p.Name == $"{name} ({number})"))
            number++;

        return $"{name} ({number})";
    }
}
