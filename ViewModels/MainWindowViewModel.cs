using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, PanelBaseViewModel> _panels = [];

    [ObservableProperty]
    private PanelBaseViewModel? _rightPanViewModel;

    [ObservableProperty]
    private PanelBaseViewModel? _selectedTab;

    public TreeGeneralViewModel TreeGeneralVm { get; } = new();

    public ObservableCollection<PanelBaseViewModel> Tabs { get; } = [];

    public int CurrentSpriteBankCounter { get; set; }

    [RelayCommand]
    private void AddSpriteBank() => RightPanViewModel = new EditSpriteBankViewModel(this);

    /// <summary>
    /// Sustituye a la antigua clase <c>CommandShowTab</c>. Igual que en la versión WPF,
    /// todavía no hay nada enlazado a él: falta decidir el gesto en el árbol.
    /// </summary>
    [RelayCommand]
    private void ShowTab(ItemTree? item)
    {
        if (item is not null)
            AddVisiblePanel(item.Tag);
    }

    public void AddPanelToDic(PanelBaseViewModel panel) => _panels.TryAdd(panel.TagId, panel);

    public PanelBaseViewModel? GetPanelFromDic(string panelId) => _panels.GetValueOrDefault(panelId);

    public void AddVisiblePanel(string panelId)
    {
        PanelBaseViewModel? panel = GetPanelFromDic(panelId);
        if (panel is null)
            return;

        if (!Tabs.Contains(panel))
            Tabs.Add(panel);

        SelectedTab = panel;
    }
}
