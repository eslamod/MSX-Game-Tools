using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_SpritesEditor.Views;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// El diálogo de confirmación de verdad, pulsando sus botones. El servicio falso de
/// los otros tests no pasa por aquí.
/// </summary>
public class ConfirmationWindowTests
{
    [AvaloniaFact]
    public async Task Pulsar_el_boton_de_confirmar_devuelve_true()
    {
        using var owner = new OwnerWindow();

        ConfirmationWindow dialog = owner.OpenConfirmation();
        Task<bool> result = dialog.ShowDialog<bool>(owner.Window);
        Dispatcher.UIThread.RunJobs();

        ClickButton(dialog, "Eliminar");

        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Pulsar_cancelar_devuelve_false()
    {
        using var owner = new OwnerWindow();

        ConfirmationWindow dialog = owner.OpenConfirmation();
        Task<bool> result = dialog.ShowDialog<bool>(owner.Window);
        Dispatcher.UIThread.RunJobs();

        ClickButton(dialog, "Cancelar");

        Assert.False(await result);
    }

    [AvaloniaFact]
    public async Task Cerrar_la_ventana_equivale_a_cancelar()
    {
        using var owner = new OwnerWindow();

        ConfirmationWindow dialog = owner.OpenConfirmation();
        Task<bool> result = dialog.ShowDialog<bool>(owner.Window);
        Dispatcher.UIThread.RunJobs();

        dialog.Close();

        Assert.False(await result);
    }

    [AvaloniaFact]
    public async Task El_dialogo_muestra_el_titulo_el_mensaje_y_la_etiqueta()
    {
        using var owner = new OwnerWindow();

        ConfirmationWindow dialog = owner.OpenConfirmation();
        Task<bool> result = dialog.ShowDialog<bool>(owner.Window);
        Dispatcher.UIThread.RunJobs();

        string[] texts = [.. dialog.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];

        Assert.Equal("Eliminar paleta", dialog.Title);
        Assert.Contains("Eliminar paleta", texts);
        Assert.Contains("Se va a eliminar la paleta «Nocturna».", texts);

        dialog.Close();
        await result;
    }

    private static void ClickButton(Window window, string content)
    {
        Button button = window.GetVisualDescendants()
            .OfType<Button>()
            .First(b => (b.Content as string) == content);

        Point centre = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class OwnerWindow : IDisposable
    {
        public OwnerWindow()
        {
            Window = new Window { Width = 600, Height = 400 };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public Window Window { get; }

        public ConfirmationWindow OpenConfirmation() => new(
            "Eliminar paleta",
            "Se va a eliminar la paleta «Nocturna».",
            "Eliminar");

        public void Dispose()
        {
            Window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
