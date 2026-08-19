using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El número de un grupo, que no se mueve al borrar otro.
/// </summary>
/// <remarks>
/// <para>
/// Los grupos se exportan en orden y el juego los direcciona por su posición, así que borrar uno
/// corre todos los de atrás. Mientras nadie apuntara a un grupo eso no se notaba; con las
/// animaciones apuntando, una que fuera al 3 pasaba a ir a otro sin que nada lo dijera.
/// </para>
/// <para>
/// El banco ya había aprendido esto con los patrones: sus huecos son fijos precisamente para que
/// ningún número se pueda mover. Los grupos no podían serlo —un grupo vacío no significa nada—
/// así que llevan un número propio que no es su posición.
/// </para>
/// </remarks>
public class GroupIdentityTests
{
    /// <summary>Cada grupo nace con su número, y no se repiten.</summary>
    [AvaloniaFact]
    public void Cada_grupo_nace_con_su_numero()
    {
        SpriteBank bank = WithGroups(3);

        Assert.Equal([0, 1, 2], bank.Groups.Select(group => group.Id));
    }

    /// <summary>
    /// Borrar uno no mueve el número de los demás, aunque sí su posición.
    /// </summary>
    /// <remarks>
    /// Es todo el asunto: la posición se mueve porque el juego cuenta grupos, y el número no se
    /// mueve porque las animaciones apuntan a él.
    /// </remarks>
    [AvaloniaFact]
    public void Borrar_uno_no_mueve_el_numero_de_los_demas()
    {
        SpriteBank bank = WithGroups(3);

        bank.Groups.RemoveAt(1);

        Assert.Equal([0, 2], bank.Groups.Select(group => group.Id));

        // La posición sí se ha movido, que es lo que ve el juego.
        Assert.Equal(1, bank.OrdinalOfGroup(2));
        Assert.Equal(0, bank.OrdinalOfGroup(0));
    }

    /// <summary>Y un número que ya no está se dice, no se confunde con el primero.</summary>
    [AvaloniaFact]
    public void Un_grupo_que_ya_no_esta_se_dice()
    {
        SpriteBank bank = WithGroups(2);

        bank.Groups.RemoveAt(0);

        Assert.Equal(-1, bank.OrdinalOfGroup(0));
    }

    /// <summary>Los números de los borrados no se reaprovechan.</summary>
    /// <remarks>
    /// Reaprovecharlos sería peor que correr las posiciones: una animación que apuntara al grupo
    /// borrado empezaría a apuntar a uno nuevo que no tiene nada que ver, y encima parecería
    /// correcta.
    /// </remarks>
    [AvaloniaFact]
    public void Los_numeros_de_los_borrados_no_se_reaprovechan()
    {
        SpriteBank bank = WithGroups(2);

        bank.Groups.RemoveAt(1);
        bank.NewGroup(0);

        Assert.Equal([0, 2], bank.Groups.Select(group => group.Id));
    }

    /// <summary>Se sabe qué animaciones usan un grupo, para poder avisar antes de borrarlo.</summary>
    [AvaloniaFact]
    public void Se_sabe_que_animaciones_usan_un_grupo()
    {
        SpriteBank bank = WithGroups(3);

        var animation = new SpriteAnimation("Andar", AnimationKind.Groups);
        var loop = new AnimationLoop { Times = 2 };

        loop.Steps.Add(new AnimationFrame { Target = 2 });
        animation.Steps.Add(loop);

        // Y una de patrones que casualmente usa el mismo número, que no cuenta.
        var patterns = new SpriteAnimation("Parpadeo", AnimationKind.Patterns);
        patterns.Steps.Add(new AnimationFrame { Target = 2 });

        bank.Animations.Add(animation);
        bank.Animations.Add(patterns);

        Assert.Same(animation, Assert.Single(bank.AnimationsUsing(2)));
        Assert.Empty(bank.AnimationsUsing(1));
    }

    // ------------------------------------------------------------------ el fichero

    /// <summary>El número va y vuelve, y sigue por donde iba al crear otro.</summary>
    [AvaloniaFact]
    public void El_numero_va_y_vuelve_en_el_fichero()
    {
        SpriteBank bank = WithGroups(3);
        bank.Groups.RemoveAt(0);

        SpriteBank back = Roundtrip(bank);

        Assert.Equal([1, 2], back.Groups.Select(group => group.Id));

        // Y el siguiente que se cree no repite ninguno de los que hay.
        back.NewGroup(0);

        Assert.Equal([1, 2, 3], back.Groups.Select(group => group.Id));
    }

    /// <summary>
    /// Un fichero sin números se abre dándole a cada grupo su posición.
    /// </summary>
    /// <remarks>
    /// Allí la posición era la identidad, así que dársela deja el banco apuntando exactamente a
    /// donde apuntaba. Cualquier otra cosa —empezar de cero, o dejarlos todos a cero— cambiaría
    /// referencias que nadie ha tocado.
    /// </remarks>
    [AvaloniaFact]
    public void Un_fichero_sin_numeros_toma_las_posiciones()
    {
        string json = SpriteBankSerializer.Serialize(
            WithGroups(3), ColorPalette.CreateMsxStandard(), 1, []);

        // Como lo escribia la version de antes: sin el numero de grupo.
        json = System.Text.RegularExpressions.Regex.Replace(json, @",?\s*""id"":\s*\d+", string.Empty);

        Assert.DoesNotContain("\"id\"", json);

        SpriteBank back = SpriteBankSerializer.Deserialize(json).Bank;

        Assert.Equal([0, 1, 2], back.Groups.Select(group => group.Id));
    }

    // ------------------------------------------------------------------ los andamios

    private static SpriteBank WithGroups(int count)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        for (int index = 0; index < count; index++)
            bank.NewGroup(0);

        return bank;
    }

    private static SpriteBank Roundtrip(SpriteBank bank) => SpriteBankSerializer
        .Deserialize(SpriteBankSerializer.Serialize(bank, ColorPalette.CreateMsxStandard(), 1, []))
        .Bank;
}
