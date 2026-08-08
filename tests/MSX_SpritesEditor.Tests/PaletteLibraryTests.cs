using MSX_SpritesEditor.Entities;
using Xunit;

namespace MSX_SpritesEditor.Tests;

public class PaletteLibraryTests
{
    [Fact]
    public void Arranca_con_la_paleta_msx_estandar_activa_y_protegida()
    {
        var library = new PaletteLibrary();

        Assert.Single(library.Palettes);
        Assert.Same(library.Palettes[0], library.ActivePalette);
        Assert.Equal(ColorPalette.StandardName, library.ActivePalette.Name);
        Assert.True(library.ActivePalette.IsReadOnly);
    }

    [Fact]
    public void La_estandar_no_se_puede_eliminar()
    {
        var library = new PaletteLibrary();

        Assert.False(library.CanRemove(library.ActivePalette));
        Assert.False(library.Remove(library.ActivePalette));
        Assert.Single(library.Palettes);
    }

    [Fact]
    public void Crear_copia_la_activa_y_la_deja_seleccionada()
    {
        var library = new PaletteLibrary();

        ColorPalette created = library.Add();

        Assert.Equal(2, library.Palettes.Count);
        Assert.Same(created, library.ActivePalette);
        Assert.False(created.IsReadOnly);
        Assert.Equal("Palette 1", created.Name);

        // Mismos colores que la estándar, pero objetos propios.
        for (int i = 0; i < ColorPalette.Size; i++)
        {
            Assert.Equal(library.Palettes[0][i].Red, created[i].Red);
            Assert.NotSame(library.Palettes[0][i], created[i]);
        }
    }

    [Fact]
    public void Editar_una_copia_no_toca_la_original()
    {
        var library = new PaletteLibrary();
        ColorPalette standard = library.Palettes[0];
        ColorPalette copy = library.Add();

        copy[5].Red = 0;

        Assert.Equal(0, copy[5].Red);
        Assert.Equal(2, standard[5].Red);
    }

    [Fact]
    public void Los_nombres_generados_no_se_repiten()
    {
        var library = new PaletteLibrary();

        Assert.Equal("Palette 1", library.Add().Name);
        Assert.Equal("Palette 2", library.Add().Name);
        Assert.Equal("Palette 3", library.Add().Name);
    }

    [Fact]
    public void Eliminar_la_activa_pasa_el_foco_a_otra()
    {
        var library = new PaletteLibrary();
        library.Add();                       // Palette 1
        ColorPalette second = library.Add(); // Palette 2, activa

        Assert.True(library.Remove(second));

        Assert.Equal(2, library.Palettes.Count);
        Assert.Equal("Palette 1", library.ActivePalette.Name);
    }

    [Fact]
    public void Eliminar_una_que_no_esta_activa_no_cambia_la_activa()
    {
        var library = new PaletteLibrary();
        ColorPalette first = library.Add();
        ColorPalette second = library.Add();

        Assert.True(library.Remove(first));

        Assert.Same(second, library.ActivePalette);
    }
}
