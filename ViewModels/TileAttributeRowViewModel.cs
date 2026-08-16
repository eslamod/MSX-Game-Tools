using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un hueco de la lista de atributos: su bit, cómo se llama y si se está mirando.
/// </summary>
/// <remarks>
/// El bit se enseña porque es lo que hay que escribir en el código de la máquina —un
/// <c>bit 2, a</c>—, y de nada sirve saber que el atributo se llama «Escalera» si hay que
/// contar filas para averiguar cuál es. Sin nombre, el atributo no está definido.
/// </remarks>
public sealed partial class TileAttributeRowViewModel(int bit, string name, Action<int, bool> watch)
    : ObservableObject
{
    [ObservableProperty]
    private string _name = name;

    /// <summary>
    /// Si su ojo está encendido, y con él el tinte en la rejilla de tiles.
    /// </summary>
    /// <remarks>
    /// Va suelto de lo demás del panel y surte efecto en el acto, sin esperar a Aceptar:
    /// no es una edición del juego, es una forma de mirarlo. Los nombres sí esperan, porque
    /// ésos sí lo cambian.
    /// </remarks>
    [ObservableProperty]
    private bool _isWatched;

    public int Bit { get; } = bit;

    partial void OnIsWatchedChanged(bool value) => watch(Bit, value);
}
