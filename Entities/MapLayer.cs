namespace MSX_GameTools.Entities;

/// <summary>
/// Una capa de un mapa: una rejilla de tiles con su nombre.
/// </summary>
/// <remarks>
/// Las capas son ayuda al diseño y no existen en la máquina, que sólo tiene una tabla de
/// nombres: al exportar se aplastan en una sola. Sirven para mover un árbol sin repintar
/// la hierba, que es para lo que sirven en cualquier editor de mapas.
/// </remarks>
public class MapLayer
{
    public MapLayer(string name, int width, int height)
    {
        Name = name;
        Grid = new TileGrid(width, height);
    }

    public string Name { get; set; }

    public TileGrid Grid { get; }

    /// <summary>Si se ve al componer el mapa. No afecta a lo que se exporta.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Si está protegida contra cambios.
    /// </summary>
    /// <remarks>
    /// El bloqueo vive aquí y no sólo en la interfaz: el mapa se niega a modificar una
    /// capa bloqueada, así que ninguna herramienta puede saltárselo por descuido.
    /// </remarks>
    public bool IsLocked { get; set; }
}
