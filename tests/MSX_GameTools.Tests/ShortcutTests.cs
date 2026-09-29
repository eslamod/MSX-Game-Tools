using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// The shortcuts to open and save, pressed on the real window: Ctrl on Windows and Linux,
/// Cmd on a Mac.
/// </summary>
/// <remarks>
/// A Mac is played by setting what Ctrl stands for before the window is built, which is when
/// the XAML reads it. Each test puts back what was there.
/// </remarks>
public class ShortcutTests
{
    [AvaloniaFact]
    public void En_un_mac_abrir_y_guardar_van_con_cmd()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        Run(KeyModifiers.Meta, main, window =>
        {
            Press(window, Key.O, RawInputModifiers.Meta);
            Assert.Equal(1, dialogs.OpenCalls);

            // Something to save that has never been saved, so saving asks where.
            main.OpenTileSet(new TileSet("Bosque"));

            Press(window, Key.S, RawInputModifiers.Meta);
            Assert.Equal(1, dialogs.SaveCalls);

            Press(window, Key.S, RawInputModifiers.Meta | RawInputModifiers.Shift);
            Assert.Equal(2, dialogs.SaveCalls);
        });
    }

    /// <summary>On a Mac, Ctrl is left to the text boxes.</summary>
    [AvaloniaFact]
    public void En_un_mac_control_no_abre_ni_guarda()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        Run(KeyModifiers.Meta, main, window =>
        {
            Press(window, Key.O, RawInputModifiers.Control);

            main.OpenTileSet(new TileSet("Bosque"));

            Press(window, Key.S, RawInputModifiers.Control);
            Press(window, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);

            Assert.Equal((0, 0), (dialogs.OpenCalls, dialogs.SaveCalls));
        });
    }

    [AvaloniaFact]
    public void Fuera_del_mac_abrir_y_guardar_van_con_control()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        Run(KeyModifiers.Control, main, window =>
        {
            Press(window, Key.O, RawInputModifiers.Control);
            Assert.Equal(1, dialogs.OpenCalls);

            main.OpenTileSet(new TileSet("Bosque"));

            Press(window, Key.S, RawInputModifiers.Control);
            Assert.Equal(1, dialogs.SaveCalls);

            Press(window, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
            Assert.Equal(2, dialogs.SaveCalls);
        });
    }

    /// <summary>What the menu shows next to each entry is the key that runs it.</summary>
    [AvaloniaTheory]
    [InlineData(KeyModifiers.Meta)]
    [InlineData(KeyModifiers.Control)]
    public void El_menu_ensena_la_tecla_que_funciona(KeyModifiers commandKey)
    {
        var main = new MainWindowViewModel(new TestDialogService());

        Run(commandKey, main, window =>
        {
            ICommand[] commands = [main.OpenProjectCommand, main.SaveDocumentCommand, main.SaveDocumentAsCommand];

            foreach (ICommand command in commands)
            {
                MenuItem item = window.GetLogicalDescendants()
                    .OfType<MenuItem>()
                    .Single(one => ReferenceEquals(one.Command, command));

                KeyBinding binding = window.KeyBindings.Single(one => ReferenceEquals(one.Command, command));

                Assert.Equal(binding.Gesture, item.InputGesture);
                Assert.True(
                    item.InputGesture!.KeyModifiers.HasFlag(commandKey),
                    $"{item.InputGesture} does not go with {commandKey}");
            }
        });
    }

    /// <summary>
    /// Builds the main window as the given system would, runs the test on it, and leaves
    /// everything as it was.
    /// </summary>
    private static void Run(KeyModifiers commandKey, MainWindowViewModel main, Action<MainWindow> test)
    {
        KeyModifiers before = ShortcutExtension.Command;
        ShortcutExtension.Command = commandKey;

        MainWindow? window = null;

        try
        {
            window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
            window.Show();
            Pump();

            test(window);
        }
        finally
        {
            ShortcutExtension.Command = before;
            window?.Close();
            Pump();
        }
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers)
    {
        PhysicalKey physical = key == Key.O ? PhysicalKey.O : PhysicalKey.S;

        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
