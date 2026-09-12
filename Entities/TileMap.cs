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
/// <summary>
/// A tile set the map draws with: which one it is, and what it was called.
/// </summary>
/// <remarks>
/// The identity is what binds; the name is what lets a map file say where its tiles come from
/// without opening the project. It is the pair a map has always kept, and the only new thing is
/// that there can be one of them per screen third.
/// </remarks>
public readonly record struct TileSetRef(Guid Id, string Name)
{
    /// <summary>No tile set yet, which is how a map is born.</summary>
    public static TileSetRef None => new(Guid.Empty, string.Empty);
}

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

    /// <summary>The rows of one screenful, which is the name table of SCREEN 1, 2 and 4.</summary>
    public const int ScreenRows = 24;

    /// <summary>
    /// The most a map can be split: one tile set per screen third.
    /// </summary>
    /// <remarks>
    /// In GRAPHIC 2 the pattern table is three banks of 256, and which third a row falls in is
    /// what decides which of the three draws it. That is why this number is three and not a
    /// setting: it is what the VDP does, and there is no fourth third to have an opinion about.
    /// </remarks>
    public const int MaxTileSets = 3;

    /// <summary>The rows of one third. Rows of tiles, not pixels.</summary>
    public const int RowsPerThird = ScreenRows / MaxTileSets;

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

    /// <summary>
    /// Cambia de índice los colores del mapa, para que siga viéndose igual después de
    /// haber movido los colores de sitio en la paleta.
    /// </summary>
    /// <param name="table">Del índice de antes al de ahora, tal cual lo da <see cref="PaletteSwaps.Table"/>.</param>
    /// <remarks>
    /// Un mapa son números de tile: lo único suyo que es un color es el fondo. Los tiles
    /// los reajusta su juego.
    /// </remarks>
    public void RemapColors(IReadOnlyList<int> table) =>
        BackgroundColorIndex = table[BackgroundColorIndex];

    private readonly List<TileSetRef> _tileSets = [TileSetRef.None];

    /// <summary>
    /// The tile sets the map draws with, one per screen third.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nearly always one, and then the three thirds draw with that same one: it is what every
    /// map was until now, and what every map taller than the screen still is.
    /// </para>
    /// <para>
    /// The first one is <see cref="TileSetId"/>, the one the rest of the program already knows
    /// about. Everything that ties a map to a tile set -opening it, finding the maps of a set,
    /// renaming- goes through that first one, so the other two could be added without moving
    /// any of it.
    /// </para>
    /// </remarks>
    public IReadOnlyList<TileSetRef> TileSets => _tileSets;

    /// <summary>
    /// El juego de tiles con el que se dibuja. Sin él los números no dicen nada.
    /// </summary>
    /// <remarks>
    /// Por identidad y no por nombre: así renombrar el juego no deja al mapa sin saber de
    /// dónde son sus tiles. En los ficheros de antes de que esto existiera viene vacío, y
    /// entonces se recurre al nombre.
    /// </remarks>
    public Guid TileSetId
    {
        get => _tileSets[0].Id;
        set => _tileSets[0] = _tileSets[0] with { Id = value };
    }

    /// <summary>
    /// Cómo se llamaba el juego la última vez que se guardó.
    /// </summary>
    /// <remarks>
    /// No es quien manda: sirve para poder leer de qué va un fichero de mapa abriéndolo, y
    /// para encontrar el juego en los mapas antiguos, que no traen identidad.
    /// </remarks>
    public string TileSetName
    {
        get => _tileSets[0].Name;
        set => _tileSets[0] = _tileSets[0] with { Name = value };
    }

    /// <summary>
    /// Puts the tile sets in, one per third and in the order of the thirds.
    /// </summary>
    /// <remarks>
    /// Anything past the third is dropped, and an empty list leaves the empty one: a map always
    /// has a first tile set, even if it is nobody, because that is the one the rest of the
    /// program asks for.
    /// </remarks>
    public void UseTileSets(IReadOnlyList<TileSetRef> tileSets)
    {
        _tileSets.Clear();
        _tileSets.AddRange(BandsOf(tileSets));
    }

    /// <summary>
    /// The bands a map keeps out of those.
    /// </summary>
    /// <remarks>
    /// At most three, because there is no fourth third; at least one, because a map always has
    /// a first tile set even if it is nobody; and one when they all match, because three copies
    /// of the same one say the same thing and would end up written into the file as a list of
    /// three. It is also what lets anyone tell that nothing has changed.
    /// </remarks>
    public static IReadOnlyList<TileSetRef> BandsOf(IReadOnlyList<TileSetRef> tileSets)
    {
        TileSetRef[] bands = [.. tileSets.Take(MaxTileSets)];

        if (bands.Length == 0)
            return [TileSetRef.None];

        return bands.All(band => band == bands[0]) ? [bands[0]] : bands;
    }

    /// <summary>
    /// How many screen thirds the map covers, from one to three.
    /// </summary>
    /// <remarks>
    /// A map taller than the screen counts as one, and that is not a limitation of the editor:
    /// the thirds belong to the screen and not to the map, so as soon as it scrolls up and down
    /// a given row of the map lands in a different third depending on where the screen is. With
    /// no fixed band there is no band to tie a bank to.
    /// </remarks>
    public int Thirds => ThirdsOf(Height);

    /// <inheritdoc cref="Thirds"/>
    /// <remarks>
    /// As a plain number and not as a map, because the form that creates one has to know how
    /// many tile sets to offer before there is any map to ask.
    /// </remarks>
    public static int ThirdsOf(int height) => height > ScreenRows
        ? 1
        : Math.Min(((height - 1) / RowsPerThird) + 1, MaxTileSets);

    /// <summary>
    /// How many tile sets this map can take when it is drawn with that one.
    /// </summary>
    /// <remarks>
    /// GRAPHIC 1 has a single pattern table for the whole screen, so there are no banks to tell
    /// apart. And in super tile mode a cell is a block of its own set, so three sets would be
    /// three numberings on top of each other. Both of them stay on one.
    /// </remarks>
    public int TileSetSlots(TileSet tileSet) => SlotsOf(Height, tileSet);

    /// <inheritdoc cref="TileSetSlots"/>
    /// <inheritdoc cref="ThirdsOf" path="/remarks"/>
    public static int SlotsOf(int height, TileSet tileSet) =>
        tileSet.IsGraphic1 || tileSet.HasSuperTiles ? 1 : ThirdsOf(height);

    /// <summary>Whether the map is drawn with that tile set in any of its bands.</summary>
    public bool Uses(Guid tileSet) => _tileSets.Any(band => band.Id == tileSet);

    /// <summary>
    /// Writes down the new name of a tile set, in every band that uses it.
    /// </summary>
    /// <remarks>
    /// The name is not what binds -the identity is- but it is what the map file says out loud,
    /// and what the exported asm tells whoever has to load each table into its own third. A
    /// stale name there is a lie that nothing else corrects.
    /// </remarks>
    /// <returns>Whether anything changed.</returns>
    public bool Rename(Guid tileSet, string name)
    {
        bool changed = false;

        for (int band = 0; band < _tileSets.Count; band++)
        {
            if (_tileSets[band].Id != tileSet || _tileSets[band].Name == name)
                continue;

            _tileSets[band] = _tileSets[band] with { Name = name };
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// The tile set a row of the map is drawn with.
    /// </summary>
    /// <remarks>
    /// A third that was never chosen falls back to the first, and so does every row of a map
    /// that does not split. Choosing fewer than the map covers is not an error: it is a map
    /// that repeats the same bank, which is what the machine does anyway.
    /// </remarks>
    public TileSetRef TileSetFor(int row)
    {
        if (Thirds == 1 || _tileSets.Count == 1)
            return _tileSets[0];

        int third = Math.Clamp(row / RowsPerThird, 0, Thirds - 1);

        return third < _tileSets.Count ? _tileSets[third] : _tileSets[0];
    }

    /// <summary>
    /// Con qué tile se rellenan las celdas vacías al exportar a binario.
    /// </summary>
    /// <remarks>
    /// En un byte no cabe el hueco: los 256 valores son tiles de verdad y la tabla de
    /// nombres del VDP siempre dibuja algo en cada celda. Así que al salir a la máquina
    /// hay que elegir qué se pone donde el editor no tiene nada, y más vale decirlo aquí
    /// que escribir ceros en silencio. En csv sí cabe el hueco y se escribe -1.
    /// </remarks>
    public int EmptyTile { get; set; }

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
            // Con los huecos del trozo respetados, igual que al estampar: es la misma
            // brocha y no tiene por que borrar en una herramienta y en la otra no.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pattern[x % pattern.Width, y % pattern.Height] is int tile)
                        grid[column + x, row + y] = tile;
                }
            }
        });

    /// <summary>
    /// Cambia unos tiles por otros dentro de un rectángulo.
    /// </summary>
    /// <param name="fromFirst">Primer tile de los que se sustituyen.</param>
    /// <param name="fromLast">Último, que puede ser el mismo para cambiar sólo uno.</param>
    /// <param name="toFirst">
    /// Por cuál empieza la serie nueva. El desplazamiento es el mismo para todo el rango,
    /// así que del 10, 11 y 12 al 20 salen 20, 21 y 22 de una pasada.
    /// </param>
    /// <param name="layers">En qué capas. Vacío es ninguna, y las bloqueadas se saltan.</param>
    /// <returns>Cuántas celdas han cambiado.</returns>
    public int Replace(
        int fromFirst, int fromLast, int toFirst,
        int left, int top, int width, int height,
        IEnumerable<int> layers)
    {
        if (fromLast < fromFirst)
            (fromFirst, fromLast) = (fromLast, fromFirst);

        int shift = toFirst - fromFirst;
        var table = new Dictionary<int, int>();

        for (int tile = fromFirst; tile <= fromLast; tile++)
            table[tile] = tile + shift;

        return Replace(table, left, top, width, height, layers);
    }

    /// <summary>
    /// Cambia unos tiles por otros dentro de un rectángulo, cada uno por el suyo.
    /// </summary>
    /// <param name="table">
    /// De qué tile a qué tile. Los que no estén se quedan como están.
    /// </param>
    /// <remarks>
    /// <para>
    /// Las sustituciones se aplican <b>todas a la vez</b> y no en cadena: cada celda se
    /// mira una sola vez y se escribe una sola vez. Con una tabla que lleve 35→77 y 77→88,
    /// un 35 acaba en 77 y ahí se queda, y sólo los 77 que ya hubiera en el mapa pasan a
    /// 88. Si se aplicaran una detrás de otra, el resultado dependería del orden de la
    /// tabla, que es de las cosas que no se entienden viendo el mapa.
    /// </para>
    /// <para>
    /// Es también el motor del rango, que no es más que una tabla con un desplazamiento
    /// constante. Así los dos modos tratan igual los destinos que se salen del juego, la
    /// cuenta de celdas y el paso de deshacer.
    /// </para>
    /// </remarks>
    /// <inheritdoc cref="Replace(int, int, int, int, int, int, int, IEnumerable{int})"/>
    public int Replace(
        IReadOnlyDictionary<int, int> table,
        int left, int top, int width, int height,
        IEnumerable<int> layers)
    {
        int changed = 0;
        var edits = new List<IMapEdit>();

        foreach (int layer in layers)
        {
            if ((uint)layer >= (uint)Layers.Count || Layers[layer].IsLocked)
                continue;

            TileGrid grid = Layers[layer].Grid;
            TilePatch before = grid.ToPatch(left, top, width, height);
            int here = 0;

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    if (grid[left + column, top + row] is not int tile)
                        continue;

                    if (!table.TryGetValue(tile, out int replacement))
                        continue;

                    // Lo que se saldría del juego de tiles se deja como está: mejor no
                    // tocarlo que dejar un número que no existe.
                    if ((uint)replacement >= (uint)TileSet.TileCount)
                        continue;

                    grid[left + column, top + row] = replacement;
                    here++;
                }
            }

            if (here == 0)
                continue;

            changed += here;
            edits.Add(new LayerRectEdit(layer, left, top, before, grid.ToPatch(left, top, width, height)));
        }

        // Un solo paso para toda la operación: se pidió una vez y se deshace una vez.
        if (edits.Count > 0)
            Undo.Push(new MapEditGroup(edits));

        return changed;
    }

    /// <summary>
    /// Cambia el tamaño del mapa y de todas sus capas.
    /// </summary>
    /// <param name="offsetColumn">
    /// Dónde queda lo que había. Con cero se ancla a la izquierda; con un número positivo
    /// el contenido se desplaza, que es lo que hace falta para alargar un nivel por el
    /// principio sin repintarlo.
    /// </param>
    /// <param name="offsetRow"><inheritdoc cref="offsetColumn"/></param>
    /// <remarks>
    /// Lo que se sale al encoger se pierde, y por eso este cambio guarda las capas enteras
    /// para poder deshacerse.
    /// </remarks>
    public void Resize(int width, int height, int offsetColumn = 0, int offsetRow = 0)
    {
        width = Math.Clamp(width, 1, MaxSide);
        height = Math.Clamp(height, 1, MaxSide);

        if (width == Width && height == Height && offsetColumn == 0 && offsetRow == 0)
            return;

        // El tamaño de antes se guarda aquí: al construir el registro más abajo, Width y
        // Height ya son los nuevos.
        (int oldWidth, int oldHeight) = (Width, Height);

        var before = Layers.Select(layer => layer.Grid.ToPatch()).ToList();

        ApplySize(width, height);

        // Se vacía y se vuelve a poner en su sitio: la rejilla al redimensionar conserva
        // las coordenadas, y aquí puede hacer falta correrlo todo.
        for (int index = 0; index < Layers.Count; index++)
        {
            Layers[index].Grid.Clear();
            Layers[index].Grid.Overwrite(offsetColumn, offsetRow, before[index]);
        }

        var after = Layers.Select(layer => layer.Grid.ToPatch()).ToList();

        Undo.Push(new MapResizeEdit(oldWidth, oldHeight, before, width, height, after));
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
            Layers[layer].Grid.Overwrite(column, row, patch);
    }

    /// <inheritdoc cref="Restore"/>
    public void RestoreSize(int width, int height, IReadOnlyList<TilePatch>? contents)
    {
        ApplySize(width, height);

        if (contents is null)
            return;

        for (int index = 0; index < Math.Min(contents.Count, Layers.Count); index++)
        {
            Layers[index].Grid.Clear();
            Layers[index].Grid.Overwrite(0, 0, contents[index]);
        }
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
