using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que exportamos lo ensamblan de verdad los ensambladores que usa la gente.
/// </summary>
/// <remarks>
/// <para>
/// Comparando bytes y no mirando si dio error: un ensamblador puede tragarse una línea y
/// entender otra cosa —un número en otra base, una directiva que hace algo parecido— y eso no
/// se ve en la salida, se ve en el binario.
/// </para>
/// <para>
/// Cada uno se salta si no está instalado, para que la suite corra en una máquina que no los
/// tenga. Que se salten todos y la prueba pase en verde no es un fallo de la prueba: es que
/// aquí no había con qué comprobarlo.
/// </para>
/// </remarks>
public class AssemblersTests
{
    /// <summary>
    /// Cuál, y con qué directiva de datos.
    /// </summary>
    /// <remarks>
    /// Medido, no supuesto: sasSX exige el punto y pasmo lo rechaza, así que no hay una grafía
    /// que valga para los cuatro y por eso se elige en las preferencias. asMSX y sjasmplus
    /// aceptan las dos.
    /// </remarks>
    public static TheoryData<string, string> Assemblers => new()
    {
        { "sasSX", AsmStyle.Dotted },
        { "sjasmplus", AsmStyle.Dotted },
        { "sjasmplus", AsmStyle.Plain },
        { "pasmo", AsmStyle.Plain },
    };

    /// <summary>Lo exportado ensambla, y sale exactamente el mismo binario.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Assemblers))]
    public void Lo_exportado_ensambla_y_da_los_mismos_bytes(string name, string data)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        TileSet tileSet = Painted();

        string asm = TileSetExporter.PatternsToAssembler(tileSet, new AsmStyle(data));
        byte[] expected = TileSetExporter.PatternsToBinary(tileSet);

        Assert.Equal(expected, Assemble(tool!, asm, name));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un juego de tiles con dibujo, para que los bytes no sean todos iguales.</summary>
    private static TileSet Painted()
    {
        var tileSet = new TileSet("Prueba");

        for (int index = 0; index < TileSet.TileCount; index++)
        {
            for (int row = 0; row < Tile.Rows; row++)
                tileSet.ListOfTiles[index].ArrayTileRows[row].ArrayPattern[(index + row) % 8] = true;
        }

        return tileSet;
    }

    /// <summary>Ensambla ese texto y devuelve los bytes que salen.</summary>
    private static byte[] Assemble(string tool, string asm, string name)
    {
        string folder = Directory.CreateTempSubdirectory("asm").FullName;

        try
        {
            string source = Path.Combine(folder, "salida.asm");
            string binary = Path.Combine(folder, "salida.bin");

            File.WriteAllText(source, asm);

            string said = Assembler.Run(tool, name, source, binary);

            Assert.True(File.Exists(binary), $"{name} no sacó binario: {said}");

            return File.ReadAllBytes(binary);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
