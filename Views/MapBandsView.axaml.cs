using Avalonia.Controls;

namespace MSX_GameTools.Views;

/// <summary>
/// The tile set of each band of the screen, inside whichever form is asking.
/// </summary>
/// <remarks>
/// No code of its own: it is three boxes over a <see cref="ViewModels.MapBandsViewModel"/>,
/// which is the one that knows how many bands there are and what can go in them.
/// </remarks>
public partial class MapBandsView : UserControl
{
    public MapBandsView() => InitializeComponent();
}
