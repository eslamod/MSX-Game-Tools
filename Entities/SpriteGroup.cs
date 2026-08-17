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
    /// Ocho porque es lo que el VDP saca por línea de barrido en modo 2, así que un plano más
    /// no se vería nunca. Ojo con no confundir las dos cosas: ese tope es por línea y de toda
    /// la pantalla, no por personaje. Un grupo de ocho planos amontonados en la misma altura
    /// se come el cupo entero de esa línea, y en modo 1 —donde el cupo es de cuatro— pasa lo
    /// mismo con la mitad. Repartidos en vertical no estorban. Eso depende de los
    /// desplazamientos y de lo que haya alrededor, así que no es algo que el editor pueda
    /// decidir por nadie.
    /// </para>
    /// </remarks>
    public const int MaxMembers = 8;

    [ObservableProperty]
    private string _name;

    /// <summary>Imagen de referencia que se ve detrás de la composición de este grupo.</summary>
    [ObservableProperty]
    private BackgroundRef _background = BackgroundRef.None;

    public SpriteGroup(string name)
    {
        _name = name;

        Members.CollectionChanged += OnMembersChanged;
    }

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
