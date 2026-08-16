namespace MSX_GameTools.Entities;

/// <summary>
/// Cómo se llama cada uno de los ocho atributos de un juego de tiles.
/// </summary>
/// <remarks>
/// <para>
/// Un atributo <b>es</b> un bit, y el bit es su posición en esta lista: el 0 es el bit 0
/// del byte que se exporta. Por eso no se reordenan. Mover «Escalera» del bit 2 al 5
/// obligaría a mover ese bit en los 256 tiles —el mismo lío que intercambiar colores en la
/// paleta—, y aquí no hace falta pasar por ahí: se le cambia el nombre y ya.
/// </para>
/// <para>
/// Un atributo está definido si tiene nombre, y borrar el nombre lo deja sin definir sin
/// tocar los tiles. El bit se queda como estaba, sólo deja de enseñarse; si mañana se
/// vuelve a nombrar, lo marcado sigue ahí. Borrar un rótulo no debería borrar trabajo.
/// </para>
/// <para>
/// Ocho y no dieciséis porque ocho son un byte redondo por tile y cubren lo que se usa
/// —sólido, plataforma, escalera, agua, daño, rompible, disparador, decorativo—. Si algún
/// día hicieran falta más, lo que hay que tocar es esta constante, la exportación y la
/// versión del formato.
/// </para>
/// </remarks>
public sealed class TileAttributeNames
{
    public const int Count = 8;

    private readonly string[] _names = [.. Enumerable.Repeat(string.Empty, Count)];

    /// <summary>Si hay al menos uno definido. Con ninguno, esto no se enseña en ningún sitio.</summary>
    public bool Any => _names.Any(name => name.Length > 0);

    /// <summary>Los bits que tienen nombre, en orden.</summary>
    public IEnumerable<int> Defined =>
        Enumerable.Range(0, Count).Where(bit => _names[bit].Length > 0);

    /// <summary>El nombre de ese bit, o vacío si no está definido.</summary>
    public string this[int bit] => Inside(bit) ? _names[bit] : string.Empty;

    public bool IsDefined(int bit) => this[bit].Length > 0;

    /// <summary>
    /// Le pone nombre a un bit. Vacío lo deja sin definir.
    /// </summary>
    /// <remarks>
    /// Se recorta: un nombre que es sólo espacios no es un nombre, y dejarlo pasar daría un
    /// atributo definido que en el editor sale como una casilla sin rótulo.
    /// </remarks>
    public void Define(int bit, string? name)
    {
        if (Inside(bit))
            _names[bit] = name?.Trim() ?? string.Empty;
    }

    public void CopyFrom(TileAttributeNames other)
    {
        for (int bit = 0; bit < Count; bit++)
            _names[bit] = other._names[bit];
    }

    private static bool Inside(int bit) => (uint)bit < Count;
}
