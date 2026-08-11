namespace MSX_GameTools.Entities;

/// <summary>
/// Apunta a una celda de una imagen de referencia. Es lo que guardan un grupo y un
/// patrón, y lo que va al fichero del banco.
/// </summary>
/// <remarks>
/// La ruta y el número de celda, y no la posición en la lista de fondos, porque esa
/// lista cambia cada vez que se carga o se borra una imagen. Si el fichero ya no está,
/// esto sigue siendo válido y simplemente no resuelve: se pierde el fondo, no el banco.
/// </remarks>
public readonly record struct BackgroundRef(string Path, int Cell)
{
    /// <summary>Sin fondo.</summary>
    public static readonly BackgroundRef None = new(string.Empty, -1);

    public bool HasValue => !string.IsNullOrEmpty(Path) && Cell >= 0;
}
