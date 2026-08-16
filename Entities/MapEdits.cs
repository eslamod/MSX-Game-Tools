namespace MSX_GameTools.Entities;

/// <summary>Un cambio del mapa que se puede deshacer.</summary>
/// <remarks>
/// Todas las modificaciones del mapa pasan por aquí. Es lo que permite que deshacer sea
/// una cosa del modelo y no un añadido de cada herramienta: rellenar, pegar, estampar un
/// bloque y redimensionar se deshacen igual porque todas dejan el mismo tipo de rastro.
/// </remarks>
public interface IMapEdit
{
    void Undo(TileMap map);

    void Redo(TileMap map);
}

/// <summary>
/// Un rectángulo de una capa que ha cambiado, con lo que había y lo que quedó.
/// </summary>
/// <remarks>
/// Se guarda el rectángulo afectado y no el mapa entero: pintar una celda no puede costar
/// una copia de cien mil. Guardar también el "después" es lo que permite rehacer, y a
/// cambio sólo duplica lo que ocupa ese rectángulo.
/// </remarks>
public sealed class LayerRectEdit(int layer, int left, int top, TilePatch before, TilePatch after)
    : IMapEdit
{
    public void Undo(TileMap map) => map.Restore(layer, left, top, before);

    public void Redo(TileMap map) => map.Restore(layer, left, top, after);
}

/// <summary>
/// Un cambio de tamaño del mapa, que afecta a todas las capas a la vez.
/// </summary>
/// <remarks>
/// Aquí sí se guarda todo el contenido, porque al encoger se pierden filas y columnas
/// enteras. Redimensionar es raro y deliberado, así que el coste está bien puesto.
/// </remarks>
public sealed class MapResizeEdit(
    int widthBefore,
    int heightBefore,
    IReadOnlyList<TilePatch> before,
    int widthAfter,
    int heightAfter,
    IReadOnlyList<TilePatch> after)
    : IMapEdit
{
    public void Undo(TileMap map) => map.RestoreSize(widthBefore, heightBefore, before);

    // También con su contenido: al redimensionar se puede desplazar lo que había, y
    // rehacer sólo el tamaño lo dejaría en otro sitio.
    public void Redo(TileMap map) => map.RestoreSize(widthAfter, heightAfter, after);
}

/// <summary>
/// Varios cambios que se deshacen de una vez.
/// </summary>
/// <remarks>
/// Una sustitución de tiles sobre todas las capas son varios rectángulos, pero para quien
/// la pidió es una sola cosa: deshacerla capa por capa sería desconcertante.
/// </remarks>
public sealed class MapEditGroup(IReadOnlyList<IMapEdit> edits) : IMapEdit
{
    public void Undo(TileMap map)
    {
        // Al revés de como se hicieron, que es lo que hace bien cualquier pila.
        for (int index = edits.Count - 1; index >= 0; index--)
            edits[index].Undo(map);
    }

    public void Redo(TileMap map)
    {
        foreach (IMapEdit edit in edits)
            edit.Redo(map);
    }
}

/// <summary>
/// La pila de deshacer de un mapa.
/// </summary>
/// <remarks>
/// Limitada a unos pocos pasos a propósito: un mapa grande con muchas operaciones de
/// relleno guardaría mucho, y en la práctica nadie deshace veinte veces seguidas.
/// </remarks>
public sealed class UndoStack
{
    public const int MaxSteps = 20;

    private readonly List<IMapEdit> _done = [];
    private readonly List<IMapEdit> _undone = [];

    private List<IMapEdit>? _stroke;

    public bool CanUndo => _done.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    /// <summary>Si hay un trazo abierto ahora mismo.</summary>
    public bool IsStroking => _stroke is not null;

    /// <summary>Ha cambiado lo que se puede deshacer o rehacer.</summary>
    public event Action? Changed;

    /// <summary>
    /// Abre un trazo: lo que venga hasta cerrarlo será un solo paso de deshacer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Estampar arrastrando pinta una celda por cada vez que el ratón cruza a otra, y cada
    /// una era un paso. Un arrastre despistado —creyendo que estabas en seleccionar— ponía
    /// treinta tiles y se llevaba por delante los veinte pasos de historia: justo cuando
    /// más falta hace deshacer, deshacer ya no sirve.
    /// </para>
    /// <para>
    /// Un paso tiene que ser lo que quien lo hizo entiende por «una cosa», y arrastrar de
    /// una punta a otra es una cosa, no treinta.
    /// </para>
    /// <para>
    /// Abrir dos veces no anida: mientras haya uno abierto, el segundo es el mismo. Quien
    /// abre trazos es el lienzo con el ratón, y ahí no hay más de un arrastre a la vez.
    /// </para>
    /// </remarks>
    public void BeginStroke() => _stroke ??= [];

    /// <summary>
    /// Cierra el trazo y lo deja como un solo paso.
    /// </summary>
    /// <remarks>
    /// Se puede llamar sin haber abierto ninguno y no pasa nada: lo cierran tanto soltar el
    /// botón como perder la captura del ratón, y los dos pueden llegar.
    /// </remarks>
    public void EndStroke()
    {
        if (_stroke is not { } stroke)
            return;

        _stroke = null;

        // Un trazo que no pintó nada —sobre una capa bloqueada, o un clic que no llegó a
        // cambiar nada— no deja paso: si no, deshacer no haría nada visible.
        if (stroke.Count == 0)
            return;

        // Uno solo se guarda tal cual: envolverlo en un grupo de uno no aporta nada y
        // enturbia lo que se lee al depurar.
        Record(stroke.Count == 1 ? stroke[0] : new MapEditGroup(stroke));
    }

    /// <summary>
    /// Anota un cambio recién hecho.
    /// </summary>
    /// <remarks>
    /// Hacer algo nuevo tira lo que hubiera para rehacer: a partir de aquí la historia es
    /// otra, y ofrecer rehacer algo que ya no encaja sería mentira.
    /// </remarks>
    public void Push(IMapEdit edit)
    {
        if (_stroke is not { } stroke)
        {
            Record(edit);

            return;
        }

        stroke.Add(edit);

        // Rehacer se tira al primer trazo del pincel y no al soltar: en cuanto se pinta
        // una celda la historia ya es otra, y el botón tiene que decirlo en ese momento.
        if (_undone.Count == 0)
            return;

        _undone.Clear();
        Changed?.Invoke();
    }

    private void Record(IMapEdit edit)
    {
        _done.Add(edit);
        _undone.Clear();

        if (_done.Count > MaxSteps)
            _done.RemoveAt(0);

        Changed?.Invoke();
    }

    public void Undo(TileMap map)
    {
        if (_done.Count == 0)
            return;

        IMapEdit edit = _done[^1];
        _done.RemoveAt(_done.Count - 1);

        edit.Undo(map);
        _undone.Add(edit);

        Changed?.Invoke();
    }

    public void Redo(TileMap map)
    {
        if (_undone.Count == 0)
            return;

        IMapEdit edit = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);

        edit.Redo(map);
        _done.Add(edit);

        Changed?.Invoke();
    }

    public void Clear()
    {
        // También el trazo a medias: si no, lo que se estuviera pintando aparecería como
        // un paso de la historia nueva en cuanto se soltara el ratón.
        _stroke = null;

        _done.Clear();
        _undone.Clear();

        Changed?.Invoke();
    }
}
