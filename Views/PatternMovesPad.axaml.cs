using Avalonia.Controls;

namespace MSX_GameTools.Views;

/// <summary>
/// The pad of moves, hanging from a button of the tile editor and of the sprite editor.
/// </summary>
/// <remarks>
/// No code of its own: everything it does is a command of whoever it is looking at, and who
/// that is comes down from the button it hangs from. What it needs of that editor is written
/// in <see cref="ViewModels.IPatternMoves"/>.
/// </remarks>
public partial class PatternMovesPad : UserControl
{
    public PatternMovesPad() => InitializeComponent();
}
