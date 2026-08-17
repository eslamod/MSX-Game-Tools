namespace MSX_GameTools.Services;

/// <summary>
/// Reparte los colores de una hoja de sprites entre los cuatro bits del índice de paleta.
/// </summary>
/// <remarks>
/// <para>
/// En modo 2 el V9938 combina con OR el color de los sprites superpuestos que llevan CC. Si
/// cada plano va coloreado con una potencia de dos, un pixel de índice <c>v</c> lo cubren
/// exactamente los planos cuyos bits están en <c>v</c>, y el OR devuelve <c>v</c>. O sea que
/// la descomposición sale sola: lo único que hay que decidir es qué índice le toca a cada
/// color, y eso es lo que decide cuántos planos hace falta superponer.
/// </para>
/// <para>
/// De ahí sale la regla que lo gobierna todo: <b>los planos que necesita una línea son los
/// bits distintos que suman los índices de sus colores</b>. Una línea de tres colores en los
/// índices 1, 2 y 3 son dos planos; esos mismos tres colores en los índices 1, 2 y 4 son tres.
/// </para>
/// <para>
/// La consecuencia menos evidente es que el reparto es <b>de toda la hoja a la vez</b> y no de
/// cada celda: la paleta es una sola. Dos personajes que compartan el blanco y el negro pero
/// uno lleve rojo y el otro azul no pueden salir los dos a dos planos, porque el rojo y el azul
/// tendrían que ocupar el mismo bit. Por eso el número de planos no se pide, se calcula.
/// </para>
/// </remarks>
public static class SpritePlaneAssignment
{
    /// <summary>Los cuatro bits del índice de color de un sprite.</summary>
    public const int MaxPlanes = 4;

    /// <summary>Índices que se pueden repartir: del 1 al 15, que el 0 es el transparente.</summary>
    public const int MaxColors = 15;

    /// <summary>
    /// Lo que se sabe de un reparto: qué índice lleva cada color y cuántos planos pide.
    /// </summary>
    /// <param name="Masks">
    /// El índice de paleta de cada color, en el orden en que se dieron los colores.
    /// </param>
    /// <param name="Planes">Los planos que hace falta superponer en la línea que más pide.</param>
    public sealed record Assignment(IReadOnlyList<int> Masks, int Planes);

    /// <summary>
    /// Reparte los índices para que ninguna línea pase de <paramref name="maxPlanes"/> planos.
    /// </summary>
    /// <param name="lines">
    /// Los colores que coinciden en cada línea, como números de color. Las líneas repetidas no
    /// estorban: se agrupan. Las de un solo color sí cuentan, porque un color con el índice 3
    /// pide dos planos él solo.
    /// </param>
    /// <param name="colors">Cuántos colores distintos hay, sin contar el transparente.</param>
    /// <returns>El reparto, o <c>null</c> si con ese tope no hay ninguno.</returns>
    public static Assignment? Solve(IReadOnlyList<IReadOnlyList<int>> lines, int colors, int maxPlanes)
    {
        if (colors is <= 0 or > MaxColors || maxPlanes is < 1 or > MaxPlanes)
            return null;

        // Sólo importa con quién coincide cada color, no en cuántas líneas: dos líneas con los
        // mismos colores atan lo mismo que una. Agrupándolas, una hoja de miles de líneas se
        // queda en las pocas combinaciones distintas que de verdad tiene.
        List<int> groups = [.. Distinct(lines, colors)];

        int[] masks = new int[colors];

        // Los más enredados primero: son los que menos sitio tienen, y dejarlos para el final
        // es lo que hace que haya que deshacer medio reparto para colocar el último.
        int[] order = [.. Enumerable.Range(0, colors).OrderByDescending(color => Neighbours(groups, color))];

        return Place(groups, masks, order, at: 0, maxPlanes)
            ? new Assignment(masks, Planes(groups, masks))
            : null;
    }

    /// <summary>Los planos que pide el reparto: los de la línea que más suma.</summary>
    public static int Planes(IReadOnlyList<int> groups, IReadOnlyList<int> masks) =>
        groups.Count == 0 ? 0 : groups.Max(group => Bits(Merge(group, masks)));

    /// <summary>
    /// Colores que no caben juntos con el tope de planos que se ha puesto.
    /// </summary>
    /// <param name="Colors">Los números de color implicados.</param>
    /// <param name="Shared">
    /// Los que salen en las dos líneas, o vacío cuando el choque es de una línea sola. Son la
    /// explicación: si el negro sale con el rojo en una línea y con el azul en otra, es el
    /// negro el que ata a los otros dos, y quitarlo de en medio arregla las dos.
    /// </param>
    public sealed record PlaneClash(IReadOnlyList<int> Colors, IReadOnlyList<int> Shared);

