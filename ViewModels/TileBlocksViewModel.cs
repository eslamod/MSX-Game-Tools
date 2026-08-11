using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Panel de bloques de un juego de tiles: la lista de bloques, la rejilla donde se
/// componen y el selector de tiles.
/// </summary>
/// <remarks>
/// <para>
/// Es satélite del editor de ese juego y no un panel suelto. De ahí saca los tiles ya
/// dibujados, así que pintar un tile en el editor se ve al momento en los bloques, que es
/// justo para lo que sirve: componer el conjunto mientras se dibujan las piezas.
/// </para>
/// <para>
/// Un bloque son números de tile, y esos números no significan nada sin saber de qué
/// juego son. Por eso hay un panel por juego y no uno global.
/// </para>
/// </remarks>
public partial class TileBlocksViewModel : PanelBaseViewModel
{
    private readonly TileSetEditorViewModel _editor;

    /// <summary>El bloque al que se le está escuchando el tamaño.</summary>
    private TileBlockViewModel? _watched;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBlock))]
    [NotifyCanExecuteChangedFor(nameof(DeleteBlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(FitToContentCommand))]
    private TileBlockViewModel? _selectedBlock;

    /// <summary>
    /// Lo que se estampa al pulsar en la rejilla. Nunca es nulo: al abrir el panel ya
    /// está cogido el primer tile, para que pulsar haga algo desde el principio.
    /// </summary>
    [ObservableProperty]
    private TilePatch _selection = TilePatch.Single(0);

    public TileBlocksViewModel(TileSetEditorViewModel editor)
    {
        _editor = editor;

        for (int index = 0; index < editor.Thumbnails.Count; index++)
            Tiles.Add(new TileChoiceViewModel(index, editor.Thumbnails[index]));

        Tiles[0].IsSelected = true;

        for (int row = 0; row < TileBlock.MaxSide; row++)
        {
            for (int column = 0; column < TileBlock.MaxSide; column++)
                Cells.Add(new BlockCellViewModel(column, row));
        }

        foreach (TileBlock block in editor.TileSet.Blocks)
            Blocks.Add(new TileBlockViewModel(block));

        SelectedBlock = Blocks.FirstOrDefault();
    }

    /// <summary>Se ve a la vez que el editor de tiles, así que va en el panel lateral.</summary>
    public override bool IsTool => true;

    public TileSet TileSet => _editor.TileSet;

    /// <summary>Zoom y demás ajustes que sobreviven al cambio de pestaña.</summary>
    public EditorPreferences Preferences => _editor.Preferences;

    /// <summary>Los bloques definidos, que es el área de arriba.</summary>
    public ObservableCollection<TileBlockViewModel> Blocks { get; } = [];

    /// <summary>Las 16x16 celdas donde se compone. Siempre están todas.</summary>
    public ObservableCollection<BlockCellViewModel> Cells { get; } = [];

    /// <summary>Los 256 tiles del juego, para elegir.</summary>
    public ObservableCollection<TileChoiceViewModel> Tiles { get; } = [];

    public bool HasBlock => SelectedBlock is not null;

    /// <summary>Lo que mide el bloque, para ver el efecto de tocar el tamaño.</summary>
    public string SizeLabel => SelectedBlock is { } block ? $"{block.Width} x {block.Height}" : string.Empty;

    public int GridSide => TileBlock.MaxSide;

    /// <summary>Tiles por fila del selector, la misma disposición que el editor y el png.</summary>
    public int TilesPerRow => 32;

    [RelayCommand]
    private void AddBlock()
    {
        var block = new TileBlock(NextAvailableName());

        TileSet.Blocks.Add(block);

        var panel = new TileBlockViewModel(block);

        Blocks.Add(panel);
        SelectedBlock = panel;
    }

    [RelayCommand(CanExecute = nameof(HasBlock))]
    private void DeleteBlock()
    {
        if (SelectedBlock is null)
            return;

        int index = Blocks.IndexOf(SelectedBlock);

        TileSet.Blocks.Remove(SelectedBlock.Block);

        // Antes de quitarlo de la coleccion: si no, el ListBox escribe null de vuelta
        // y la seleccion se queda perdida.
        SelectedBlock = Blocks.Count > 1 ? Blocks[index == 0 ? 1 : index - 1] : null;

        Blocks.RemoveAt(index);
    }

    /// <summary>
    /// Encoge el bloque hasta lo que ocupan los tiles puestos.
    /// </summary>
    /// <remarks>
    /// El tamaño no se deduce solo al borrar, que dejaría un supertile de 2x2 con la
    /// esquina vacía convertido en 2x1. Cuando de verdad sobra sitio, se pide con esto.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasBlock))]
    private void FitToContent()
    {
        if (SelectedBlock is not { } block)
            return;

        (int width, int height) = block.Block.UsedSize();

        // Un bloque vacío se queda en una celda: el tamaño mínimo, no cero.
        block.Width = Math.Max(1, width);
        block.Height = Math.Max(1, height);
    }

    /// <summary>Coge un tile suelto, que es un trozo de una celda.</summary>
    public void SelectTile(int index) => SelectRange(index, index);

    /// <summary>
    /// Coge el rectángulo que va de un tile a otro.
    /// </summary>
    /// <remarks>
    /// El rectángulo se calcula sobre las 32 columnas del selector, que son las mismas
    /// del editor y las del png. Si el selector repartiera los tiles según el ancho
    /// disponible, un árbol dibujado en tres filas dejaría de ser un rectángulo.
    /// </remarks>
    public void SelectRange(int from, int to)
    {
        if ((uint)from >= (uint)Tiles.Count || (uint)to >= (uint)Tiles.Count)
            return;

        int left = Math.Min(from % TilesPerRow, to % TilesPerRow);
        int right = Math.Max(from % TilesPerRow, to % TilesPerRow);
        int top = Math.Min(from / TilesPerRow, to / TilesPerRow);
        int bottom = Math.Max(from / TilesPerRow, to / TilesPerRow);

        var patch = new TilePatch(right - left + 1, bottom - top + 1);

        foreach (TileChoiceViewModel tile in Tiles)
            tile.IsSelected = false;

        for (int row = 0; row < patch.Height; row++)
        {
            for (int column = 0; column < patch.Width; column++)
            {
                int index = ((top + row) * TilesPerRow) + left + column;

                patch[column, row] = index;
                Tiles[index].IsSelected = true;
            }
        }

        Selection = patch;
    }

    /// <summary>Estampa lo que haya cogido con la esquina en esa celda.</summary>
    public void Paint(int column, int row)
    {
        if (SelectedBlock is null)
            return;

        SelectedBlock.Block.Stamp(column, row, Selection);
        SelectedBlock.RefreshSize();

        RefreshCells();
    }

    /// <summary>Vacía una celda, que no es lo mismo que ponerle el tile 0.</summary>
    public void Erase(int column, int row)
    {
        if (SelectedBlock is null)
            return;

        SelectedBlock.Block.Set(column, row, null);

        RefreshCells();
    }

    /// <summary>Vuelve a leer del bloque lo que enseña cada celda.</summary>
    public void RefreshCells()
    {
        TileBlock? block = SelectedBlock?.Block;

        foreach (BlockCellViewModel cell in Cells)
        {
            bool inside = block is not null && block.Contains(cell.Column, cell.Row);
            int? tile = block?[cell.Column, cell.Row];

            cell.IsInside = inside;
            cell.Image = tile is int index ? _editor.Thumbnails[index] : null;
        }
    }

    /// <summary>
    /// El bloque avisa de su tamaño, y la rejilla tiene que enterarse.
    /// </summary>
    /// <remarks>
    /// Cambiar el ancho o el alto a mano mueve el límite de lo que está dentro del bloque
    /// y puede vaciar celdas. Sin escuchar al bloque, eso no se veía hasta cambiar de
    /// bloque y volver, y tocar el tamaño no daba ninguna señal de estar haciendo algo.
    /// </remarks>
    partial void OnSelectedBlockChanged(TileBlockViewModel? value)
    {
        if (_watched is not null)
            _watched.PropertyChanged -= OnBlockChanged;

        _watched = value;

        if (_watched is not null)
            _watched.PropertyChanged += OnBlockChanged;

        OnPropertyChanged(nameof(SizeLabel));
        RefreshCells();
    }

    private void OnBlockChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(TileBlockViewModel.Width) or nameof(TileBlockViewModel.Height)))
            return;

        OnPropertyChanged(nameof(SizeLabel));
        RefreshCells();
    }

    /// <summary>«Bloque 3», saltándose los números que ya estén cogidos.</summary>
    private string NextAvailableName()
    {
        for (int number = 1; ; number++)
        {
            string name = $"Bloque {number}";

            if (!Blocks.Any(block => block.Name == name))
                return name;
        }
    }
}

