using Avalonia.Controls;
using Avalonia.Interactivity;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

public partial class ShiftReportView : UserControl
{
    public ShiftReportView() => InitializeComponent();

    /// <summary>
    /// The band of the screen the report is looking at.
    /// </summary>
    /// <remarks>
    /// Through the Tag, the same as every other group of buttons around here: the group says
    /// which one is on and the number lives in the panel.
    /// </remarks>
    private void OnBandChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string tag }
            || DataContext is not ShiftReportViewModel report)
        {
            return;
        }

        if (int.TryParse(tag, out int band))
            report.Band = band;
    }
}
