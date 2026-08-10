using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Las imágenes de referencia cargadas y las celdas que ofrecen como fondo.
/// </summary>
/// <remarks>
/// Hay dos colecciones porque son dos cosas distintas: las imágenes son lo que se carga
/// y se borra, y las celdas son lo que se elige. Una hoja de sprites de 256x256 a 16x16
/// son 256 fondos de una sola imagen, y borrarlos de uno en uno no tendría sentido.
/// </remarks>
public sealed class ReferenceImageLibrary : ObservableObject
{
    private int _nextNumber = 1;
    private ReferenceImage? _selectedImage;

    public ObservableCollection<ReferenceImage> Images { get; } = [];

    /// <summary>Todas las celdas de todas las imágenes, que es lo que ve el usuario.</summary>
    public ObservableCollection<ReferenceTile> Tiles { get; } = [];

    public bool HasTiles => Tiles.Count > 0;

    /// <summary>La imagen sobre la que actúa el botón de eliminar.</summary>
    public ReferenceImage? SelectedImage
    {
        get => _selectedImage;
        set
        {
            if (ReferenceEquals(_selectedImage, value))
                return;

            _selectedImage = value;
            OnPropertyChanged();
        }
    }

    public ReferenceImage Add(ReferenceImage image)
    {
        Images.Add(image);

        // La numeración no se reaprovecha al borrar: dos fondos con el mismo nombre en
        // sesiones distintas confundirían más de lo que ahorra.
        foreach (ReferenceTile tile in image.Tiles)
        {
            tile.Number = _nextNumber++;
            Tiles.Add(tile);
        }

        SelectedImage = image;
        OnPropertyChanged(nameof(HasTiles));

        return image;
    }

    /// <summary>Carga un fichero y lo añade. El troceado lo decide el tamaño de la imagen.</summary>
    public ReferenceImage Load(string path, int cellSize) => Add(ReferenceImage.Load(path, cellSize));

    /// <summary>Quita una imagen y con ella todos sus fondos.</summary>
    public void Remove(ReferenceImage image)
    {
        int position = Images.IndexOf(image);
        if (position < 0)
            return;

        Images.Remove(image);

        foreach (ReferenceTile tile in image.Tiles)
            Tiles.Remove(tile);

        // Hay que decir cuál queda seleccionada. Al desaparecer la suya de la lista el
        // ComboBox enlazado escribe null aquí, y sin esto el botón de eliminar se
        // quedaría apagado con imágenes todavía cargadas.
        SelectedImage = Images.Count > 0 ? Images[Math.Min(position, Images.Count - 1)] : null;

        OnPropertyChanged(nameof(HasTiles));
    }

    /// <summary>
    /// Busca la celda a la que apunta un grupo o un patrón. Devuelve <c>null</c> si esa
    /// imagen ya no está cargada, que es lo que pasa cuando el fichero se ha movido.
    /// </summary>
    public ReferenceTile? Find(BackgroundRef reference) => reference.HasValue
        ? Tiles.FirstOrDefault(tile => tile.Index == reference.Cell
            && string.Equals(tile.Source.Path, reference.Path, StringComparison.OrdinalIgnoreCase))
        : null;

    /// <inheritdoc cref="Find(BackgroundRef)"/>
    public ReferenceTile? Find(string path, int index) => Find(new BackgroundRef(path, index));
}
