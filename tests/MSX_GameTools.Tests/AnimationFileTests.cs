using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Las animaciones dentro del fichero del banco.
/// </summary>
/// <remarks>
/// En el fichero del banco y no en uno suyo, por lo mismo que están colgadas de él: sus pasos
/// apuntan a patrones y a grupos suyos, así que separarlas dejaría dos ficheros que sólo valen
/// juntos y que se pueden desparejar.
/// </remarks>
public class AnimationFileTests
{
    /// <summary>Una animación con bucles anidados va y vuelve igual.</summary>
    [AvaloniaFact]
    public void Una_animacion_va_y_vuelve_igual()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        var animation = new SpriteAnimation("Andar", AnimationKind.Groups)
        {
            Mode = AnimationMode.PingPong,
        };

        animation.Steps.Add(new AnimationFrame
        {
            Target = 3, Wait = 7, OffsetX = -5, OffsetY = 9, Event = 2,
        });

        var loop = new AnimationLoop { Times = 4 };
        loop.Steps.Add(new AnimationFrame { Target = 8, Wait = 2 });
        animation.Steps.Add(loop);

        bank.Animations.Add(animation);

        SpriteBank back = Roundtrip(bank);

        SpriteAnimation read = Assert.Single(back.Animations);

        Assert.Equal("Andar", read.Name);
        Assert.Equal(AnimationKind.Groups, read.Kind);
        Assert.Equal(AnimationMode.PingPong, read.Mode);

        var frame = Assert.IsType<AnimationFrame>(read.Steps[0]);

        Assert.Equal(3, frame.Target);
        Assert.Equal(7, frame.Wait);
        Assert.Equal(-5, frame.OffsetX);
        Assert.Equal(9, frame.OffsetY);
        Assert.Equal(2, frame.Event);

        var readLoop = Assert.IsType<AnimationLoop>(read.Steps[1]);

        Assert.Equal(4, readLoop.Times);
        Assert.Equal(8, Assert.IsType<AnimationFrame>(Assert.Single(readLoop.Steps)).Target);
    }

    /// <summary>
    /// Y lo que sale de leerla se resuelve igual que lo que se guardó.
    /// </summary>
    /// <remarks>
    /// Comprobar campo a campo no basta: lo que importa de una animación es lo que se ve, y eso
    /// es la línea de tiempo. Un bucle que se leyera con una vuelta de menos pasaría todas las
    /// comprobaciones de arriba.
    /// </remarks>
    [AvaloniaFact]
    public void Lo_que_se_lee_se_ve_igual_que_lo_que_se_guardo()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        var animation = new SpriteAnimation("Andar") { Mode = AnimationMode.PingPong };

        var loop = new AnimationLoop { Times = 3 };
        loop.Steps.Add(new AnimationFrame { Target = 1 });
        loop.Steps.Add(new AnimationFrame { Target = 2, OffsetX = 4 });

        animation.Steps.Add(loop);
        animation.Steps.Add(new AnimationFrame { Target = 5, Wait = 9 });

        bank.Animations.Add(animation);

        AnimationTimeline before = AnimationResolver.Of(animation);
        AnimationTimeline after = AnimationResolver.Of(Roundtrip(bank).Animations[0]);

        Assert.Equal(before.Frames, after.Frames);
        Assert.Equal(before.Ticks, after.Ticks);
    }

    /// <summary>Un banco sin animaciones se lee sin ninguna, no con una vacía.</summary>
    [AvaloniaFact]
    public void Un_banco_sin_animaciones_se_lee_sin_ninguna()
    {
        Assert.Empty(Roundtrip(new SpriteBank(SpriteBank.SpriteType.MSX, "Pelado")).Animations);
    }

    /// <summary>
    /// Un fichero anterior a las animaciones se abre sin tocar nada.
    /// </summary>
    /// <remarks>
    /// Es el motivo de subir la versión del formato en vez de colar el campo por la cara: los
    /// bancos que ya existen no traen la lista, y no traerla no puede ser un error.
    /// </remarks>
    [AvaloniaFact]
    public void Un_fichero_de_antes_se_abre_igual()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX, "Viejo");
        bank.SpritesList[2].ArraySpriteRows[0].ArrayColumns[0] = true;

        string json = SpriteBankSerializer.Serialize(bank, ColorPalette.CreateMsxStandard(), 1, []);

        // Como lo escribia la version 3: sin la lista de animaciones.
        json = json.Replace("\"animations\": [],", string.Empty).Replace("\"animations\":[],", string.Empty);

        SpriteBank back = SpriteBankSerializer.Deserialize(json).Bank;

        Assert.Empty(back.Animations);
        Assert.True(back.SpritesList[2].ArraySpriteRows[0].ArrayColumns[0]);
    }

    /// <summary>Un paso de un tipo que no existe se dice, no se ignora.</summary>
    /// <remarks>
    /// Ignorarlo dejaría la animación con menos pasos de los que tenía y sin nada que lo diga,
    /// que es la peor forma de perder trabajo: la de la que no te enteras.
    /// </remarks>
    [AvaloniaFact]
    public void Un_paso_de_un_tipo_que_no_existe_se_dice()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX, "Bichos");
        var animation = new SpriteAnimation("Andar");

        animation.Steps.Add(new AnimationFrame { Target = 1 });
        bank.Animations.Add(animation);

        string json = SpriteBankSerializer
            .Serialize(bank, ColorPalette.CreateMsxStandard(), 1, [])
            .Replace("\"frame\"", "\"salto\"");

        FileFormatException failed = Assert.Throws<FileFormatException>(
            () => SpriteBankSerializer.Deserialize(json));

        Assert.Contains("salto", failed.Message);
    }

    private static SpriteBank Roundtrip(SpriteBank bank) => SpriteBankSerializer
        .Deserialize(SpriteBankSerializer.Serialize(bank, ColorPalette.CreateMsxStandard(), 1, []))
        .Bank;
}
