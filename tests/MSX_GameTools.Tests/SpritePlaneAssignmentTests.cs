using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El reparto de los colores de una hoja entre los cuatro bits del índice.
/// </summary>
/// <remarks>
/// La regla que lo gobierna todo: los planos que necesita una línea son los bits distintos que
/// suman los índices de sus colores. Con los planos coloreados a potencias de dos, el OR del
/// VDP reconstruye el índice de cada pixel, así que repartir bien los índices es lo único que
/// decide cuántos sprites hay que superponer.
/// </remarks>
public class SpritePlaneAssignmentTests
{
    /// <summary>Los planos que pide un reparto para unas líneas dadas.</summary>
    private static int PlanesOf(IReadOnlyList<int[]> lines, IReadOnlyList<int> masks)
    {
        int most = 0;

        foreach (int[] line in lines)
        {
            int merged = 0;

            foreach (int color in line)
                merged |= masks[color];

            most = Math.Max(most, System.Numerics.BitOperations.PopCount((uint)merged));
        }

        return most;
    }

    /// <summary>Tres colores que van siempre juntos caben en dos planos.</summary>
    /// <remarks>
    /// Es el caso que hace útil el CC: dos sprites superpuestos dan tres colores, porque el
    /// tercero es el OR de los otros dos.
    /// </remarks>
    [Fact]
    public void Tres_colores_que_van_juntos_caben_en_dos_planos()
    {
        int[][] lines = [[0, 1, 2]];

        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve(lines, colors: 3, maxPlanes: 2);

        Assert.NotNull(solved);
        Assert.Equal(2, solved.Planes);

        // Y el reparto de verdad los deja en dos bits: uno de los tres es el OR de los otros.
        Assert.Equal(2, PlanesOf(lines, solved.Masks));
    }

    /// <summary>Cuatro colores en la misma línea no caben en dos planos.</summary>
    /// <remarks>
    /// Dos planos dan tres colores y no cuatro: 2² − 1. No es una limitación del reparto sino
    /// del hardware, y por eso aquí se devuelve que no hay solución en vez de aproximar.
    /// </remarks>
    [Fact]
    public void Cuatro_colores_en_una_linea_no_caben_en_dos_planos()
    {
        Assert.Null(SpritePlaneAssignment.Solve([[0, 1, 2, 3]], colors: 4, maxPlanes: 2));
    }

    /// <summary>Y en tres planos sí, que dan siete.</summary>
    [Fact]
    public void Cuatro_colores_en_una_linea_caben_en_tres_planos()
    {
        int[][] lines = [[0, 1, 2, 3]];

        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve(lines, colors: 4, maxPlanes: 3);

        Assert.NotNull(solved);
        Assert.Equal(3, PlanesOf(lines, solved.Masks));
    }

    /// <summary>
    /// El reparto es de toda la hoja, no de cada celda.
    /// </summary>
    /// <remarks>
    /// Es la consecuencia menos evidente de que la paleta sea una sola, y la que decide de
    /// verdad cuántos planos hace falta superponer. Dos personajes que compartan el blanco y el
    /// negro pero uno lleve rojo y el otro azul no pueden salir los dos a dos planos: el rojo y
    /// el azul tendrían que ocupar el mismo bit, y entonces serían el mismo color.
    /// </remarks>
    [Fact]
    public void Dos_personajes_que_comparten_colores_no_caben_en_dos_planos()
    {
        // 0 blanco, 1 negro, 2 rojo, 3 azul.
        int[][] lines = [[0, 1, 2], [0, 1, 3]];

        Assert.Null(SpritePlaneAssignment.Solve(lines, colors: 4, maxPlanes: 2));

        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve(lines, colors: 4, maxPlanes: 3);

        Assert.NotNull(solved);
        Assert.Equal(3, PlanesOf(lines, solved.Masks));
    }

    /// <summary>Colores que nunca coinciden no se estorban, por muchos que sean.</summary>
    /// <remarks>
    /// Lo que ata es coincidir en una línea, no existir. Una hoja con quince colores donde cada
    /// personaje usa dos sale a dos planos.
    /// </remarks>
    [Fact]
    public void Colores_que_nunca_coinciden_no_se_estorban()
    {
        int[][] lines = [[0, 1], [2, 3], [4, 5], [6, 7]];

        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve(lines, colors: 8, maxPlanes: 2);

        Assert.NotNull(solved);
        Assert.Equal(2, PlanesOf(lines, solved.Masks));
    }

    /// <summary>Dos colores distintos nunca comparten índice: serían el mismo color.</summary>
    [Fact]
    public void Cada_color_se_lleva_un_indice_distinto()
    {
        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve([[0, 1], [2, 3]], colors: 4, maxPlanes: 2);

        Assert.NotNull(solved);
        Assert.Equal(4, solved.Masks.Distinct().Count());
        Assert.All(solved.Masks, mask => Assert.InRange(mask, 1, SpritePlaneAssignment.MaxColors));
    }

    /// <summary>Una línea de un solo color no ata nada.</summary>
    [Fact]
    public void Una_linea_de_un_color_no_ata_nada()
    {
        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve([[0], [1], [2], [3]], colors: 4, maxPlanes: 1);

        Assert.NotNull(solved);
        Assert.Equal(1, PlanesOf([[0], [1], [2], [3]], solved.Masks));
    }

    /// <summary>Los quince colores caben si hace falta, que es el tope de la paleta.</summary>
    [Fact]
    public void Los_quince_colores_juntos_caben_en_cuatro_planos()
    {
        int[] all = [.. Enumerable.Range(0, 15)];

        SpritePlaneAssignment.Assignment? solved =
            SpritePlaneAssignment.Solve([all], colors: 15, maxPlanes: 4);

        Assert.NotNull(solved);
        Assert.Equal(4, PlanesOf([all], solved.Masks));
    }

    /// <summary>Y dieciséis no, que el 0 es el transparente.</summary>
    [Fact]
    public void Dieciseis_colores_no_caben()
    {
        Assert.Null(SpritePlaneAssignment.Solve([[0]], colors: 16, maxPlanes: 4));
    }
}
