namespace MSX_GameTools.Entities;

/// <summary>
/// Lleva la cuenta de los intercambios de color que se han hecho en una paleta, para
/// poder reajustar de una vez los dibujos que la usaban.
/// </summary>
/// <remarks>
/// <para>
/// No guarda la lista de intercambios sino <b>dónde ha acabado cada color</b>. Aplicar
/// los intercambios uno detrás de otro sobre los dibujos da mal: con A↔B y luego B↔C, el
/// primer pase manda al B lo que era A y el segundo se lo vuelve a llevar al C, y el
/// color A termina en dos sitios. Con la permutación se hace una sola pasada y sale bien
/// encadenes los que encadenes.
/// </para>
/// </remarks>
public sealed class PaletteSwaps
{
    /// <summary>En cada ranura, el índice del que era ese color antes de tocar nada.</summary>
    private readonly int[] _origin;

    public PaletteSwaps(int size = ColorPalette.Size) =>
        _origin = [.. Enumerable.Range(0, size)];

    /// <summary>No se ha movido nada de su sitio.</summary>
    public bool IsEmpty
    {
        get
        {
            for (int slot = 0; slot < _origin.Length; slot++)
            {
                if (_origin[slot] != slot)
                    return false;
            }

            return true;
        }
    }

    public int Size => _origin.Length;

    /// <summary>En qué ranura estaba el color que ahora ocupa ésta.</summary>
    public int OriginOf(int slot) => _origin[slot];

    public void Swap(int one, int other) =>
        (_origin[one], _origin[other]) = (_origin[other], _origin[one]);

    public void Reset()
    {
        for (int slot = 0; slot < _origin.Length; slot++)
            _origin[slot] = slot;
    }

    /// <summary>
    /// Del índice viejo al nuevo: <c>tabla[color de antes]</c> da dónde está ahora.
    /// </summary>
    /// <remarks>
    /// Es la vuelta de lo que guarda <see cref="_origin"/>, que va de ranura a color de
    /// antes. Los dibujos apuntan al color de antes y hay que decirles la ranura nueva.
    /// </remarks>
    public int[] Table()
    {
        int[] table = new int[_origin.Length];

        for (int slot = 0; slot < _origin.Length; slot++)
            table[_origin[slot]] = slot;

        return table;
    }

    /// <summary>
    /// Del índice de ahora al de antes, que es la vuelta de <see cref="Table"/>.
    /// </summary>
    /// <remarks>
    /// Con esto se deshace: reajustar los dibujos con esta tabla los devuelve a los índices
    /// que tenían, y sirve igual para devolver cada color a la ranura de la que salió.
    /// </remarks>
    public int[] Back() => [.. _origin];
}
