using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace MSX_GameTools.Views;

/// <summary>
/// A keyboard shortcut written the Windows way, <c>{views:Shortcut Ctrl+S}</c>, that takes Cmd
/// where it says Ctrl when the program runs on a Mac.
/// </summary>
/// <remarks>
/// On a Mac the shortcuts of a program go with Cmd: Cmd+S saves, and Ctrl is left to the text
/// boxes, where Ctrl+O opens a line and Ctrl+A goes to its start. Written straight into the
/// XAML, Ctrl+S would stay Ctrl+S everywhere. The key binding and the text of the menu both go
/// through here, so that the menu shows the key that does something.
/// </remarks>
public sealed class ShortcutExtension(string gesture) : MarkupExtension
{
    /// <summary>What Ctrl stands for: Cmd on a Mac and Ctrl everywhere else.</summary>
    /// <remarks>Settable so that a test can be a Mac on another system.</remarks>
    public static KeyModifiers Command { get; set; } =
        OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>The shortcut as it goes on Windows and Linux.</summary>
    public string Gesture { get; set; } = gesture;

    public override object ProvideValue(IServiceProvider serviceProvider) => For(Gesture);

    /// <summary>The shortcut for the system the program is running on.</summary>
    public static KeyGesture For(string gesture)
    {
        KeyGesture written = KeyGesture.Parse(gesture);

        if (!written.KeyModifiers.HasFlag(KeyModifiers.Control))
            return written;

        return new KeyGesture(written.Key, (written.KeyModifiers & ~KeyModifiers.Control) | Command);
    }
}
