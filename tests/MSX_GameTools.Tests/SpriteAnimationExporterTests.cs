using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La tira de bytes de las animaciones, que es lo que recorrerá el juego.
/// </summary>
public class SpriteAnimationExporterTests
{
    /// <summary>
    /// Un fotograma corriente ocupa tres bytes y uno desplazado, seis.
    /// </summary>
    /// <remarks>
    /// Es la razón de que haya dos: lo normal es no moverse ni avisar de nada, y pagar los
    /// tres bytes de más en cada fotograma para dejarlos a cero es justo lo que no interesa.
    /// </remarks>
    [AvaloniaFact]
    public void Un_fotograma_quieto_ocupa_menos_que_uno_desplazado()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        var animation = new SpriteAnimation("Andar");

        animation.Steps.Add(new AnimationFrame { Target = 3, Wait = 6 });

        bank.Animations.Add(animation);

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Frame, 0x03, 0x06,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));

        animation.Steps.Add(new AnimationFrame { Target = 4, Wait = 6, OffsetX = 2 });

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Frame, 0x03, 0x06,
                SpriteAnimationExporter.MovedFrame, 0x04, 0x06, 0x00, 0x02, 0x00,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>Un desplazamiento hacia atrás sale en complemento a dos.</summary>
    [AvaloniaFact]
    public void Los_desplazamientos_negativos_salen_en_complemento_a_dos()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        var animation = new SpriteAnimation("Andar");

        animation.Steps.Add(new AnimationFrame { Target = 0, Wait = 1, OffsetX = -1, OffsetY = -128 });

        bank.Animations.Add(animation);

        byte[] bytes = SpriteAnimationExporter.ToBinary(bank);

        Assert.Equal(0x80, bytes[5]); // Y, que va primero como en la tabla de atributos
        Assert.Equal(0xFF, bytes[6]); // X
    }

    /// <summary>
    /// Un bucle sale como bucle y no estirado.
    /// </summary>
    /// <remarks>
    /// Estirarlo aquí dejaría la tira lista para leer de corrido, pero un bucle de doscientas
    /// vueltas ocuparía doscientas veces lo mismo en ROM. Es la decisión del formato.
    /// </remarks>
    [AvaloniaFact]
    public void Un_bucle_no_se_estira()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        var animation = new SpriteAnimation("Andar");
        var loop = new AnimationLoop { Times = 200 };

        loop.Steps.Add(new AnimationFrame { Target = 1, Wait = 4 });
        animation.Steps.Add(loop);

        bank.Animations.Add(animation);

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.LoopStart, 200,
                SpriteAnimationExporter.Frame, 0x01, 0x04,
                SpriteAnimationExporter.LoopEnd,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>Y un bucle dentro de otro también.</summary>
    [AvaloniaFact]
    public void Los_bucles_anidados_salen_anidados()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        var animation = new SpriteAnimation("Andar");
        var outer = new AnimationLoop { Times = 2 };
        var inner = new AnimationLoop { Times = 3 };

        inner.Steps.Add(new AnimationFrame { Target = 1, Wait = 1 });
        outer.Steps.Add(inner);
        animation.Steps.Add(outer);

        bank.Animations.Add(animation);

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.LoopStart, 0x02,
                SpriteAnimationExporter.LoopStart, 0x03,
                SpriteAnimationExporter.Frame, 0x01, 0x01,
                SpriteAnimationExporter.LoopEnd,
                SpriteAnimationExporter.LoopEnd,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>Lo que hacer al acabar va en la cabecera de cada animación.</summary>
    [AvaloniaTheory]
    [InlineData(AnimationMode.Single, 0x00)]
    [InlineData(AnimationMode.Loop, 0x01)]
    [InlineData(AnimationMode.PingPong, 0x02)]
    public void El_final_va_en_la_cabecera(AnimationMode mode, byte expected)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        bank.Animations.Add(new SpriteAnimation("Andar") { Mode = mode });

        Assert.Equal(expected, SpriteAnimationExporter.ToBinary(bank)[1]);
    }

    /// <summary>
    /// Y de qué están hechos los destinos, delante del todo.
    /// </summary>
    /// <remarks>
    /// Sin esto la tira no se puede leer: el 38 de una animación de patrones y el 38 de una de
    /// grupos son dos cosas distintas y en los bytes se veían igual. Lo destapó la ROM de
    /// prueba, que buscaba el grupo 38 de un banco que tiene dieciséis y no enseñaba nada.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(AnimationKind.Patterns, SpriteAnimationExporter.OfPatterns)]
    [InlineData(AnimationKind.Groups, SpriteAnimationExporter.OfGroups)]
    public void De_que_esta_hecha_va_delante_del_todo(AnimationKind kind, byte expected)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        bank.NewGroup(0);
        bank.Animations.Add(new SpriteAnimation("Andar") { Kind = kind });

        Assert.Equal(expected, SpriteAnimationExporter.ToBinary(bank)[0]);
    }

    /// <summary>
    /// Los grupos salen por el sitio que ocupan, no por su número.
    /// </summary>
    /// <remarks>
    /// Dentro se conocen por un número que no se mueve al borrar otros; fuera se exportan en
    /// orden y el juego los cuenta. Aquí se borra el primero para que las dos cosas no
    /// coincidan: si no, la prueba pasaría de las dos maneras.
    /// </remarks>
    [AvaloniaFact]
    public void Los_grupos_salen_por_su_sitio_y_no_por_su_numero()
    {
        SpriteBank bank = WithGroups(3);

        bank.Groups.Remove(bank.Groups[0]);

        int number = bank.Groups[1].Id;

        Assert.Equal(2, number);
        Assert.Equal(1, bank.OrdinalOfGroup(number));

        var animation = new SpriteAnimation("Andar") { Kind = AnimationKind.Groups };

        animation.Steps.Add(new AnimationFrame { Target = number, Wait = 1 });
        bank.Animations.Add(animation);

        Assert.Equal(1, SpriteAnimationExporter.ToBinary(bank)[3]);
        Assert.Empty(SpriteAnimationExporter.MissingGroups(bank));
    }

    /// <summary>
    /// Un grupo borrado sale como 0xFF, y se puede preguntar antes de exportar.
    /// </summary>
    /// <remarks>
    /// Borrar un grupo no toca las animaciones, así que se puede llegar a exportar con una
    /// apuntando a un número que ya no está. Cortar la exportación por eso sería peor: sale un
    /// valor que no es el sitio de ningún grupo, y encima se avisa.
    /// </remarks>
    [AvaloniaFact]
    public void Un_grupo_borrado_sale_marcado_y_se_avisa()
    {
        SpriteBank bank = WithGroups(2);

        var animation = new SpriteAnimation("Andar") { Kind = AnimationKind.Groups };

        animation.Steps.Add(new AnimationFrame { Target = 9, Wait = 1 });
        bank.Animations.Add(animation);

        Assert.Equal(SpriteAnimationExporter.NoGroup, SpriteAnimationExporter.ToBinary(bank)[3]);

        SpriteAnimationExporter.MissingGroup missing =
            Assert.Single(SpriteAnimationExporter.MissingGroups(bank));

        Assert.Equal("Andar", missing.Animation);
        Assert.Equal(9, missing.Group);
    }

    /// <summary>Un bucle también se mira al avisar de los grupos que faltan.</summary>
    [AvaloniaFact]
    public void Los_grupos_que_faltan_se_buscan_dentro_de_los_bucles()
    {
        SpriteBank bank = WithGroups(1);

        var animation = new SpriteAnimation("Andar") { Kind = AnimationKind.Groups };
        var loop = new AnimationLoop { Times = 2 };

        loop.Steps.Add(new AnimationFrame { Target = 9, Wait = 1 });
        animation.Steps.Add(loop);
        bank.Animations.Add(animation);

        Assert.Single(SpriteAnimationExporter.MissingGroups(bank));
    }

    /// <summary>Un banco sin animaciones no escribe nada, ni siquiera un terminador.</summary>
    [AvaloniaFact]
    public void Sin_animaciones_no_sale_nada()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        Assert.Empty(SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>
    /// El ensamblador trae las etiquetas por las que se recorre y una por animación.
    /// </summary>
    /// <remarks>
    /// El fichero no dice cuántas animaciones trae, igual que el de grupos: se recorre de
    /// <c>_animations</c> a <c>_animations_end</c>. La etiqueta de cada una es para poder
    /// referirla desde el juego sin contar bytes.
    /// </remarks>
    [AvaloniaFact]
    public void El_ensamblador_trae_las_etiquetas_para_recorrerlo()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho malo");

        bank.Animations.Add(new SpriteAnimation("Andar"));
        bank.Animations.Add(new SpriteAnimation("Saltar"));

        string text = SpriteAnimationExporter.ToAssembler(bank);

        Assert.Contains("bicho_malo_animations:", text);
        Assert.Contains("bicho_malo_animation_0:", text);
        Assert.Contains("bicho_malo_animation_1:", text);
        Assert.Contains("bicho_malo_animations_end:", text);
    }

    private static SpriteBank WithGroups(int many)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        for (int each = 0; each < many; each++)
            bank.NewGroup(each);

        return bank;
    }
}
