using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un atributo del tile que se está editando: su nombre y si lo tiene puesto.
/// </summary>
/// <remarks>
/// Sólo salen los que tienen nombre. Un bit sin nombre puede estar puesto en el tile —de
/// cuando lo tuvo— y se sigue guardando y exportando, pero no se enseña: enseñar una casilla
/// sin rótulo no ayuda a nadie a decidir si marcarla.
/// </remarks>
public sealed partial class TileFlagViewModel : ObservableObject
{
    private readonly Action<int, bool> _write;

    [ObservableProperty]
    private bool _isOn;

    public TileFlagViewModel(int bit, string name, bool isOn, Action<int, bool> write)
    {
        Bit = bit;
        Name = name;
        _isOn = isOn;
        _write = write;
    }

    public int Bit { get; }

    public string Name { get; }

    /// <summary>Qué bit es, para quien luego tenga que escribir el <c>bit 2, a</c>.</summary>
    public string Label => $"{Bit} · {Name}";

    /// <summary>
    /// Escribe en el tile lo que se acaba de marcar.
    /// </summary>
    /// <remarks>
    /// Al cambiar de tile las casillas también se mueven, y esto salta igual: quien
    /// distingue marcar de sólo enseñar es el editor, que no ensucia el juego si el tile ya
    /// estaba así. Se lleva ahí y no aquí porque es él quien decide qué cuenta como haber
    /// tocado algo, y tener la misma guarda en dos sitios deja las dos sin vigilar.
    /// </remarks>
    partial void OnIsOnChanged(bool value) => _write(Bit, value);
}
