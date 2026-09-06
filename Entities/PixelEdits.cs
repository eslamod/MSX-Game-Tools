namespace MSX_GameTools.Entities;

/// <summary>
/// Un dibujo que se puede deshacer.
/// </summary>
/// <remarks>
/// A diferencia de los del mapa, éstos no reciben sobre qué actuar: cada uno se guarda el
/// tile o el patrón al que pertenece. De un mapa hay uno solo; aquí un paso puede ser de
/// cualquiera de los 256 tiles o de los 64 patrones, y al deshacer lo normal es que se esté
/// mirando otro.
/// </remarks>
public interface IPixelEdit
{
    void Undo();

    void Redo();
}

/// <summary>
/// Un tile dibujado, con lo que tenía antes y lo que quedó.
/// </summary>
/// <remarks>
/// El tile entero y no los pixeles que cambiaron: son ocho filas de dos bytes, así que los
/// veinte pasos ocupan menos que una miniatura. Y guardándolo entero, deshacer devuelve
/// también los colores por línea, que el trazo puede haber cambiado de camino.
/// </remarks>
public sealed class TileDrawn(Tile tile, Tile before, Tile after) : IPixelEdit
{
    public void Undo() => tile.CopyFrom(before);

    public void Redo() => tile.CopyFrom(after);
}

/// <summary>Un patrón de sprite dibujado.</summary>
/// <inheritdoc cref="TileDrawn" path="/remarks"/>
public sealed class SpriteDrawn(Sprite sprite, Sprite before, Sprite after) : IPixelEdit
{
    public void Undo() => sprite.CopyFrom(before);

    public void Redo() => sprite.CopyFrom(after);
}

/// <summary>
/// Un trozo estampado en la rejilla, con lo que había debajo.
/// </summary>
/// <remarks>
/// El trozo entero y no los tiles que cambiaron: es lo que ya devolvía estampar, y un trozo
/// de la rejilla son unos pocos tiles. Rehacer vuelve a estampar lo mismo en el mismo sitio,
/// que da el mismo resultado porque el recorte al borde de la rejilla también es el mismo.
/// </remarks>
public sealed class TilesStamped(
    TileSet tileSet, int left, int top, TileSetPatch before, TileSetPatch after) : IPixelEdit
{
    public void Undo() => tileSet.Stamp(left, top, before);

    public void Redo() => tileSet.Stamp(left, top, after);
}

/// <summary>
/// La lista de planos de un grupo, antes y después de tocarla.
/// </summary>
/// <remarks>
/// La lista entera y no la operación que se hizo: añadir, quitar y subir o bajar un plano se
/// deshacen igual, y son los mismos objetos los que vuelven, así que un plano recuperado trae
/// sus desplazamientos y sus colores donde estaban.
/// </remarks>
public sealed class MembersChanged(
    SpriteGroup group,
    IReadOnlyList<SpriteGroupMember> before,
    IReadOnlyList<SpriteGroupMember> after) : IPixelEdit
{
    public void Undo() => Restore(before);

    public void Redo() => Restore(after);

    /// <summary>
    /// Deja la lista tal cual estaba.
    /// </summary>
    /// <remarks>
    /// Por la colección y no por <c>Group.Add</c>, que mira el tope: lo que se devuelve ya
    /// cabía, y pasar por el tope al restaurar sería negarse a deshacer justo cuando el grupo
    /// está lleno.
    /// </remarks>
    private void Restore(IReadOnlyList<SpriteGroupMember> members)
    {
        group.Members.Clear();

        foreach (SpriteGroupMember member in members)
            group.Members.Add(member);
    }
}

/// <summary>
/// Dónde está un plano: qué patrón usa y dónde se coloca.
/// </summary>
/// <remarks>
/// Los tres números que se manejan a la vez mientras se compone una figura: se arrastra, se
/// afina con los cursores y se escribe el patrón, y todo eso es «colocar el plano».
/// </remarks>
public readonly record struct MemberSpot(int Pattern, int X, int Y)
{
    public static MemberSpot Of(SpriteGroupMember member) =>
        new(member.PatternIndex, member.OffsetX, member.OffsetY);

    public void ApplyTo(SpriteGroupMember member)
    {
        member.PatternIndex = Pattern;
        member.OffsetX = X;
        member.OffsetY = Y;
    }
}

/// <summary>Un plano colocado en otro sitio, o cambiado de patrón.</summary>
/// <remarks>
/// Un arrastre entero es un paso: lo abre la pulsación y lo cierra soltar, igual que un trazo
/// en el lienzo. Los cursores y las cajas dejan uno por cambio, que es lo que se espera de un
/// ajuste fino.
/// </remarks>
public sealed class MemberMoved(SpriteGroupMember member, MemberSpot before, MemberSpot after)
    : IPixelEdit
{
    public void Undo() => before.ApplyTo(member);

    public void Redo() => after.ApplyTo(member);
}

