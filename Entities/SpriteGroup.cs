using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>
/// Un personaje multicolor: de uno a ocho sprites superpuestos con desplazamiento,
/// que emula lo que en el hardware serían otros tantos planos de la tabla de atributos.
/// </summary>
/// <remarks>
/// El orden de los miembros es la prioridad: el primero es el de mayor prioridad, y
/// es contra él contra quien combinan su color los que llevan CC.
/// </remarks>
public partial class SpriteGroup : ObservableObject
{
    /// <summary>
    /// Un grupo no puede pasar de ocho planos superpuestos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eran cuatro y se quedaban cortos en cuanto el personaje lleva varios colores por
    /// plano: dos sprites de tres colores cada uno ya son seis.
    /// </para>
    /// <para>
    /// Treinta y dos porque es lo que cabe en la tabla de atributos: más planos que ésos no
    /// los puede tener el VDP a la vez, ni repartidos entre todos los personajes de la
    /// pantalla.
    /// </para>
    /// <para>
    /// Estuvo en ocho, que es lo que el VDP saca por <b>línea de barrido</b> en modo 2, y era
    /// confundir dos cosas distintas: ese cupo es por línea y de toda la pantalla, no por
    /// personaje. Un grupo de ocho planos amontonados a la misma altura se come el cupo
    /// entero de esa línea —en modo 1, donde son cuatro, con la mitad basta—, pero repartidos
    /// en vertical no se estorban, y hay personajes de verdad que pasan de ocho: el que
    /// duerme en la pausa del K Mare gasta trece. Eso depende de los desplazamientos y de lo
    /// que haya alrededor, así que el editor lo avisa donde se ve —línea a línea— en vez de
    /// decidirlo por nadie a base de no dejar añadir.
    /// </para>
    /// </remarks>
    public const int MaxMembers = 32;

    [ObservableProperty]
    private string _name;

    /// <summary>Imagen de referencia que se ve detrás de la composición de este grupo.</summary>
    [ObservableProperty]
    private BackgroundRef _background = BackgroundRef.None;

    public SpriteGroup(string name, int id = 0)
    {
        _name = name;
        Id = id;

        Members.CollectionChanged += OnMembersChanged;
    }

    /// <summary>
    /// Su número, que no se mueve nunca y no se reaprovecha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No es su posición en la lista. La posición sí se mueve —borrar un grupo corre todos los
    /// de atrás— y eso dejaba a las animaciones apuntando a otro grupo sin que nada lo dijera.
    /// El banco ya había aprendido esto con los patrones: sus huecos son fijos precisamente para
    /// que ningún número se pueda mover.
    /// </para>
    /// <para>
    /// Al juego no le llega: los grupos se exportan en orden y él los direcciona por su
    /// posición. Esto es de puertas adentro, y se traduce a posición al exportar.
    /// </para>
    /// </remarks>
    public int Id { get; internal set; }

    /// <summary>Ha cambiado algo que afecta a cómo se ve el grupo.</summary>
    public event Action<SpriteGroup>? Changed;

    /// <summary>Los sprites del grupo, del de mayor al de menor prioridad.</summary>
    public ObservableCollection<SpriteGroupMember> Members { get; } = [];

    public bool CanAddMember => Members.Count < MaxMembers;

    public bool CanRemoveMember => Members.Count > 1;

    public bool Add(SpriteGroupMember member)
    {
        if (!CanAddMember)
            return false;

        Members.Add(member);

        return true;
    }

    public bool Remove(SpriteGroupMember member)
    {
        if (!CanRemoveMember)
            return false;

        return Members.Remove(member);
    }

    /// <summary>Sube un miembro en la prioridad. Devuelve la posición en que queda.</summary>
    public int MoveUp(SpriteGroupMember member) => Move(member, -1);

    /// <summary>Baja un miembro en la prioridad.</summary>
    public int MoveDown(SpriteGroupMember member) => Move(member, +1);

    private int Move(SpriteGroupMember member, int delta)
    {
        int from = Members.IndexOf(member);
        if (from < 0)
            return -1;

        int to = from + delta;
        if ((uint)to >= (uint)Members.Count)
            return from;

        Members.Move(from, to);

        return to;
    }

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (SpriteGroupMember member in e.OldItems?.Cast<SpriteGroupMember>() ?? [])
            member.PropertyChanged -= OnMemberPropertyChanged;

        foreach (SpriteGroupMember member in e.NewItems?.Cast<SpriteGroupMember>() ?? [])
            member.PropertyChanged += OnMemberPropertyChanged;

        OnPropertyChanged(nameof(CanAddMember));
        OnPropertyChanged(nameof(CanRemoveMember));

        Changed?.Invoke(this);
    }

    private void OnMemberPropertyChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this);
}
