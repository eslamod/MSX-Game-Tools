using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.Services;

namespace MSX_SpritesEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, PanelBaseViewModel> _panels = [];

    [ObservableProperty]
    private PanelBaseViewModel? _rightPanViewModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSpriteBankCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSpriteBankBinaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSpriteBankAssemblerCommand))]
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

        TreeGeneralVm.OpenItemCommand = OpenTreeItemCommand;
        TreeGeneralVm.DeleteItemCommand = DeleteTreeItemCommand;

        Backgrounds.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReferenceImageLibrary.SelectedImage))
                DeleteBackgroundCommand.NotifyCanExecuteChanged();
        };
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

    /// <summary>
    /// Imagenes de referencia que se pueden poner de fondo. Recurso del espacio de
    /// trabajo, como las paletas: se cargan una vez y las usa quien quiera.
    /// </summary>
    public ReferenceImageLibrary Backgrounds { get; } = new();

    /// <summary>Se reparte a los paneles que necesiten confirmar algo destructivo.</summary>
    public IDialogService Dialogs { get; }

    public TreeGeneralViewModel TreeGeneralVm { get; } = new();

    public ObservableCollection<PanelBaseViewModel> Tabs { get; } = [];

    public int CurrentSpriteBankCounter { get; set; }

    public int CurrentTileSetCounter { get; set; }

    [RelayCommand]
    private void AddSpriteBank() => RightPanViewModel = new EditSpriteBankViewModel(this);

    [RelayCommand]
    private void AddTileSet() => RightPanViewModel = new EditTileSetViewModel(this);

    /// <summary>Abre un juego de tiles en una pestaña nueva y lo cuelga del árbol.</summary>
    public TileSetEditorViewModel OpenTileSet(TileSet tileSet)
    {
        var panel = new TileSetEditorViewModel(tileSet, Palettes)
        {
            TagId = $"tls{CurrentTileSetCounter}",
            Header = $"{tileSet.Name} (TS)",
        };

        CurrentTileSetCounter++;

        AddPanelToDic(panel);
        Tabs.Add(panel);
        SelectedTab = panel;
        TreeGeneralVm.AddTileSet(panel.Header, panel.TagId, panel);

        return panel;
    }

    /// <summary>Abre un banco en una pestaña nueva y lo cuelga del árbol.</summary>
    public SpritesEditorViewModel OpenSpriteBank(SpriteBank bank, int backgroundColorIndex = 1)
    {
        var panel = new SpritesEditorViewModel(bank, Palettes, Dialogs, Backgrounds)
        {
            TagId = $"spb{CurrentSpriteBankCounter}",
            Header = $"{bank.Name} (SP)",
            BackgroundColorIndex = backgroundColorIndex,
        };

        CurrentSpriteBankCounter++;

        AddPanelToDic(panel);
        Tabs.Add(panel);
        SelectedTab = panel;
        TreeGeneralVm.AddSpriteBank(panel.Header, panel.TagId, panel);

        return panel;
    }

    [RelayCommand(CanExecute = nameof(CanSaveSpriteBank))]
    private async Task SaveSpriteBankAsync()
    {
        if (SelectedTab is not SpritesEditorViewModel editor)
            return;

        string? path = await Dialogs.PickFileToSaveAsync(
            "Guardar banco de sprites",
            SuggestedFileName(editor.SpritesBank.Name));

        if (path is null)
            return;

        try
        {
            // La paleta va dentro: los sprites guardan indices, y sin ella el banco se
            // abriria con los colores que hubiera puestos en ese momento.
            string json = SpriteBankSerializer.Serialize(
                editor.SpritesBank, editor.ColorPalette, editor.BackgroundColorIndex, [.. Backgrounds.Images]);

            await File.WriteAllTextAsync(path, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo guardar el banco", exception.Message);
        }
    }

    private bool CanSaveSpriteBank() => SelectedTab is SpritesEditorViewModel;

    [RelayCommand]
    private async Task LoadSpriteBankAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync("Cargar banco de sprites");
        if (path is null)
            return;

        try
        {
            string json = await File.ReadAllTextAsync(path);
            LoadedSpriteBank loaded = SpriteBankSerializer.Deserialize(json);

            Palettes.Activate(loaded.Palette);
            await LoadBackgroundsAsync(loaded.Backgrounds);
            OpenSpriteBank(loaded.Bank, loaded.BackgroundColorIndex);
        }
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync("El banco no es válido", exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo abrir el fichero", exception.Message);
        }
    }

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

    /// <summary>
    /// Carga un png o jpg como imagen de referencia. Si no cabe en el lienzo de un grupo
    /// se pregunta con qué lado trocearla, y cada trozo pasa a ser un fondo.
    /// </summary>
    [RelayCommand]
    private async Task LoadBackgroundAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync("Cargar imagen de referencia", PickerFileKind.Image);
        if (path is null)
            return;

        try
        {
            PixelSize size = ReferenceImage.Measure(path);

            int cellSize = 0;

            if (size.Width > ReferenceImageSlicer.SingleTileMax || size.Height > ReferenceImageSlicer.SingleTileMax)
            {
                int? answer = await Dialogs.AskCellSizeAsync(
                    "Tamaño de celda",
                    $"«{Path.GetFileName(path)}» mide {size.Width}x{size.Height}, así que se carga como hoja de sprites. "
                    + "¿De qué lado son las celdas? Lo que sobre en los bordes se coge recortado.",
                    suggested: 16,
                    maximum: Math.Max(size.Width, size.Height));

                if (answer is not { } chosen)
                    return;

                cellSize = chosen;
            }

            Backgrounds.Load(path, cellSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await Dialogs.ShowMessageAsync("No se pudo cargar la imagen", exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteBackground))]
    private async Task DeleteBackgroundAsync()
    {
        if (Backgrounds.SelectedImage is not { } image)
            return;

        // Se borra la imagen entera, no un fondo suelto: de una hoja de sprites salen
        // decenas y quitarlos de uno en uno no serviría de nada. Por eso se dice cuántos
        // se lleva por delante.
        bool confirmed = await Dialogs.ConfirmAsync(
            "Eliminar imagen de referencia",
            $"Se van a eliminar los {image.Tiles.Count} fondos de «{Path.GetFileName(image.Path)}». "
            + "Los grupos que los usen se quedarán sin fondo.",
            "Eliminar");

        if (!confirmed)
            return;

        Backgrounds.Remove(image);
    }

    private bool CanDeleteBackground() => Backgrounds.SelectedImage is not null;

    /// <summary>
    /// Recarga las imágenes que traía un banco. Que falte una no impide abrirlo: los
    /// grupos que la usaran se quedan sin fondo y ya está.
    /// </summary>
    private async Task LoadBackgroundsAsync(IReadOnlyList<BackgroundImageRef> references)
    {
        var missing = new List<string>();

        foreach (BackgroundImageRef reference in references)
        {
            if (Backgrounds.Images.Any(image =>
                    string.Equals(image.Path, reference.Path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                Backgrounds.Load(reference.Path, reference.CellSize);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                missing.Add(Path.GetFileName(reference.Path));
            }
        }

        if (missing.Count > 0)
        {
            await Dialogs.ShowMessageAsync(
                "Faltan imágenes de referencia",
                $"No se han podido cargar: {string.Join(", ", missing)}. El banco se abre igual, sin esos fondos.");
        }
    }

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
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync("La paleta no es válida", exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo abrir el fichero", exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveSpriteBank))]
    private Task ExportSpriteBankBinaryAsync() => ExportSpriteBankAsync(binary: true);

    [RelayCommand(CanExecute = nameof(CanSaveSpriteBank))]
    private Task ExportSpriteBankAssemblerAsync() => ExportSpriteBankAsync(binary: false);

    /// <summary>
    /// Escribe las dos tablas. Se pide un nombre base y de ahí salen los dos ficheros,
    /// para no encadenar dos selectores seguidos.
    /// </summary>
    private async Task ExportSpriteBankAsync(bool binary)
    {
        if (SelectedTab is not SpritesEditorViewModel editor)
            return;

        SpriteBank bank = editor.SpritesBank;
        string extension = binary ? ".bin" : ".asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            binary ? "Exportar a binario" : "Exportar a ensamblador",
            $"{SpriteBankExporter.LabelOf(bank.Name)}{extension}");

        if (path is null)
            return;

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);

        string patternsPath = Path.Combine(folder, $"{stem}_patterns{extension}");
        string groupsPath = Path.Combine(folder, $"{stem}_groups{extension}");

        try
        {
            if (binary)
            {
                await File.WriteAllBytesAsync(patternsPath, SpriteBankExporter.PatternsToBinary(bank));
                await File.WriteAllBytesAsync(groupsPath, SpriteBankExporter.GroupsToBinary(bank));
            }
            else
            {
                await File.WriteAllTextAsync(patternsPath, SpriteBankExporter.PatternsToAssembler(bank));
                await File.WriteAllTextAsync(groupsPath, SpriteBankExporter.GroupsToAssembler(bank));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo exportar el banco", exception.Message);

            return;
        }

        // El nombre elegido se reparte en dos, asi que conviene decir cuales han salido.
        await Dialogs.ShowMessageAsync(
            "Banco exportado",
            $"Se han escrito:{Environment.NewLine}{Path.GetFileName(patternsPath)}{Environment.NewLine}{Path.GetFileName(groupsPath)}");
    }

    /// <summary>El nombre de una paleta puede llevar caracteres que no valen en un fichero.</summary>
    private static string SuggestedFileName(string paletteName)
    {
        string clean = string.Concat(paletteName.Split(Path.GetInvalidFileNameChars())).Trim();

        return $"{(clean.Length == 0 ? "paleta" : clean)}.json";
    }

    /// <summary>
    /// Vuelve a enseñar el elemento del árbol. Es el doble clic.
    /// </summary>
    /// <remarks>
    /// Si su pestaña estaba cerrada se vuelve a abrir con el estado que tenía: cerrar
    /// una pestaña sólo la quita de la vista, el panel sigue vivo.
    /// </remarks>
    [RelayCommand]
    private void OpenTreeItem(ItemTree? item)
    {
        if (item is { IsPanelNode: true })
            AddVisiblePanel(item.Tag);
    }

    /// <summary>
    /// Elimina el elemento del proyecto: se va del árbol, se cierra su pestaña y se
    /// olvida el panel.
    /// </summary>
    /// <remarks>
    /// Es lo contrario de cerrar la pestaña, y por eso pregunta. Cerrar es reversible
    /// con un doble clic; esto no, y como no llevamos control de cambios sin guardar,
    /// el aviso lo dice explícitamente.
    /// </remarks>
    [RelayCommand]
    private async Task DeleteTreeItemAsync(ItemTree? item)
    {
        if (item is not { IsPanelNode: true })
            return;

        bool confirmed = await Dialogs.ConfirmAsync(
            "Eliminar del proyecto",
            $"Se va a eliminar «{item.DisplayText}» y se cerrará su pestaña. "
            + "Se perderá lo que no hayas guardado en un fichero.",
            "Eliminar");

        if (!confirmed)
            return;

        if (GetPanelFromDic(item.Tag) is { } panel)
        {
            CloseTab(panel);
            _panels.Remove(item.Tag);
        }

        TreeGeneralVm.Remove(item);
    }

    /// <summary>
    /// Quita la pestaña de la vista sin tocar el proyecto. El panel se queda guardado y
    /// el nodo del árbol lo puede volver a traer con un doble clic.
    /// </summary>
    [RelayCommand]
    private void CloseTab(PanelBaseViewModel? panel)
    {
        if (panel is null)
            return;

        int position = Tabs.IndexOf(panel);
        if (position < 0)
            return;

        Tabs.Remove(panel);

        // Hay que decir qué queda seleccionado: al desaparecer la suya, el TabControl
        // escribe null y se quedaría sin ninguna con pestañas todavía abiertas.
        SelectedTab = Tabs.Count > 0 ? Tabs[Math.Min(position, Tabs.Count - 1)] : null;
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