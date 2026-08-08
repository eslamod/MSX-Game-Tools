using Avalonia.Headless.XUnit;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Colores de frente: uno por sprite en MSX1, uno por línea en MSX2, y el color de
/// fondo común sobre el que se previsualiza el banco.
/// </summary>
public class SpriteColorTests
{
    [Fact]
    public void Un_banco_msx1_no_tiene_color_por_linea()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX);

        Assert.True(vm.IsMsx1);
        Assert.False(vm.IsMsx2);
    }

    [Fact]
    public void Un_banco_msx2_tiene_una_casilla_por_linea()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);

        Assert.True(vm.IsMsx2);
        Assert.Equal(Sprite.Rows, vm.RowColors.Count);

        for (int row = 0; row < Sprite.Rows; row++)
            Assert.Equal(row, vm.RowColors[row].RowIndex);
    }

    [Fact]
    public void En_msx2_elegir_un_color_afecta_solo_a_esa_linea()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);

        vm.RowColors[3].PickCommand.Execute(vm.ColorPalette[6]);

        Assert.Equal(6, vm.CurrentSprite.ArraySpriteRows[3].Color);
        Assert.Equal("6", vm.RowColors[3].Color.Hex);

        for (int row = 0; row < Sprite.Rows; row++)
        {
            if (row != 3)
                Assert.Equal(15, vm.CurrentSprite.ArraySpriteRows[row].Color);
        }
    }

    [Fact]
    public void En_msx1_el_color_elegido_se_aplica_a_las_16_lineas()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX);

        vm.PickSpriteColorCommand.Execute(vm.ColorPalette[10]);

        Assert.All(vm.CurrentSprite.ArraySpriteRows, row => Assert.Equal(10, row.Color));
        Assert.Equal("A", vm.SpriteColor.Hex);
        Assert.Equal("Dark yellow", vm.SpriteColor.Name);
    }

    [Fact]
    public void Las_casillas_se_repuntan_al_cambiar_de_sprite()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);
        vm.RowColors[3].PickCommand.Execute(vm.ColorPalette[6]);

        vm.AddSpriteCommand.Execute(null); // el nuevo queda seleccionado

        Assert.Equal(15, vm.RowColors[3].Color.Index);

        vm.PreviousSpriteCommand.Execute(null);

        Assert.Equal(6, vm.RowColors[3].Color.Index);
    }

    [Fact]
    public void El_fondo_arranca_en_negro_y_no_ofrece_el_transparente()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);

        Assert.Equal(1, vm.BackgroundColor.Index);
        Assert.Equal("Black", vm.BackgroundColor.Name);
        Assert.DoesNotContain(vm.BackgroundChoices, c => c.IsTransparent);
    }

    [AvaloniaFact]
    public void Cambiar_el_color_de_una_linea_repinta_su_miniatura()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);
        vm.CurrentSprite.ArraySpriteRows[2].ArrayColumns[5] = true;

        vm.RowColors[2].PickCommand.Execute(vm.ColorPalette[8]);

        ImageMini mini = vm.CurrentSprite.ImageMini!;
        Assert.Equal(PixelReader.Bgra(vm.ColorPalette[8].Color), PixelReader.At(mini, 5, 2));

        // Un pixel apagado de la misma línea sigue en el color de fondo.
        Assert.Equal(PixelReader.Bgra(vm.BackgroundColor.Color), PixelReader.At(mini, 6, 2));
    }

    [AvaloniaFact]
    public void El_color_0_se_dibuja_con_el_color_de_fondo()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);
        vm.CurrentSprite.ArraySpriteRows[0].ArrayColumns[0] = true;

        vm.PickBackgroundColorCommand.Execute(vm.ColorPalette[4]); // azul oscuro
        vm.RowColors[0].PickCommand.Execute(vm.ColorPalette[0]);   // transparente

        // En la máquina real ese pixel deja ver el fondo, así que aquí también.
        ImageMini mini = vm.CurrentSprite.ImageMini!;
        Assert.Equal(PixelReader.Bgra(vm.ColorPalette[4].Color), PixelReader.At(mini, 0, 0));
    }

    [AvaloniaFact]
    public void Cambiar_el_fondo_repinta_todas_las_miniaturas_del_banco()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);
        vm.AddSpriteCommand.Execute(null);
        vm.AddSpriteCommand.Execute(null);

        vm.PickBackgroundColorCommand.Execute(vm.ColorPalette[7]); // cian

        int expected = PixelReader.Bgra(vm.ColorPalette[7].Color);
        foreach (Sprite sprite in vm.SpritesBank.SpritesList)
            Assert.All(PixelReader.Read(sprite.ImageMini!), pixel => Assert.Equal(expected, pixel));
    }

    [AvaloniaFact]
    public void El_fondo_no_borra_los_pixeles_ya_pintados()
    {
        SpritesEditorViewModel vm = NewEditor(SpriteBank.SpriteType.MSX2);
        vm.CurrentSprite.ArraySpriteRows[9].ArrayColumns[9] = true;
        vm.RowColors[9].PickCommand.Execute(vm.ColorPalette[3]); // verde claro

        vm.PickBackgroundColorCommand.Execute(vm.ColorPalette[6]); // rojo oscuro

        ImageMini mini = vm.CurrentSprite.ImageMini!;
        Assert.Equal(PixelReader.Bgra(vm.ColorPalette[3].Color), PixelReader.At(mini, 9, 9));
        Assert.Equal(PixelReader.Bgra(vm.ColorPalette[6].Color), PixelReader.At(mini, 8, 9));
    }

    private static SpritesEditorViewModel NewEditor(SpriteBank.SpriteType type) =>
        new(new SpriteBank(type), GlobalSettings.CurrentColorPalette);
}
