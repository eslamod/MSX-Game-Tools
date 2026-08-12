namespace MSX_GameTools.Entities;

/// <summary>
/// Un mapa de tiles: varias capas del mismo tamaño que se aplastan en una sola tabla de
/// nombres al exportar.
/// </summary>
/// <remarks>
/// <para>
/// Las capas se funden por su orden en la lista: la primera es la de abajo y la última la
/// de arriba, así que gana la celda no vacía más alta. En la máquina no hay capas, hay una
/// tabla de nombres, y por eso el aplastado no es una opción sino la única salida posible.
/// </para>
/// <para>
/// Toda modificación pasa por los métodos de esta clase, que anotan el rastro en la pila
/// de deshacer. Tocar <c>Layers[i].Grid</c> por fuera funciona, pero se queda sin deshacer.
/// </para>
/// </remarks>
public class TileMap
{
    /// <summary>
    /// Lo más grande que puede ser un mapa de lado.
    /// </summary>
    /// <remarks>
    /// Mil veinticuatro y no más porque la cabecera del binario lleva dos bytes por lado:
    /// diez bits bastan para el tamaño y quedan seis libres en cada uno para banderas. Y
    /// un lado de mil tiles ya son treinta pantallas seguidas.
    /// </remarks>
    public const int MaxSide = 1024;

    public TileMap(string name = "", int width = 32, int height = 24)
    {
        Name = name;
        Width = Math.Clamp(width, 1, MaxSide);
        Height = Math.Clamp(height, 1, MaxSide);

        Layers.Add(new MapLayer("Capa 1", Width, Height));
    }

    public string Name { get; set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>De abajo a arriba: la última de la lista es la que tapa a las demás.</summary>
    public IList<MapLayer> Layers { get; } = [];

    /// <summary>
    /// Color que se ve donde no hay tile en ninguna capa.
    /// </summary>
    /// <remarks>
    /// Es el mismo R#7 que el borde del editor de tiles. Arranca en el color más oscuro de
    /// la paleta y no en un índice fijo, que dar por negro el 1 ya nos costó un fallo.
    /// </remarks>
    public int BackgroundColorIndex { get; set; } = 1;

    /// <summary>El juego de tiles con el que se dibuja. Sin él los números no dicen nada.</summary>
    public string TileSetName { get; set; } = string.Empty;

    public UndoStack Undo { get; } = new();

    /// <summary>
    /// El tile que se ve en una celda, mirando de arriba abajo hasta encontrar uno.
    /// </summary>
    /// <param name="onlyVisible">
    /// Para pintar en pantalla se saltan las capas apagadas; para exportar no, porque
    /// apagar una capa es una ayuda de edición y no quiere decir que sobre.
    /// </param>
    public int? TileAt(int column, int row, bool onlyVisible = false)
    {
        for (int index = Layers.Count - 1; index >= 0; index--)
        {
            if (onlyVisible && !Layers[index].IsVisible)
                continue;

            if (Layers[index].Grid[column, row] is int tile)
                return tile;
        }

        return null;
    }

    /// <summary>Las capas fundidas en una sola rejilla, que es lo que va a la máquina.</summary>
    public TileGrid Flatten(bool onlyVisible = false)
    {
        var flat = new TileGrid(Width, Height);

        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
                flat[column, row] = TileAt(column, row, onlyVisible);
        }

        return flat;
    }

    /// <summary>Estampa un trozo en una capa, dejando rastro para deshacer.</summary>
    /// <returns><c>false</c> si la capa no existe o está bloqueada.</returns>
    public bool Stamp(int layer, int column, int row, TilePatch patch) =>
        Edit(layer, column, row, patch.Width, patch.Height, grid => grid.Stamp(column, row, patch));

    /// <summary>Vacía un rectángulo de una capa.</summary>
    /// <inheritdoc cref="Stamp"/>
    public bool Erase(int layer, int column, int row, int width = 1, int height = 1) =>
        Edit(layer, column, row, width, height, grid =>
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    grid[column + x, row + y] = null;
            }
        });

    /// <summary>
    /// Rellena un rectángulo repitiendo un trozo.
    /// </summary>
    /// <remarks>
    /// Repetir y no estirar: el trozo se va embaldosando desde la esquina, que es lo que
    /// hace falta para llenar una zona de hierba con un patrón de 2x2.
    /// </remarks>
    /// <inheritdoc cref="Stamp"/>
    public bool Fill(int layer, int column, int row, int width, int height, TilePatch pattern) =>
        Edit(layer, column, row, width, height, grid =>
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    grid[column + x, row + y] = pattern[x % pattern.Width, y % pattern.Height];
            }
        });

    /// <summary>
    /// Cambia el tamaño del mapa y de todas sus capas.
    /// </summary>
    /// <remarks>
    /// El contenido se ancla arriba a la izquierda: lo que se sale al encoger se pierde, y
    /// por eso este cambio guarda las capas enteras para poder deshacerse.
    /// </remarks>
    public void Resize(int width, int height)
    {
        width = Math.Clamp(width, 1, MaxSide);
        height = Math.Clamp(height, 1, MaxSide);

        if (width == Width && height == Height)
            return;

        var before = Layers.Select(layer => layer.Grid.ToPatch()).ToList();
        var edit = new MapResizeEdit(Width, Height, before, width, height);

        ApplySize(width, height);

        Undo.Push(edit);
    }

    /// <summary>Añade una capa vacía encima de las demás.</summary>
    public MapLayer AddLayer(string? name = null)
    {
        var layer = new MapLayer(name ?? NextLayerName(), Width, Height);

        Layers.Add(layer);

        return layer;
    }

    /// <summary>
    /// Devuelve un rectángulo a como estaba. Lo usa la pila de deshacer.
    /// </summary>
    /// <remarks>
    /// Se salta el bloqueo de la capa a propósito: deshacer no es editar, es volver a
    /// donde estabas, y una capa que se bloquea después no debería atrapar el cambio.
    /// </remarks>
    public void Restore(int layer, int column, int row, TilePatch patch)
    {
        if ((uint)layer < (uint)Layers.Count)
            Layers[layer].Grid.Stamp(column, row, patch);
    }

    /// <inheritdoc cref="Restore"/>
    public void RestoreSize(int width, int height, IReadOnlyList<TilePatch>? contents)
    {
        ApplySize(width, height);

        if (contents is null)
            return;

        for (int index = 0; index < Math.Min(contents.Count, Layers.Count); index++)
            Layers[index].Grid.Stamp(0, 0, contents[index]);
    }

    private void ApplySize(int width, int height)
    {
        Width = width;
        Height = height;

        foreach (MapLayer layer in Layers)
            layer.Grid.Resize(width, height);
    }

    /// <summary>
    /// El punto por el que pasan todos los cambios de una capa: anota lo que había, deja
    /// que la herramienta haga lo suyo, y guarda el rastro.
    /// </summary>
    private bool Edit(int layer, int column, int row, int width, int height, Action<TileGrid> change)
    {
        if ((uint)layer >= (uint)Layers.Count || Layers[layer].IsLocked)
            return false;

        TileGrid grid = Layers[layer].Grid;

        TilePatch before = grid.ToPatch(column, row, width, height);

        change(grid);

        TilePatch after = grid.ToPatch(column, row, width, height);

        Undo.Push(new LayerRectEdit(layer, column, row, before, after));

        return true;
    }

    private string NextLayerName()
    {
        for (int number = 1; ; number++)
        {
            string name = $"Capa {number}";

            if (!Layers.Any(layer => layer.Name == name))
                return name;
        }
    }
}