    /// <summary>
    /// Por qué no hay reparto: qué colores se estorban.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dos formas de no caber. Una línea con más colores de los que dan los planos no cabe ella
    /// sola. Y dos líneas que comparten colores tienen que caber en los mismos bits aunque cada
    /// una por separado quepa: es lo que pasa con dos personajes que comparten el blanco y el
    /// negro y llevan uno rojo y otro azul.
    /// </para>
    /// <para>
    /// No cubre todos los casos, y no pasa nada: cuando no sabe atribuirlo devuelve nada y
    /// queda el aviso de siempre. Una explicación de menos es un aviso más flojo; una
    /// explicación inventada manda a retocar el color que no era.
    /// </para>
    /// </remarks>
    public static PlaneClash? Explain(
        IReadOnlyList<IReadOnlyList<int>> lines, int colors, int maxPlanes)
    {
        if (colors is <= 0 or > MaxColors || maxPlanes is < 1 or > MaxPlanes)
            return null;

        List<int> groups = [.. Distinct(lines, colors)];

        // Con N planos salen 2^N − 1 colores: tres con dos planos, siete con tres, quince con
        // cuatro. Es el tope de colores distintos que puede haber en el mismo sitio.
        int fit = (1 << maxPlanes) - 1;

        // Primero las que no caben solas, que son las que se arreglan de una: sobran colores.
        foreach (int group in groups.OrderByDescending(Bits))
        {
            if (Bits(group) > fit)
                return new PlaneClash(Numbers(group), []);
        }

        // Y luego los pares que se atan. El de más solapamiento primero: es donde está el color
        // que más estorba, y quitarlo de en medio arregla más de un sitio.
        foreach (int one in groups)
        {
            foreach (int other in groups)
            {
                int shared = one & other;

                if (one == other || shared == 0 || Bits(one | other) <= fit)
                    continue;

                return new PlaneClash(Numbers(one | other), Numbers(shared));
            }
        }

        return null;
    }

    /// <summary>Los números de color de una máscara de coincidencia.</summary>
    private static int[] Numbers(int group) =>
        [.. Enumerable.Range(0, MaxColors + 1).Where(color => (group & (1 << color)) != 0)];

    /// <summary>
    /// Coloca un color detrás de otro, deshaciendo cuando el siguiente ya no cabe.
    /// </summary>
    /// <remarks>
    /// Con quince colores y quince índices esto es pequeño de sobra para probarlo entero. El
    /// corte de la poda no es el número de nodos sino que se comprueba el tope en cada paso: en
    /// cuanto una línea a medias ya se pasa de planos, esa rama entera sobra.
    /// </remarks>
    private static bool Place(
        IReadOnlyList<int> groups, int[] masks, IReadOnlyList<int> order, int at, int maxPlanes)
    {
        if (at == order.Count)
            return true;

        int color = order[at];

        for (int mask = 1; mask <= MaxColors; mask++)
        {
            // Dos colores no pueden compartir índice: serían el mismo color en la paleta.
            if (Array.IndexOf(masks, mask) >= 0)
                continue;

            masks[color] = mask;

            if (Fits(groups, masks, maxPlanes) && Place(groups, masks, order, at + 1, maxPlanes))
                return true;

            masks[color] = 0;
        }

        return false;
    }

    /// <summary>
    /// Si ninguna línea se pasa de planos con lo que hay repartido hasta ahora.
    /// </summary>
    /// <remarks>
    /// Los colores que todavía no tienen índice cuentan como cero bits, así que esto no dice
    /// que el reparto valga: dice que <b>todavía puede valer</b>. Es lo que hace que la poda
    /// sea correcta, porque una línea que ya se pasa no va a mejorar añadiéndole colores.
    /// </remarks>
    private static bool Fits(IReadOnlyList<int> groups, IReadOnlyList<int> masks, int maxPlanes) =>
        groups.All(group => Bits(Merge(group, masks)) <= maxPlanes);

    /// <summary>Los bits que suman entre todos los colores de una línea.</summary>
    private static int Merge(int group, IReadOnlyList<int> masks)
    {
        int merged = 0;

        for (int color = 0; color < masks.Count; color++)
        {
            if ((group & (1 << color)) != 0)
                merged |= masks[color];
        }

        return merged;
    }

    /// <summary>Con cuántos colores coincide uno en alguna línea.</summary>
    private static int Neighbours(IReadOnlyList<int> groups, int color)
    {
        int with = 0;

        foreach (int group in groups)
        {
            if ((group & (1 << color)) != 0)
                with |= group;
        }

        return Bits(with);
    }

    /// <summary>Las combinaciones de colores distintas que trae la hoja, como máscaras.</summary>
    private static IEnumerable<int> Distinct(IReadOnlyList<IReadOnlyList<int>> lines, int colors)
    {
        var seen = new HashSet<int>();

        foreach (IReadOnlyList<int> line in lines)
        {
            int group = 0;

            foreach (int color in line)
            {
                if ((uint)color < (uint)colors)
                    group |= 1 << color;
            }

            // También las de un solo color. Parece que no atan, pero un color con el índice 3
            // pide dos planos él solo: hacen falta los dos sprites superpuestos para que el OR
            // dé 3. Lo que no ata es una línea vacía.
            if (group != 0)
                seen.Add(group);
        }

        return seen;
    }

    private static int Bits(int value) => System.Numerics.BitOperations.PopCount((uint)value);
}
