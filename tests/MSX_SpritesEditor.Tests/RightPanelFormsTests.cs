using Avalonia.Headless.XUnit;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Los formularios del lateral: los de agregar algo y el de la paleta.
/// </summary>
/// <remarks>
/// De cada uno hay como mucho uno abierto. Se trae el que hay en vez de deshabilitar el
/// botón: con el lateral sin el foco, un botón apagado no dice por qué está apagado.
/// </remarks>
public class RightPanelFormsTests
{
    [AvaloniaTheory]
    [InlineData("AddTileSet", "Agregar tileset")]
    [InlineData("AddSpriteBank", "Agregar banco de sprites")]
    public void El_formulario_dice_en_la_pestana_lo_que_es(string command, string expected)
    {
        var main = new MainWindowViewModel();

        Execute(main, command);

        Assert.Equal(expected, main.RightPanViewModel!.Header);
    }

    [AvaloniaTheory]
    [InlineData("AddTileSet")]
    [InlineData("AddSpriteBank")]
    public void Pulsar_dos_veces_trae_el_que_ya_estaba(string command)
    {
        var main = new MainWindowViewModel();

        Execute(main, command);
        PanelBaseViewModel first = main.RightPanViewModel!;

        Execute(main, command);

        Assert.Single(main.RightPanels);
        Assert.Same(first, main.RightPanViewModel);
    }

    /// <summary>Lo escrito no se pierde al volver a pulsar el botón.</summary>
    [AvaloniaFact]
    public void Volver_al_formulario_conserva_lo_escrito()
    {
        var main = new MainWindowViewModel();

        main.AddTileSetCommand.Execute(null);
        ((EditTileSetViewModel)main.RightPanViewModel!).Name = "Bosque";

        main.AddTileSetCommand.Execute(null);

        Assert.Equal("Bosque", ((EditTileSetViewModel)main.RightPanViewModel!).Name);
    }

    /// <summary>Los dos formularios son distintos y sí conviven.</summary>
    [AvaloniaFact]
    public void Los_dos_formularios_pueden_estar_abiertos_a_la_vez()
    {
        var main = new MainWindowViewModel();

        main.AddTileSetCommand.Execute(null);
        main.AddSpriteBankCommand.Execute(null);

        Assert.Equal(2, main.RightPanels.Count);
        Assert.IsType<EditSpriteBankViewModel>(main.RightPanViewModel);
    }

    [AvaloniaFact]
    public void Al_aceptar_el_formulario_se_cierra_y_se_puede_abrir_otro()
    {
        var main = new MainWindowViewModel();

        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;
        form.Name = "Bosque";
        form.AcceptTileSetCommand.Execute(null);

        Assert.Empty(main.RightPanels);

        main.AddTileSetCommand.Execute(null);

        Assert.Single(main.RightPanels);
        Assert.Equal(string.Empty, ((EditTileSetViewModel)main.RightPanViewModel!).Name);
    }

    // ------------------------------------------------------------------ paleta

    [AvaloniaFact]
    public void Editar_la_misma_paleta_dos_veces_trae_el_editor_que_hay()
    {
        var main = new MainWindowViewModel();

        main.AddPaletteCommand.Execute(null);
        PanelBaseViewModel first = main.RightPanViewModel!;

        main.EditPaletteCommand.Execute(null);

        Assert.Single(main.RightPanels);
        Assert.Same(first, main.RightPanViewModel);
    }

    /// <summary>Dos editores de paleta a la vez no sirven de nada: el nuevo sustituye.</summary>
    [AvaloniaFact]
    public void Editar_otra_paleta_cambia_el_editor_en_vez_de_apilarlo()
    {
        var main = new MainWindowViewModel();

        main.AddPaletteCommand.Execute(null);
        ColorPalette first = ((EditPaletteViewModel)main.RightPanViewModel!).Palette;

        main.AddPaletteCommand.Execute(null);

        Assert.Single(main.RightPanels);

        var editor = (EditPaletteViewModel)main.RightPanViewModel!;

        Assert.NotSame(first, editor.Palette);
        Assert.Same(main.Palettes.ActivePalette, editor.Palette);
    }

    private static void Execute(MainWindowViewModel main, string command)
    {
        if (command == "AddTileSet")
            main.AddTileSetCommand.Execute(null);
        else
            main.AddSpriteBankCommand.Execute(null);
    }
}
