using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Un personaje multicolor: de uno a cuatro sprites superpuestos con desplazamiento,
/// que emula lo que en el hardware serían otros tantos planos de la tabla de atributos.
/// </summary>
/// <remarks>
/// El orden de los miembros es la prioridad: el primero es el de mayor prioridad, y
/// es contra él contra quien combinan su color los que llevan CC.
/// </remarks>
public partial class SpriteGroup : ObservableObject
{
    /// <summary>Un grupo no puede pasar de cuatro planos superpuestos.</summary>
    public const int MaxMembers = 4;

    [ObservableProperty]
    private string _name;

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
