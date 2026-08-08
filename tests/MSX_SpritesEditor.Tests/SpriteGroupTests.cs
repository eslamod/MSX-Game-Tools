using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_SpritesEditor.Entities;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Grupos: composición de varios sprites con desplazamiento, con el orden como
/// prioridad, tal y como funcionan los planos del VDP.
/// </summary>
public class SpriteGroupTests
{
    private const int Size = SpriteGroupRenderer.PreviewSize;
    private const int Origin = SpriteGroupMember.MaxOffset;

    [Fact]
    public void El_lienzo_del_grupo_cubre_el_sprite_mas_el_desplazamiento_maximo()
    {
        // 16 del sprite mas 15 a cada lado.
        Assert.Equal(46, SpriteGroupRenderer.PreviewSize);
    }

    [Fact]
    public void Un_grupo_nuevo_arranca_con_el_patron_indicado()
    {
        var bank = new SpriteBank();
        bank.NewSprite();

        SpriteGroup? group = bank.NewGroup(1);

        Assert.NotNull(group);
        Assert.Single(group.Members);
        Assert.Equal(1, group.Members[0].PatternIndex);
        Assert.Equal("Group 1", group.Name);
    }

    [Fact]
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

    [Fact]
    public void Un_grupo_no_pasa_de_cuatro_miembros_ni_baja_de_uno()
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

    [Fact]
    public void El_banco_no_pasa_de_32_grupos()
    {
        var bank = new SpriteBank();

        for (int i = 0; i < SpriteBank.MaxGroups; i++)
            Assert.NotNull(bank.NewGroup(0));

        Assert.False(bank.CanAddGroup);
        Assert.Null(bank.NewGroup(0));
        Assert.Equal(SpriteBank.MaxGroups, bank.Groups.Count);
    }

    [Fact]
    public void Los_desplazamientos_se_limitan_a_mas_menos_15()
    {
        var bank = new SpriteBank();
        SpriteGroupMember member = bank.NewGroup(0)!.Members[0];

        member.OffsetX = 40;
        member.OffsetY = -40;

        Assert.Equal(SpriteGroupMember.MaxOffset, member.OffsetX);
        Assert.Equal(SpriteGroupMember.MinOffset, member.OffsetY);
    }

    [Fact]
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

    [Fact]
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

        ImageMini preview = SpriteGroupRenderer.CreatePreview();
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        int[] pixels = PixelReader.Read(preview);

        Assert.Equal(PixelReader.Bgra(palette.GetColor(8)), pixels[((Origin + 15) * Size) + Origin - 15]);
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

        ImageMini preview = SpriteGroupRenderer.CreatePreview();
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

        ImageMini preview = SpriteGroupRenderer.CreatePreview();
        SpriteGroupRenderer.Render(group, bank, palette, palette.GetColor(1), preview);

        // 0001 OR 0100 = 0101 = 5. El V9938 combina los codigos de color, no el RGB.
        Assert.Equal(PixelReader.Bgra(palette.GetColor(5)), PixelReader.At(preview, Origin, Origin));
    }

    [AvaloniaFact]
    public void Donde_no_pinta_nadie_se_ve_el_fondo()
    {
        var bank = new SpriteBank();
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color background = palette.GetColor(4);

        SpriteGroup group = bank.NewGroup(0)!;

        ImageMini preview = SpriteGroupRenderer.CreatePreview();
        SpriteGroupRenderer.Render(group, bank, palette, background, preview);

        Assert.All(PixelReader.Read(preview), pixel => Assert.Equal(PixelReader.Bgra(background), pixel));
    }
}
