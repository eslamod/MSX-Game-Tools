using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El slider de opacidad del panel del grupo tiene que llegar al control de la miniatura.
/// Con la propiedad a pelo no se ve: hace falta la vista montada.
/// </summary>
public class GroupOpacityViewTests
{
    [AvaloniaFact]
    public void La_opacidad_del_grupo_llega_a_la_miniatura()
    {
        var vm = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard());

        var view = new SpritesEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.ThumbnailMode = ThumbnailMode.Groups;
        vm.AddGroupCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedGroup!.SpriteOpacity = 0.25;
        Dispatcher.UIThread.RunJobs();

        ListBox groupList = view.FindControl<ListBox>("GroupList")!;

        double[] opacities = [.. groupList.GetVisualDescendants().OfType<Image>().Select(image => image.Opacity)];

        Assert.Contains(0.25, opacities);
    }

    /// <summary>
    /// Y al reves: mover el slider tiene que llegar al ViewModel. Poner la propiedad a
    /// mano no prueba nada de esto, que es por donde entra el usuario.
    /// </summary>
    [AvaloniaFact]
    public void Mover_el_slider_del_panel_cambia_la_opacidad_del_grupo()
    {
        var vm = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard());

        var view = new SpritesEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.ThumbnailMode = ThumbnailMode.Groups;
        vm.AddGroupCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Border panel = view.FindControl<Border>("GroupPanel")!;
        Slider slider = panel.GetVisualDescendants().OfType<Slider>().Single();

        slider.Value = 0.3;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.3, vm.SelectedGroup!.SpriteOpacity);
    }
}
