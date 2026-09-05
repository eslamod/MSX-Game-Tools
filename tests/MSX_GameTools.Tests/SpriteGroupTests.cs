using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Grupos: composición de varios sprites con desplazamiento, con el orden como
/// prioridad, tal y como funcionan los planos del VDP.
/// </summary>
public class SpriteGroupTests
{
    /// <summary>El lienzo de un grupo corriente y dónde cae en él el desplazamiento cero.</summary>
    /// <remarks>
    /// No son constantes del renderizador: el lienzo sale de lo que ocupa cada grupo. Estos son
    /// los de un grupo de un sprite sin desplazar, que es el de casi todas estas pruebas.
    /// </remarks>
    private const int Size = SpriteGroupRenderer.NominalSize;

    /// <summary>Sin planos sacados del grupo, el desplazamiento cero cae en la esquina.</summary>
    private const int Origin = 0;

    /// <summary>
    /// El lienzo de un grupo sale de lo que el grupo ocupa, con un sprite de margen.
    /// </summary>
    /// <remarks>
    /// Antes era fijo, atado al desplazamiento máximo, y con eso una figura de dos sprites de
    /// alto no cabía. Redondeado a sprites enteros para que mover una flecha no cambie el
    /// tamaño de la miniatura y reordene el panel.
    /// </remarks>
    [AvaloniaFact]
    public void El_lienzo_sale_de_lo_que_ocupa_el_grupo()
    {
        var bank = new SpriteBank();
        SpriteGroup group = bank.NewGroup(0)!;

        // Uno solo sin desplazar: justo el sprite, sin margen que no enseña nada.
        Assert.Equal((16, 16, 0, 0), SpriteGroupRenderer.CanvasOf(group));

        // Con otro un sprite más abajo, el lienzo crece a lo alto y no a lo ancho.
        group.Add(new SpriteGroupMember(1, bank.SpritesList[1]) { OffsetY = 16 });

        Assert.Equal((16, 32, 0, 0), SpriteGroupRenderer.CanvasOf(group));

        // Y sacando uno hacia arriba y a la izquierda, el origen se corre con él.
        group.Add(new SpriteGroupMember(2, bank.SpritesList[2]) { OffsetX = -3, OffsetY = -5 });

        Assert.Equal((19, 37, 3, 5), SpriteGroupRenderer.CanvasOf(group));
    }

    [AvaloniaFact]
    public void Un_grupo_nuevo_arranca_con_el_patron_indicado()
    {
        var bank = new SpriteBank();

        SpriteGroup? group = bank.NewGroup(1);

        Assert.NotNull(group);
        Assert.Single(group.Members);
        Assert.Equal(1, group.Members[0].PatternIndex);
        Assert.Equal("Group 1", group.Name);
    }

    [AvaloniaFact]
    public void Los_colores_del_miembro_se_siembran_del_patron_pero_son_suyos()
    {
        var bank = new SpriteBank();
        bank.SpritesList[0].ArraySpriteRows[4].Color = 6;

        SpriteGroup group = bank.NewGroup(0)!;
        SpriteGroupMember member = group.Members[0];

        Assert.Equal(6, member.Rows[4].Color);

        // Editar el patron ya no toca al miembro: es lo que permite el mismo dibujo
        // con dos colores distintos.
        bank.SpritesList[0].ArraySpriteRows[4].Color = 2;

        Assert.Equal(6, member.Rows[4].Color);
    }

    /// <summary>
    /// El tope son treinta y dos planos, los que caben en la tabla de atributos.
    /// </summary>
    /// <remarks>
    /// Con el número escrito a mano y no con la constante: es lo único que distingue subir el
    /// tope a propósito de subirlo sin querer. Las demás comprobaciones usan la constante y
    /// pasarían con cualquier valor.
    /// </remarks>
    [Fact]
    public void El_tope_de_planos_de_un_grupo_es_treinta_y_dos()
    {
        Assert.Equal(32, SpriteGroup.MaxMembers);
    }

    [AvaloniaFact]
    public void Un_grupo_no_pasa_de_su_tope_de_miembros_ni_baja_de_uno()
    {
        var bank = new SpriteBank();
        SpriteGroup group = bank.NewGroup(0)!;

        for (int i = 1; i < SpriteGroup.MaxMembers; i++)
            Assert.True(group.Add(new SpriteGroupMember(0, bank.SpritesList[0])));

        Assert.False(group.CanAddMember);
        Assert.False(group.Add(new SpriteGroupMember(0, bank.SpritesList[0])));
        Assert.Equal(SpriteGroup.MaxMembers, group.Members.Count);

        while (group.CanRemoveMember)
            Assert.True(group.Remove(group.Members[^1]));

        Assert.Single(group.Members);
        Assert.False(group.Remove(group.Members[0]));
    }

