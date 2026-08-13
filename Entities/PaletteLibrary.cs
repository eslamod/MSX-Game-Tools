using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>
/// Las paletas del proyecto y cuál se está mirando.
/// </summary>
/// <remarks>
/// Es un catálogo, no un ajuste: con quién dibuja cada documento lo decide el documento,
/// porque su paleta va dentro de su fichero. Aquí sólo están todas juntas para poder
/// elegirlas, compartirlas entre documentos y editarlas en un sitio.
/// </remarks>
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
    /// La paleta que enseña la barra, que es la del documento que está delante.
    /// </summary>
    /// <remarks>
    /// Nunca es <c>null</c>: un ComboBox enlazado a ella escribe null en cuanto el
    /// elemento seleccionado desaparece de la colección, y la biblioteca no puede
    /// quedarse sin paleta activa.
    /// </remarks>
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
    /// Añade una copia editable de la seleccionada y la deja seleccionada. Partir de la
    /// que se está mirando es más útil que partir de una paleta en negro.
    /// </summary>
    public ColorPalette Add(string? name = null)
    {
        ColorPalette created = ActivePalette.Clone(name ?? NextAvailableName());

        Palettes.Add(created);
        ActivePalette = created;

        return created;
    }

    /// <summary>
    /// Mete en la biblioteca una paleta venida de un fichero de paleta y la deja
    /// seleccionada. Si el nombre ya está cogido se numera, para que el desplegable no
    /// muestre dos iguales.
    /// </summary>
    public ColorPalette Import(ColorPalette palette)
    {
        ActivePalette = AddWithFreeName(palette);

        return ActivePalette;
    }

    /// <summary>
    /// Recoge la paleta que traía un documento y devuelve con cuál se queda.
    /// </summary>
    /// <remarks>
    /// Si en la biblioteca ya hay una idéntica se reutiliza, para no llenarla de copias al
    /// abrir varios documentos guardados con la misma paleta. No toca la selección: quién
    /// dibuja con qué lo decide el documento, y la barra lo sigue.
    /// </remarks>
    public ColorPalette Adopt(ColorPalette palette) =>
        Palettes.FirstOrDefault(candidate => HasSameContent(candidate, palette)) ?? AddWithFreeName(palette);

    private ColorPalette AddWithFreeName(ColorPalette palette)
    {
        palette.Name = UniqueName(palette.Name);

        Palettes.Add(palette);

        return palette;
    }

    private static bool HasSameContent(ColorPalette one, ColorPalette other)
    {
        if (one.Name != other.Name)
            return false;

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            if (one[index].Red != other[index].Red
                || one[index].Green != other[index].Green
                || one[index].Blue != other[index].Blue)
            {
                return false;
            }
        }

        return true;
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
