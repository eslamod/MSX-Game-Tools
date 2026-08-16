using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un hueco de la lista de atributos: su bit y cómo se llama.
/// </summary>
/// <remarks>
/// El bit se enseña porque es lo que hay que escribir en el código de la máquina —un
/// <c>bit 2, a</c>—, y de nada sirve saber que el atributo se llama «Escalera» si hay que
/// contar filas para averiguar cuál es. Sin nombre, el atributo no está definido.
/// </remarks>
public sealed partial class TileAttributeRowViewModel(int bit, string name) : ObservableObject
{
    [ObservableProperty]
    private string _name = name;

    public int Bit { get; } = bit;
}
