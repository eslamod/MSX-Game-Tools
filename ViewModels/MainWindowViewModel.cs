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

    public MainWindowViewModel()
    {
        // Los comandos de paleta dependen de cuál esté activa (la estándar no se puede
        // editar ni eliminar) y de cuántas queden (nunca se borra la última).
        Palettes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaletteLibrary.ActivePalette))
                RefreshPaletteCommands();
        };

        Palettes.Palettes.CollectionChanged += (_, _) => RefreshPaletteCommands();
    }

    private void RefreshPaletteCommands()
    {
        EditPaletteCommand.NotifyCanExecuteChanged();
        DeletePaletteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Recurso común a todos los módulos: los bancos de sprites, y en su día los
    /// tilesets y los mapas, dibujan con la paleta activa de aquí.
    /// </summary>
    public PaletteLibrary Palettes { get; } = new();

    public TreeGeneralViewModel TreeGeneralVm { get; } = new();

    public ObservableCollection<PanelBaseViewModel> Tabs { get; } = [];

    public int CurrentSpriteBankCounter { get; set; }

    [RelayCommand]
    private void AddSpriteBank() => RightPanViewModel = new EditSpriteBankViewModel(this);

    /// <summary>Crea una copia editable de la paleta activa y abre su editor.</summary>
    [RelayCommand]
    private void AddPalette()
    {
        ColorPalette created = Palettes.Add();
        RightPanViewModel = new EditPaletteViewModel(this, created);
    }

    [RelayCommand(CanExecute = nameof(CanEditPalette))]
    private void EditPalette() =>
        RightPanViewModel = new EditPaletteViewModel(this, Palettes.ActivePalette);

    private bool CanEditPalette() => !Palettes.ActivePalette.IsReadOnly;

    [RelayCommand(CanExecute = nameof(CanDeletePalette))]
    private void DeletePalette()
    {
        if (RightPanViewModel is EditPaletteViewModel editing && editing.Palette == Palettes.ActivePalette)
            RightPanViewModel = null;

        Palettes.Remove(Palettes.ActivePalette);
    }

    private bool CanDeletePalette() => Palettes.CanRemove(Palettes.ActivePalette);

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