/// <summary>Un bloque en la lista del panel.</summary>
public partial class TileBlockViewModel : ObservableObject
{
    public TileBlockViewModel(TileBlock block) => Block = block;

    public TileBlock Block { get; }

    public string Name
    {
        get => Block.Name;
        set
        {
            if (Block.Name == value)
                return;

            Block.Name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>El tamaño se puede fijar a mano además de crecer al pintar.</summary>
    public int Width
    {
        get => Block.Width;
        set
        {
            Block.Width = value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc cref="Width"/>
    public int Height
    {
        get => Block.Height;
        set
        {
            Block.Height = value;
            OnPropertyChanged();
        }
    }

    /// <summary>El bloque ha crecido solo al pintar fuera.</summary>
    public void RefreshSize()
    {
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
    }

    public override string ToString() => Name;
}

/// <summary>Una celda de la rejilla donde se compone el bloque.</summary>
public partial class BlockCellViewModel : ObservableObject
{
    /// <summary>Si la celda cae dentro del tamaño del bloque.</summary>
    /// <remarks>
    /// La rejilla se enseña siempre entera para poder pintar fuera y que el bloque
    /// crezca, así que hace falta ver hasta dónde llega de verdad.
    /// </remarks>
    [ObservableProperty]
    private bool _isInside;

    [ObservableProperty]
    private ImageMini? _image;

    public BlockCellViewModel(int column, int row)
    {
        Column = column;
        Row = row;
    }

    public int Column { get; }

    public int Row { get; }
}

/// <summary>Un tile del juego en el selector de abajo.</summary>
public partial class TileChoiceViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public TileChoiceViewModel(int index, ImageMini image)
    {
        Index = index;
        Image = image;
    }

    public int Index { get; }

    public ImageMini Image { get; }
}