    [AvaloniaFact]
    public void El_banco_no_pasa_de_32_grupos()
    {
        var bank = new SpriteBank();

        for (int i = 0; i < SpriteBank.MaxGroups; i++)
            Assert.NotNull(bank.NewGroup(0));

        Assert.False(bank.CanAddGroup);
        Assert.Null(bank.NewGroup(0));
        Assert.Equal(SpriteBank.MaxGroups, bank.Groups.Count);
    }

    [AvaloniaFact]
    public void Los_desplazamientos_se_limitan_a_tres_sprites()
    {
        var bank = new SpriteBank();
        SpriteGroupMember member = bank.NewGroup(0)!.Members[0];

        member.OffsetX = 100;
        member.OffsetY = -100;

        Assert.Equal(SpriteGroupMember.MaxOffset, member.OffsetX);
        Assert.Equal(SpriteGroupMember.MinOffset, member.OffsetY);
    }

    [AvaloniaFact]
    public void Reordenar_cambia_la_prioridad_y_respeta_los_extremos()
    {
        var bank = new SpriteBank();
        SpriteGroup group = bank.NewGroup(0)!;

        var second = new SpriteGroupMember(0, bank.SpritesList[0]);
        group.Add(second);

        Assert.Equal(0, group.MoveUp(second));
        Assert.Same(second, group.Members[0]);

        Assert.Equal(0, group.MoveUp(second)); // ya es el primero
        Assert.Equal(1, group.MoveDown(second));
    }

    [AvaloniaFact]
    public void El_grupo_avisa_cuando_cambia_algo_que_se_ve()
    {
        var bank = new SpriteBank();
        SpriteGroup group = bank.NewGroup(0)!;

        int notifications = 0;
        group.Changed += _ => notifications++;

        group.Members[0].OffsetX = 3;
        group.Add(new SpriteGroupMember(0, bank.SpritesList[0]));
        group.Members[1].PatternIndex = 0; // mismo valor: no avisa

        Assert.Equal(2, notifications);
    }

    [AvaloniaFact]
    public void La_composicion_coloca_cada_miembro_en_su_desplazamiento()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color background = palette.GetColor(1);

