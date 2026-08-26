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
                SpriteAnimationExporter.Paint, Fresh,
                SpriteAnimationExporter.Frame, 0x03, 0x06,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));

        animation.Steps.Add(new AnimationFrame { Target = 4, Wait = 6, OffsetX = 2 });

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, Fresh,
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

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, Fresh,
                SpriteAnimationExporter.MovedFrame, 0x00, 0x01,
                0x80,   // Y, que va primero como en la tabla de atributos
                0xFF,   // X
                0x00,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
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
                SpriteAnimationExporter.Paint, Fresh,
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
                SpriteAnimationExporter.Paint, Fresh,
                SpriteAnimationExporter.Frame, 0x01, 0x01,
                SpriteAnimationExporter.LoopEnd,
                SpriteAnimationExporter.LoopEnd,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>
    /// El color se fija una vez y no en cada fotograma.
    /// </summary>
    /// <remarks>
    /// Es la razón de que sea un paso aparte: un byte por fotograma se paga siempre, y esto
    /// sólo cuando cambia. Una animación entera del mismo color son dos bytes.
    /// </remarks>
    [AvaloniaFact]
    public void El_color_se_fija_una_vez_y_no_en_cada_fotograma()
    {
        SpriteBank bank = Painted([1, 1, 7]);
        var animation = new SpriteAnimation("Andar");

        animation.Steps.Add(new AnimationFrame { Target = 0, Wait = 1 });
        animation.Steps.Add(new AnimationFrame { Target = 1, Wait = 1 });
        animation.Steps.Add(new AnimationFrame { Target = 2, Wait = 1 });

        bank.Animations.Add(animation);

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, 0x01,
                SpriteAnimationExporter.Frame, 0x00, 0x01,
                SpriteAnimationExporter.Frame, 0x01, 0x01,   // el mismo color: no se repite
                SpriteAnimationExporter.Paint, 0x07,
                SpriteAnimationExporter.Frame, 0x02, 0x01,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>
    /// Un bucle fija su color al entrar.
    /// </summary>
    /// <remarks>
    /// Dentro de un bucle no vale con mirar lo que había antes: se entra más de una vez, y de
    /// la segunda en adelante se entra con el color que dejó la vuelta anterior. Sin fijarlo,
    /// un bucle que cambia de color sale bien la primera vuelta y mal todas las demás.
    /// </remarks>
    [AvaloniaFact]
    public void Un_bucle_fija_su_color_al_entrar()
    {
        SpriteBank bank = Painted([1, 8]);
        var animation = new SpriteAnimation("Andar");
        var loop = new AnimationLoop { Times = 3 };

        loop.Steps.Add(new AnimationFrame { Target = 0, Wait = 1 });
        loop.Steps.Add(new AnimationFrame { Target = 1, Wait = 1 });

        animation.Steps.Add(new AnimationFrame { Target = 0, Wait = 1 });
        animation.Steps.Add(loop);

        bank.Animations.Add(animation);

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, 0x01,
                SpriteAnimationExporter.Frame, 0x00, 0x01,
                SpriteAnimationExporter.LoopStart, 0x03,
                SpriteAnimationExporter.Paint, 0x01,   // otra vez: la vuelta anterior deja el 8
                SpriteAnimationExporter.Frame, 0x00, 0x01,
                SpriteAnimationExporter.Paint, 0x08,
                SpriteAnimationExporter.Frame, 0x01, 0x01,
                SpriteAnimationExporter.LoopEnd,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>
    /// Cada animación fija su color desde cero.
    /// </summary>
    /// <remarks>
    /// El juego no lee la tira de corrido: salta a la animación que le pidan. Si una diera por
    /// sabido el color de la anterior, se vería del color de la que se enseñó antes.
    /// </remarks>
    [AvaloniaFact]
    public void Cada_animacion_fija_su_color_desde_cero()
    {
        SpriteBank bank = Painted([4]);

        foreach (string name in (string[])["Andar", "Saltar"])
        {
            var animation = new SpriteAnimation(name);

            animation.Steps.Add(new AnimationFrame { Target = 0, Wait = 1 });

            bank.Animations.Add(animation);
        }

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, 0x04,
                SpriteAnimationExporter.Frame, 0x00, 0x01,
                SpriteAnimationExporter.End,
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, 0x04,   // otra vez, que a esta se puede saltar
                SpriteAnimationExporter.Frame, 0x00, 0x01,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>
    /// Una animación de grupos no lleva color.
    /// </summary>
    /// <remarks>
    /// El grupo ya exporta el color de cada uno de sus sprites. Decirlo otra vez aquí sería la
    /// misma cosa en dos sitios, con dos respuestas posibles y ninguna manera de saber cuál
    /// manda.
    /// </remarks>
    [AvaloniaFact]
    public void Una_animacion_de_grupos_no_lleva_color()
    {
        SpriteBank bank = WithGroups(2);
        var animation = new SpriteAnimation("Andar", AnimationKind.Groups);

        animation.Steps.Add(new AnimationFrame { Target = 0, Wait = 1 });

        bank.Animations.Add(animation);

        Assert.DoesNotContain(
            SpriteAnimationExporter.Paint, SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>
    /// De un patrón de varios colores sale el de su primera línea.
    /// </summary>
    /// <remarks>
    /// En MSX2 un patrón puede llevar 16 colores, uno por línea, y en un byte no caben. El
    /// color por línea se queda en los grupos, que es donde se edita; aquí se exporta el
    /// primero y el juego pinta con él las 16.
    /// </remarks>
    [AvaloniaFact]
    public void De_un_patron_de_varios_colores_sale_el_de_la_primera_linea()
    {
        SpriteBank bank = Painted([6]);
        var animation = new SpriteAnimation("Andar");

        bank.SpritesList[0].ArraySpriteRows[9].Color = 11;

        animation.Steps.Add(new AnimationFrame { Target = 0, Wait = 1 });

        bank.Animations.Add(animation);

        Assert.Equal(
            [
                SpriteAnimationExporter.OfPatterns, 0x00,
                SpriteAnimationExporter.Paint, 0x06,
                SpriteAnimationExporter.Frame, 0x00, 0x01,
                SpriteAnimationExporter.End,
            ],
            SpriteAnimationExporter.ToBinary(bank));
    }

    /// <summary>Un banco con los primeros patrones de los colores que se le digan.</summary>
    private static SpriteBank Painted(int[] colors)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        for (int at = 0; at < colors.Length; at++)
        {
            foreach (SpriteRow row in bank.SpritesList[at].ArraySpriteRows)
                row.Color = colors[at];
        }

        return bank;
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

    /// <summary>
    /// El color de un patrón sin tocar, que es con el que nace cada línea.
    /// </summary>
    /// <remarks>
    /// Sale en la tira aunque nadie haya elegido nada: es el color que enseña la vista previa,
    /// y no exportarlo era justamente el fallo.
    /// </remarks>
    private const byte Fresh = 15;

    private static SpriteBank WithGroups(int many)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        for (int each = 0; each < many; each++)
            bank.NewGroup(each);

        return bank;
    }
}