/// <summary>
/// La pila de deshacer de lo que se dibuja en un juego de tiles o en un banco.
/// </summary>
/// <remarks>
/// <para>
/// Los mismos veinte pasos que el mapa. No es la misma clase porque lo que guarda es de otra
/// forma: allí un paso es un rectángulo de una capa del único mapa que hay, y se agrupan los
/// pasos de un arrastre; aquí un paso es un tile o un patrón entero, y el trazo lo agrupa el
/// lienzo, que sabe cuándo empieza y cuándo acaba.
/// </para>
/// <para>
/// Lo que no pasa por aquí <b>tira la historia</b>. Estampar, pegar, importar o mover los
/// colores de la paleta cambian tiles sin dejar paso, y deshacer después devolvería una foto
/// vieja encima de lo que hicieran: se vería como si deshacer resucitara cosas borradas. Es
/// mejor quedarse sin historia que tener una que miente.
/// </para>
/// </remarks>
public sealed class PixelUndoStack
{
    /// <inheritdoc cref="UndoStack.MaxSteps"/>
    public const int MaxSteps = UndoStack.MaxSteps;

    private readonly List<IPixelEdit> _done = [];
    private readonly List<IPixelEdit> _undone = [];

    /// <summary>Hay un trazo en marcha: lo que cambie mientras tanto es suyo.</summary>
    private bool _drawing;

    /// <summary>Se está deshaciendo o rehaciendo: los cambios que salgan de ahí son míos.</summary>
    private bool _applying;

    public bool CanUndo => _done.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    /// <summary>
    /// Está en medio de algo suyo: un paso abierto, o deshaciendo.
    /// </summary>
    /// <remarks>
    /// Lo pregunta quien vigila cambios sueltos para no anotar los que ya son de un paso
    /// abierto: los cincuenta que deja un arrastre son ese arrastre, no cincuenta pasos.
    /// </remarks>
    public bool Recording => _drawing || _applying;

    /// <summary>Ha cambiado lo que se puede deshacer o rehacer.</summary>
    public event Action? Changed;

    /// <summary>Empieza un trazo, que será un solo paso.</summary>
    public void Begin() => _drawing = true;

    /// <summary>
    /// Anota el trazo que acaba de terminar.
    /// </summary>
    /// <remarks>
    /// Dibujar algo nuevo tira lo que hubiera para rehacer: a partir de aquí la historia es
    /// otra, y ofrecer rehacer algo que ya no encaja sería mentira.
    /// </remarks>
    public void Push(IPixelEdit edit)
    {
        _done.Add(edit);
        _undone.Clear();

        if (_done.Count > MaxSteps)
            _done.RemoveAt(0);

        // El aviso, con el trazo todavía abierto: quien lo escucha repinta y dice que el
        // documento ha cambiado, y ese aviso llegaría aquí a tirar el paso recién anotado.
        Changed?.Invoke();

        _drawing = false;
    }

    /// <summary>El trazo terminó sin dejar nada que deshacer.</summary>
    public void Cancel() => _drawing = false;

    /// <summary>
    /// Ha cambiado algo del documento. Si no es cosa de esta pila, la historia deja de valer.
    /// </summary>
    public void Touched()
    {
        if (_drawing || _applying)
            return;

        Forget();
    }

    /// <summary>Se olvida lo que había, porque ya no describe lo que hay delante.</summary>
    public void Forget()
    {
        if (_done.Count == 0 && _undone.Count == 0)
            return;

        _done.Clear();
        _undone.Clear();

        Changed?.Invoke();
    }

    public void Undo() => Move(_done, _undone, edit => edit.Undo());

    public void Redo() => Move(_undone, _done, edit => edit.Redo());

    /// <summary>
    /// Saca el último de una pila, lo aplica y lo deja en la otra.
    /// </summary>
    /// <remarks>
    /// Con la bandera puesta mientras dura: aplicarlo cambia el tile, el documento avisa de
    /// que ha cambiado, y sin la bandera ese aviso se llevaría por delante la historia que
    /// se está usando.
    /// </remarks>
    private void Move(List<IPixelEdit> from, List<IPixelEdit> to, Action<IPixelEdit> apply)
    {
        if (from.Count == 0)
            return;

        IPixelEdit edit = from[^1];
        from.RemoveAt(from.Count - 1);

        _applying = true;

        try
        {
            apply(edit);
            to.Add(edit);

            // Dentro de la bandera, por lo mismo que en Push: repintar avisa de que hay
            // cambios, y ese aviso no puede llevarse por delante la historia que se usa.
            Changed?.Invoke();
        }
        finally
        {
            _applying = false;
        }
    }
}