        // Un unico pixel encendido en la esquina del patron.
        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;
        bank.SpritesList[0].ArraySpriteRows[0].Color = 8;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].OffsetX = -15;
        group.Members[0].OffsetY = 15;

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        int[] pixels = PixelReader.Read(preview);

        // El origen de este grupo no es el de siempre: sacar un plano quince pixeles a la
        // izquierda ensancha el lienzo por ese lado.
        (int width, _, int originX, int originY) = SpriteGroupRenderer.CanvasOf(group);

        Assert.Equal(
            PixelReader.Bgra(palette.GetColor(8)),
            pixels[((originY + 15) * width) + originX - 15]);
        Assert.Equal(1, pixels.Count(p => p == PixelReader.Bgra(palette.GetColor(8))));
    }

    [AvaloniaFact]
    public void Sin_CC_gana_el_miembro_de_mayor_prioridad()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 8;

        var behind = new SpriteGroupMember(0, bank.SpritesList[0]);
        behind.Rows[0].Color = 2;
        group.Add(behind);

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, palette.GetColor(1), preview);

        // El primero de la lista es el de mayor prioridad.
        Assert.Equal(PixelReader.Bgra(palette.GetColor(8)), PixelReader.At(preview, Origin, Origin));
    }

    [AvaloniaFact]
    public void Con_CC_el_color_se_combina_con_or_de_los_indices()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 1; // 0001

        var behind = new SpriteGroupMember(0, bank.SpritesList[0]);
        behind.Rows[0].Color = 4; // 0100
        behind.Rows[0].CombineColor = true;
        group.Add(behind);

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, palette.GetColor(1), preview);

        // 0001 OR 0100 = 0101 = 5. El V9938 combina los codigos de color, no el RGB.
        Assert.Equal(PixelReader.Bgra(palette.GetColor(5)), PixelReader.At(preview, Origin, Origin));
    }

    // Reglas del bit CC, apartado 5.2.5 del manual del V9938.

    [AvaloniaFact]
    public void Una_linea_con_CC_no_se_dibuja_si_no_hay_nadie_con_CC_a_0_por_delante()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color background = palette.GetColor(1);

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 8;
        group.Members[0].Rows[0].CombineColor = true; // el primero: nadie por delante

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        // La maquina no lo dibujaria, asi que el editor tampoco.
        Assert.Equal(PixelReader.Bgra(background), PixelReader.At(preview, Origin, Origin));
    }

    [AvaloniaFact]
    public void Un_sprite_de_menor_prioridad_con_CC_a_0_no_habilita_al_de_arriba()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color background = palette.GetColor(1);

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 8;
        group.Members[0].Rows[0].CombineColor = true;

        var behind = new SpriteGroupMember(0, bank.SpritesList[0]);
        behind.Rows[0].Color = 4; // CC a 0, pero va detras
        group.Add(behind);

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        // Solo habilitan los de numero menor: el de CC sigue sin dibujarse y se ve
        // unicamente el de detras.
        Assert.Equal(PixelReader.Bgra(palette.GetColor(4)), PixelReader.At(preview, Origin, Origin));
    }

    [AvaloniaFact]
    public void La_condicion_de_CC_es_por_linea_de_pantalla_y_no_por_pixel()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color background = palette.GetColor(1);

        // Cuatro pixeles en la fila 0 del patron.
        for (int column = 0; column < 4; column++)
            bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[column] = true;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 8; // CC a 0, columnas 0-3

        var combined = new SpriteGroupMember(0, bank.SpritesList[0]) { OffsetX = 6 };
        combined.Rows[0].Color = 4;
        combined.Rows[0].CombineColor = true; // columnas 6-9, sin solapar con el de abajo
        group.Add(combined);

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        // Comparten linea de pantalla aunque no se toquen, asi que se dibuja igual.
        Assert.Equal(PixelReader.Bgra(palette.GetColor(4)), PixelReader.At(preview, Origin + 6, Origin));
    }

    [AvaloniaFact]
    public void El_ejemplo_de_siete_colores_del_manual()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        for (int column = 0; column < 4; column++)
            bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[column] = true;

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 8; // 1000, CC a 0, columnas 0-3

        var second = new SpriteGroupMember(0, bank.SpritesList[0]) { OffsetX = 2 };
        second.Rows[0].Color = 4; // 0100, columnas 2-5
        second.Rows[0].CombineColor = true;
        group.Add(second);

        var third = new SpriteGroupMember(0, bank.SpritesList[0]) { OffsetX = 3 };
        third.Rows[0].Color = 2; // 0010, columnas 3-6
        third.Rows[0].CombineColor = true;
        group.Add(third);

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, palette.GetColor(1), preview);

        // Los codigos se acumulan con OR segun se solapan.
        int[] expected = [8, 8, 12, 14, 6, 6, 2];

        for (int column = 0; column < expected.Length; column++)
        {
            Assert.Equal(
                PixelReader.Bgra(palette.GetColor(expected[column])),
                PixelReader.At(preview, Origin + column, Origin));
        }
    }

    [AvaloniaFact]
    public void Donde_no_pinta_nadie_se_ve_el_fondo()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color background = palette.GetColor(4);

        SpriteGroup group = bank.NewGroup(0)!;

        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        Assert.All(PixelReader.Read(preview), pixel => Assert.Equal(PixelReader.Bgra(background), pixel));
    }

    // ------------------------------------------------------------------ ocho planos

    /// <summary>Un grupo lleno sobrevive a guardar y volver a abrir.</summary>
    /// <remarks>
    /// El fichero valida cuántos sprites trae cada grupo, así que subir el tope sin que la
    /// validación lo siguiera habría dejado ilegibles justo los grupos nuevos: se guardarían
    /// bien y reventarían al abrirlos.
    /// </remarks>
    [AvaloniaFact]
    public void Un_grupo_de_ocho_va_y_vuelve_del_fichero()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        SpriteGroup group = Full(bank);

        LoadedSpriteBank loaded = SpriteBankSerializer.Deserialize(
            SpriteBankSerializer.Serialize(bank, ColorPalette.CreateMsxStandard(), 1, []));

        Assert.Single(loaded.Bank.Groups);
        Assert.Equal(SpriteGroup.MaxMembers, loaded.Bank.Groups[0].Members.Count);

        // Y cada plano sigue apuntando a su patrón, que es el orden de prioridad.
        for (int member = 0; member < SpriteGroup.MaxMembers; member++)
        {
            Assert.Equal(
                group.Members[member].PatternIndex,
                loaded.Bank.Groups[0].Members[member].PatternIndex);
        }
    }

    /// <summary>Y se exporta entero: el byte de la cuenta dice ocho y van los ocho detrás.</summary>
    /// <remarks>
    /// El byte de la cuenta es lo único que deja recorrer el fichero, así que si dijera cuatro
    /// el juego leería cuatro planos y tomaría los otros cuatro por el grupo siguiente.
    /// </remarks>
    [AvaloniaFact]
    public void Un_grupo_de_ocho_se_exporta_entero()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX, "Bichos");
        Full(bank);

        byte[] bytes = SpriteBankExporter.GroupsToBinary(bank);

        // En MSX1 cada plano son cuatro bytes: desplazamiento Y, X, patrón y color.
        const int BytesPerMember = 4;

        Assert.Equal(SpriteGroup.MaxMembers, bytes[0]);
        Assert.Equal(1 + (SpriteGroup.MaxMembers * BytesPerMember), bytes.Length);
    }

    /// <summary>Un grupo con el tope de planos, cada uno sobre un patrón distinto.</summary>
    private static SpriteGroup Full(SpriteBank bank)
    {
        SpriteGroup group = bank.NewGroup(0)!;

        for (int member = 1; member < SpriteGroup.MaxMembers; member++)
            group.Add(new SpriteGroupMember(member, bank.SpritesList[member]));

        return group;
    }
}
