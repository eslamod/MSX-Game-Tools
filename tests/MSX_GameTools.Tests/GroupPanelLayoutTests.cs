using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que se pulsa en el panel del grupo se queda quieto.
/// </summary>
/// <remarks>
/// La lista de planos crece y encoge al añadir y quitar, y estaba por encima de los botones:
/// cada + o - movía de sitio el botón que se acababa de pulsar, así que el siguiente clic caía
/// donde ya no estaba. Lo que crece va debajo de lo que se pulsa.
/// </remarks>
public class GroupPanelLayoutTests
{
    [AvaloniaFact]
    public void Los_botones_y_las_cajas_no_se_mueven_al_anadir_planos()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);
        Pump();

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        double addWas = TopOf(Add(editor), editor);
        double boxWas = TopOf(Box(editor), editor);

        for (int plane = 1; plane < 9; plane++)
            group.AddMember(plane);

        Pump();

        Assert.Equal(9, group.Group.Members.Count);
        Assert.Equal(addWas, TopOf(Add(editor), editor));
        Assert.Equal(boxWas, TopOf(Box(editor), editor));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>El botón de añadir plano, por su comando y no por su sitio.</summary>
    private static Button Add(SpriteCanvasHarness editor) =>
        editor.GroupPanel.GetVisualDescendants()
            .OfType<Button>()
            .First(button =>
                ReferenceEquals(button.Command, editor.ViewModel.SelectedGroup!.AddMemberCommand));

    /// <summary>La caja del número de patrón.</summary>
    private static NumericUpDown Box(SpriteCanvasHarness editor) =>
        editor.GroupPanel.GetVisualDescendants()
            .OfType<NumericUpDown>()
            .Single(control => control.Name == "MemberPattern");

    private static double TopOf(Visual control, SpriteCanvasHarness editor) =>
        editor.PointIn(control, 0, 0).Y;
}
