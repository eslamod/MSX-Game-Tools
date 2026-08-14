namespace MSX_GameTools.Entities;

/// <summary>
/// Un rectángulo de tiles copiado de la rejilla del juego.
/// </summary>
/// <remarks>
/// Lleva los dibujos, no los números: al estamparlo en otro sitio, los tiles de destino
/// pasan a dibujar lo mismo que los de origen, y siguen siendo los números que eran. Es
/// justo al revés que el trozo del mapa, que lleva números de tile y no dibujos.
/// </remarks>
public sealed class TileSetPatch
{
    private readonly Tile[] _tiles;

    public TileSetPatch(int width, int height, IEnumerable<Tile> tiles)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _tiles = [.. tiles];
    }

    /// <summary>Tiles de ancho, contando por la rejilla de 32 columnas.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>Los dibujos, por filas de izquierda a derecha.</summary>
    public IReadOnlyList<Tile> Tiles => _tiles;

    public Tile this[int column, int row] => _tiles[(row * Width) + column];
}
