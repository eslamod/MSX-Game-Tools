using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.Services;

namespace MSX_SpritesEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, PanelBaseViewModel> _panels = [];

    [ObservableProperty]
    private PanelBaseViewModel? _rightPanViewModel;

    [ObservableProperty]
    private PanelBaseViewModel? _selectedTab;

    /// <param name="dialogs">
    /// La aplicación inyecta el servicio real; si no se pasa ninguno no se abre nada,
    /// que es lo que quieren los tests y cualquier uso sin ventana.
    /// </param>
    public MainWindowViewModel(IDialogService? dialogs = null)
    {
        Dialogs = dialogs ?? new SilentDialogService();

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

    /// <summary>Se reparte a los paneles que necesiten confirmar algo destructivo.</summary>
    public IDialogService Dialogs { get; }

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
    private async Task DeletePaletteAsync()
    {
        ColorPalette palette = Palettes.ActivePalette;

        // Eliminar una paleta no se puede deshacer, y el botón está pegado a los
        // otros dos: mejor un clic de más que perder el trabajo.
        bool confirmed = await Dialogs.ConfirmAsync(
            "Eliminar paleta",
            $"Se va a eliminar la paleta «{palette.Name}». Esta acción no se puede deshacer.",
            "Eliminar");

        if (!confirmed)
            return;

        if (RightPanViewModel is EditPaletteViewModel editing && editing.Palette == palette)
            RightPanViewModel = null;

        Palettes.Remove(palette);
    }

    private bool CanDeletePalette() => Palettes.CanRemove(Palettes.ActivePalette);

    [RelayCommand]
    private async Task SavePaletteAsync()
    {
        ColorPalette palette = Palettes.ActivePalette;

        string? path = await Dialogs.PickFileToSaveAsync("Guardar paleta", SuggestedFileName(palette.Name));
        if (path is null)
            return;

        try
        {
            await File.WriteAllTextAsync(path, PaletteSerializer.Serialize(palette));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo guardar la paleta", exception.Message);
        }
    }

    [RelayCommand]
    private async Task LoadPaletteAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync("Cargar paleta");
        if (path is null)
            return;

        try
        {
            string json = await File.ReadAllTextAsync(path);
            Palettes.Import(PaletteSerializer.Deserialize(json));
        }
        catch (PaletteFormatException exception)
        {
            await Dialogs.ShowMessageAsync("La paleta no es válida", exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo abrir el fichero", exception.Message);
        }
    }

    /// <summary>El nombre de una paleta puede llevar caracteres que no valen en un fichero.</summary>
    private static string SuggestedFileName(string paletteName)
    {
        string clean = string.Concat(paletteName.Split(Path.GetInvalidFileNameChars())).Trim();

        return $"{(clean.Length == 0 ? "paleta" : clean)}.json";
    }

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