using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, PanelBaseViewModel> _panels = [];

    private PanelBaseViewModel? _rightPanViewModel;

    private double _rightPanelWidth = MinRightPanelWidth;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSpriteBankCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSpriteBankBinaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSpriteBankAssemblerCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveTileSetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTileSetBinaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTileSetAssemblerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTileSetPngCommand))]
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

    /// <summary>
    /// Ajustes de presentación comunes a todas las pestañas, como el zoom.
    /// </summary>
    public EditorPreferences Preferences { get; } = new();

    /// <summary>Se reparte a los paneles que necesiten confirmar algo destructivo.</summary>
    public IDialogService Dialogs { get; }

    public TreeGeneralViewModel TreeGeneralVm { get; } = new();

    public ObservableCollection<PanelBaseViewModel> Tabs { get; } = [];

    /// <summary>
    /// Los paneles del lateral derecho.
    /// </summary>
    /// <remarks>
    /// Conviven los formularios de un solo uso (crear un juego, editar la paleta), que se
    /// cierran solos al aceptar, y las herramientas que se quedan puestas mientras se
    /// trabaja, como los bloques de un juego de tiles.
    /// </remarks>
    public ObservableCollection<PanelBaseViewModel> RightPanels { get; } = [];

    /// <summary>Lo más estrecho que puede quedar el lateral sin volverse inservible.</summary>
    public const double MinRightPanelWidth = 380;

    /// <summary>Lo más ancho, para que no se coma la zona de edición.</summary>
    public const double MaxRightPanelWidth = 760;

    /// <summary>
    /// Ancho de la columna del lateral.
    /// </summary>
    /// <remarks>
    /// El ancho que dejó el separador la última vez, para volver a abrir con él. Lo aplica
    /// la ventana: una definición de columna no está en el árbol y no le llega un enlace.
    /// </remarks>
    public double RightPanelWidth
    {
        get => _rightPanelWidth;
        set => _rightPanelWidth = Math.Clamp(value, MinRightPanelWidth, MaxRightPanelWidth);
    }

    /// <summary>
    /// El panel del lateral que se está viendo.
    /// </summary>
    /// <remarks>
    /// Asignarlo abre ese panel; asignar <c>null</c> cierra el que estuviera delante, que
    /// es lo que hacen los formularios al aceptar o cancelar.
    /// </remarks>
    public PanelBaseViewModel? RightPanViewModel
    {
        get => _rightPanViewModel;
        set
        {
            if (value is null)
            {
                CloseRightPanel(_rightPanViewModel);
                return;
            }

            if (!RightPanels.Contains(value))
                RightPanels.Add(value);

            SetProperty(ref _rightPanViewModel, value);
        }
    }

    /// <summary>
    /// Cierra un panel del lateral.
    /// </summary>
    /// <remarks>
    /// La selección se mueve antes de quitarlo de la colección: si se quita primero, el
    /// TabControl escribe <c>null</c> de vuelta y el lateral se queda en blanco aunque
    /// queden paneles.
    /// </remarks>
    [RelayCommand]
    public void CloseRightPanel(PanelBaseViewModel? panel)
    {
        if (panel is null)
            return;

        int index = RightPanels.IndexOf(panel);
        if (index < 0)
            return;

        SetProperty(
            ref _rightPanViewModel,
            RightPanels.Count > 1 ? RightPanels[index == 0 ? 1 : index - 1] : null,
            nameof(RightPanViewModel));

        RightPanels.RemoveAt(index);
    }

    public int CurrentSpriteBankCounter { get; set; }

    public int CurrentTileSetCounter { get; set; }

    [RelayCommand]
    private void AddSpriteBank() => OpenForm(() => new EditSpriteBankViewModel(this));

    [RelayCommand]
    private void AddTileSet() => OpenForm(() => new EditTileSetViewModel(this));

    /// <summary>
    /// Abre un formulario del lateral, o trae el que ya estuviera abierto.
    /// </summary>
    /// <remarks>
    /// Sin esto, cada pulsación del botón dejaba otra pestaña igual. Se trae la que hay en
    /// vez de deshabilitar el botón: con el lateral sin el foco, un botón apagado no dice
    /// por qué y el usuario se queda mirándolo.
    /// </remarks>
    private void OpenForm<T>(Func<T> create)
        where T : PanelBaseViewModel
    {
        RightPanViewModel = RightPanels.OfType<T>().FirstOrDefault() ?? create();
    }

    /// <summary>Abre un juego de tiles en una pestaña nueva y lo cuelga del árbol.</summary>
    /// <param name="borderColorIndex">
    /// El borde guardado en el fichero. Sin él manda el del editor, que lo busca en la
    /// paleta: fijarlo aquí en el 1 daba por negro un índice que en una paleta generada
    /// a partir de un png es el primer color de la imagen.
    /// </param>
    public TileSetEditorViewModel OpenTileSet(TileSet tileSet, int? borderColorIndex = null)
    {
        var panel = new TileSetEditorViewModel(tileSet, Palettes, Preferences)
        {
            TagId = $"tls{CurrentTileSetCounter}",
            Header = $"{tileSet.Name} (TS)",
        };

        if (borderColorIndex is int border)
            panel.BorderColorIndex = border;

        // El panel de bloques nace con el juego aunque no se abra: es suyo, y asi el
        // nodo del arbol ya esta ahi con lo que trajera el fichero.
        var blocks = new TileBlocksViewModel(panel)
        {
            TagId = $"blk{CurrentTileSetCounter}",
            Header = $"Bloques de {tileSet.Name}",
        };

        CurrentTileSetCounter++;

        AddPanelToDic(panel);
        AddPanelToDic(blocks);
        Tabs.Add(panel);
        SelectedTab = panel;
        TreeGeneralVm.AddTileSet(panel.Header, panel.TagId, panel, blocks);

        return panel;
    }

    /// <summary>Abre un banco en una pestaña nueva y lo cuelga del árbol.</summary>
    /// <inheritdoc cref="OpenTileSet" path="/param[@name='borderColorIndex']"/>
    public SpritesEditorViewModel OpenSpriteBank(SpriteBank bank, int? backgroundColorIndex = null)
    {
        var panel = new SpritesEditorViewModel(bank, Palettes, Dialogs, Backgrounds, Preferences)
        {
            TagId = $"spb{CurrentSpriteBankCounter}",
            Header = $"{bank.Name} (SP)",
        };

        if (backgroundColorIndex is int background)
            panel.BackgroundColorIndex = background;

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
    private void AddPalette() => OpenPaletteEditor(Palettes.Add());

    [RelayCommand(CanExecute = nameof(CanEditPalette))]
    private void EditPalette() => OpenPaletteEditor(Palettes.ActivePalette);

    /// <summary>
    /// Abre el editor de esa paleta, sin dejar dos editores abiertos.
    /// </summary>
    /// <remarks>
    /// Si el que hay es de otra paleta se cambia por éste: dos editores de paleta a la vez
    /// no sirven para nada y se acaban apilando pestañas iguales.
    /// </remarks>
    private void OpenPaletteEditor(ColorPalette palette)
    {
        if (RightPanels.OfType<EditPaletteViewModel>().FirstOrDefault() is { } open)
        {
            if (open.Palette == palette)
            {
                RightPanViewModel = open;
                return;
            }

            CloseRightPanel(open);
        }

        RightPanViewModel = new EditPaletteViewModel(this, palette);
    }

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

    [RelayCommand(CanExecute = nameof(CanSaveTileSet))]
    private async Task SaveTileSetAsync()
    {
        if (SelectedTab is not TileSetEditorViewModel editor)
            return;

        string? path = await Dialogs.PickFileToSaveAsync(
            "Guardar juego de tiles",
            SuggestedFileName(editor.TileSet.Name));

        if (path is null)
            return;

        try
        {
            // La paleta va dentro por lo mismo que en un banco: los tiles guardan
            // indices, y sin ella se abriria con los colores que hubiera puestos.
            await File.WriteAllTextAsync(path, TileSetSerializer.Serialize(editor.TileSet, editor.ColorPalette, editor.BorderColorIndex));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo guardar el juego de tiles", exception.Message);
        }
    }

    private bool CanSaveTileSet() => SelectedTab is TileSetEditorViewModel;

    [RelayCommand]
    private async Task LoadTileSetAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync("Cargar juego de tiles");
        if (path is null)
            return;

        try
        {
            string json = await File.ReadAllTextAsync(path);
            LoadedTileSet loaded = TileSetSerializer.Deserialize(json);

            Palettes.Activate(loaded.Palette);
            OpenTileSet(loaded.TileSet, loaded.BorderColorIndex);
        }
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync("El juego de tiles no es válido", exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo abrir el fichero", exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveTileSet))]
    private Task ExportTileSetBinaryAsync() => ExportTileSetAsync(binary: true);

    [RelayCommand(CanExecute = nameof(CanSaveTileSet))]
    private Task ExportTileSetAssemblerAsync() => ExportTileSetAsync(binary: false);

    /// <summary>
    /// Escribe la tabla de patrones y la de colores. Igual que con los bancos, se pide un
    /// nombre base y de ahí salen los dos ficheros.
    /// </summary>
    private async Task ExportTileSetAsync(bool binary)
    {
        if (SelectedTab is not TileSetEditorViewModel editor)
            return;

        TileSet tileSet = editor.TileSet;
        string extension = binary ? ".bin" : ".asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            binary ? "Exportar a binario" : "Exportar a ensamblador",
            $"{SpriteBankExporter.LabelOf(tileSet.Name)}{extension}");

        if (path is null)
            return;

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);

        string patternsPath = Path.Combine(folder, $"{stem}_patterns{extension}");
        string colorsPath = Path.Combine(folder, $"{stem}_colors{extension}");

        try
        {
            if (binary)
            {
                await File.WriteAllBytesAsync(patternsPath, TileSetExporter.PatternsToBinary(tileSet));
                await File.WriteAllBytesAsync(colorsPath, TileSetExporter.ColorsToBinary(tileSet));
            }
            else
            {
                await File.WriteAllTextAsync(patternsPath, TileSetExporter.PatternsToAssembler(tileSet));
                await File.WriteAllTextAsync(colorsPath, TileSetExporter.ColorsToAssembler(tileSet));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo exportar el juego de tiles", exception.Message);

            return;
        }

        await Dialogs.ShowMessageAsync(
            "Juego de tiles exportado",
            $"Se han escrito:{Environment.NewLine}{Path.GetFileName(patternsPath)}{Environment.NewLine}{Path.GetFileName(colorsPath)}"
            + $"{Environment.NewLine}{Environment.NewLine}Recuerda copiar cada tabla {TileSetExporter.ScreenThirds} veces en VRAM, "
            + "una por tercio de pantalla.");
    }

    [RelayCommand]
    private Task ExportPaletteBinaryAsync() => ExportPaletteAsync(binary: true);

    [RelayCommand]
    private Task ExportPaletteAssemblerAsync() => ExportPaletteAsync(binary: false);

    /// <summary>
    /// Escribe la paleta activa en el formato del registro de paleta del V9938.
    /// </summary>
    /// <remarks>
    /// Un solo fichero, a diferencia de los bancos y los tilesets: son 32 bytes y no hay
    /// dos tablas que separar.
    /// </remarks>
    private async Task ExportPaletteAsync(bool binary)
    {
        ColorPalette palette = Palettes.ActivePalette;
        string extension = binary ? ".bin" : ".asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            binary ? "Exportar paleta a binario" : "Exportar paleta a ensamblador",
            $"{SpriteBankExporter.LabelOf(palette.Name)}_palette{extension}");

        if (path is null)
            return;

        try
        {
            if (binary)
                await File.WriteAllBytesAsync(path, PaletteExporter.ToBinary(palette));
            else
                await File.WriteAllTextAsync(path, PaletteExporter.ToAssembler(palette));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo exportar la paleta", exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveTileSet))]
    private async Task ExportTileSetPngAsync()
    {
        if (SelectedTab is not TileSetEditorViewModel editor)
            return;

        string? path = await Dialogs.PickFileToSaveAsync(
            "Exportar el juego de tiles a png",
            $"{SpriteBankExporter.LabelOf(editor.TileSet.Name)}.png",
            PickerFileKind.Image);

        if (path is null)
            return;

        try
        {
            PngFile.Write(
                path,
                TileSetPngConverter.ToPixels(editor.TileSet, editor.ColorPalette),
                TileSetPngConverter.FullSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync("No se pudo exportar el png", exception.Message);
        }
    }

    /// <summary>
    /// Trae un png como juego de tiles, comprobando antes que se puede.
    /// </summary>
    /// <remarks>
    /// Se rechaza en vez de aproximar en silencio: una imagen que no cumple las reglas de
    /// GRAPHIC 2 se puede convertir igualmente, pero el resultado no se parece a lo que
    /// dibujaste y no sabrías por qué. Es mejor decir dónde está el problema.
    /// </remarks>
    [RelayCommand]
    private async Task ImportTileSetPngAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync("Importar un png como juego de tiles", PickerFileKind.Image);
        if (path is null)
            return;

        int[] pixels;
        PixelSize size;

        try
        {
            (pixels, size) = PngFile.Read(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await Dialogs.ShowMessageAsync("No se pudo abrir la imagen", exception.Message);

            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);

        ColorPalette? palette = await ChoosePaletteForImportAsync(pixels, name);
        if (palette is null)
            return;

        TileSetImportResult result = TileSetPngConverter.Analyse(pixels, size, palette, name);

        if (!result.Ok)
        {
            await Dialogs.ShowMessageAsync("La imagen no se puede importar", Describe(result));

            return;
        }

        if (!ReferenceEquals(palette, Palettes.ActivePalette))
            Palettes.Activate(palette);

        OpenTileSet(result.TileSet!);
    }

    /// <summary>
    /// Decide con qué paleta se lee la imagen: la activa, o una nueva con sus colores.
    /// </summary>
    /// <remarks>
    /// Sólo se ofrece generar cuando los colores caben, y caben quince y no dieciséis: el
    /// índice 0 del VDP es transparente y darle un color haría que esos pixeles enseñaran
    /// el borde en la máquina.
    /// </remarks>
    private async Task<ColorPalette?> ChoosePaletteForImportAsync(int[] pixels, string name)
    {
        IReadOnlyList<Color> colors = TileSetPngConverter.DistinctColors(pixels);

        if (colors.Count > TileSetPngConverter.MaxGeneratedColors)
        {
            await Dialogs.ShowMessageAsync(
                "La imagen tiene demasiados colores",
                $"Trae {colors.Count} colores distintos y como mucho pueden ser "
                + $"{TileSetPngConverter.MaxGeneratedColors}, porque el color 0 del MSX está reservado para el "
                + "transparente. Reduce los colores en tu editor de imagen y vuelve a intentarlo.");

            return null;
        }

        bool? generate = await Dialogs.ChooseAsync(
            "Colores de la imagen",
            $"La imagen usa {colors.Count} colores. Puedes crear una paleta nueva con ellos, que los respeta "
            + $"tal cual, o buscar los más parecidos en la paleta activa «{Palettes.ActivePalette.Name}», que "
            + "puede cambiarlos.",
            "Crear una paleta",
            "Usar la activa");

        return generate switch
        {
            null => null,
            true => TileSetPngConverter.BuildPalette($"{name} (png)", colors),
            false => Palettes.ActivePalette,
        };
    }

    private static string Describe(TileSetImportResult result)
    {
        IEnumerable<string> lines = result.Problems
            .Take(TileSetPngConverter.MaxReportedProblems)
            .Select(problem => problem.Message);

        string text = string.Join(Environment.NewLine, lines);

        return result.Problems.Count > TileSetPngConverter.MaxReportedProblems
            ? $"{text}{Environment.NewLine}...y alguno más."
            : text;
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
        if (item is not { CanDelete: true })
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

        // Lo que cuelga del elemento se va con él: los bloques de un juego de tiles no
        // son un elemento aparte y no tienen sentido sin sus tiles.
        foreach (ItemTree child in item.Childs)
        {
            if (GetPanelFromDic(child.Tag) is { } tool)
                CloseRightPanel(tool);

            _panels.Remove(child.Tag);
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

        // Una herramienta va al lateral, para verla a la vez que lo que se edita.
        if (panel.IsTool)
        {
            RightPanViewModel = panel;
            return;
        }

        if (!Tabs.Contains(panel))
            Tabs.Add(panel);

        SelectedTab = panel;
    }
}