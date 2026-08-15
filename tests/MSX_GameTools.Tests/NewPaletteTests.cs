using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El formulario de crear una paleta: el nombre y de cuál se copian los 16 colores.
/// </summary>
public class NewPaletteTests
{
    [AvaloniaFact]
    public void Propone_un_nombre_libre_y_la_paleta_que_se_esta_mirando()
    {
        var main = new MainWindowViewModel();

        TestPalette.Create(main, "Nocturna");
        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;

        Assert.Equal("Palette 1", form.Name);
        Assert.Same(main.Palettes.ActivePalette, form.Source);

        // Entre las que se puede elegir están todas, la del MSX incluida.
        Assert.Contains(form.Sources, palette => palette.Name == ColorPalette.StandardName);
        Assert.Contains(form.Sources, palette => palette.Name == "Nocturna");
    }

    /// <summary>
    /// Los colores salen de la paleta elegida, no de la que estuviera seleccionada.
    /// </summary>
    /// <remarks>
    /// Es para lo que está el formulario: la que quieres de base rara vez es la que
    /// resulta que estabas mirando.
    /// </remarks>
    [AvaloniaFact]
    public void Copia_los_colores_de_la_paleta_elegida()
    {
        var main = new MainWindowViewModel();

        EditPaletteViewModel first = TestPalette.Create(main, "Nocturna");
        first.Palette[3].SetComponents(7, 0, 0);
        first.Palette[3].Name = "Sangre";

        // Se crea otra estando seleccionada la Nocturna, pero copiando de la del MSX.
        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;
        form.Name = "Diurna";
        form.Source = form.Sources.Single(palette => palette.Name == ColorPalette.StandardName);
        form.AcceptPaletteCommand.Execute(null);

        ColorPalette created = main.Palettes.ActivePalette;

        Assert.Equal("Diurna", created.Name);
        Assert.Equal("373", created[3].HexRgb);
    }

    /// <summary>Copiar de una tuya sí se lleva sus nombres, que los escribiste tú.</summary>
    [AvaloniaFact]
    public void Copiar_de_una_propia_se_lleva_sus_nombres()
    {
        var main = new MainWindowViewModel();

        EditPaletteViewModel first = TestPalette.Create(main, "Nocturna");
        first.Palette[3].Name = "Sangre";

        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;
        form.Name = "Nocturna 2";
        form.Source = form.Sources.Single(palette => palette.Name == "Nocturna");
        form.AcceptPaletteCommand.Execute(null);

        Assert.Equal("Sangre", main.Palettes.ActivePalette[3].Name);
    }

    /// <summary>Y de la del MSX no, que sus nombres son de la máquina.</summary>
    [AvaloniaFact]
    public void Copiar_de_la_del_msx_no_trae_los_nombres()
    {
        var main = new MainWindowViewModel();

        EditPaletteViewModel editor = TestPalette.Create(main, "Diurna");

        Assert.All(editor.Palette.Colors, color => Assert.Equal(string.Empty, color.Name));
        Assert.Equal("373", editor.Palette[3].HexRgb);
    }

    [AvaloniaFact]
    public void Un_nombre_vacio_no_crea_nada()
    {
        var main = new MainWindowViewModel();

        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;
        form.Name = "   ";
        form.AcceptPaletteCommand.Execute(null);

        Assert.True(form.HasError);
        Assert.Single(main.Palettes.Palettes);
        Assert.Same(form, main.RightPanViewModel);
    }

    [AvaloniaFact]
    public void Un_nombre_repetido_no_crea_nada()
    {
        var main = new MainWindowViewModel();

        TestPalette.Create(main, "Nocturna");
        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;
        form.Name = "Nocturna";
        form.AcceptPaletteCommand.Execute(null);

        Assert.True(form.HasError);
        Assert.Equal(2, main.Palettes.Palettes.Count);
        Assert.Same(form, main.RightPanViewModel);
    }

    [AvaloniaFact]
    public void Cancelar_cierra_el_formulario_sin_crear_nada()
    {
        var main = new MainWindowViewModel();

        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;
        form.CancelPaletteCommand.Execute(null);

        Assert.Single(main.Palettes.Palettes);
        Assert.Null(main.RightPanViewModel);
    }
}
