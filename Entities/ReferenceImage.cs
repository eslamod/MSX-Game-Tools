using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MSX_GameTools.Entities;

/// <summary>
/// Una celda de una imagen de referencia, de las que se ofrecen como fondo.
/// </summary>
/// <remarks>
/// La identidad es la ruta del fichero más el número de celda, no la posición en la
/// lista: es lo que se guarda en el banco, y así borrar una imagen no descoloca las
/// referencias de las demás.
/// </remarks>
public sealed class ReferenceTile
{
    internal ReferenceTile(ReferenceImage source, int index, IImage image, PixelSize size)
    {
        Source = source;
        Index = index;
        Image = image;
        Size = size;
    }

    public ReferenceImage Source { get; }

    /// <summary>Posición dentro de su imagen, de izquierda a derecha y de arriba abajo.</summary>
    public int Index { get; }

    public IImage Image { get; }

    /// <summary>
    /// Tamaño en pixeles, que no siempre es el de la celda: las del borde salen
    /// recortadas. Hace falta para centrarla en el lienzo a escala 1:1.
    /// </summary>
    public PixelSize Size { get; }

    /// <summary>El tamaño otra vez, en doubles, que es lo que enlaza la vista.</summary>
    public double PixelWidth => Size.Width;

    /// <inheritdoc cref="PixelWidth"/>
    public double PixelHeight => Size.Height;

    /// <summary>Número con el que aparece en la lista. Lo reparte la biblioteca.</summary>
    public int Number { get; internal set; }

    public string Name => $"Background {Number}";

    /// <summary>Columna dentro de la retícula de su imagen.</summary>
    public int Column => Index % Source.Columns;

    /// <summary>Fila dentro de la retícula de su imagen.</summary>
    public int Row => Index / Source.Columns;

    /// <summary>
    /// Cómo se nombra la celda elegida al lado del desplegable de imágenes: la posición
    /// en la retícula, que es lo que el usuario acaba de señalar con el ratón.
    /// </summary>
    public string Label => Source.IsSheet
        ? $"{System.IO.Path.GetFileName(Source.Path)} · {Column},{Row}"
        : System.IO.Path.GetFileName(Source.Path);

    /// <summary>Cómo lo guarda un grupo o un patrón que use esta celda.</summary>
    public BackgroundRef Ref => new(Source.Path, Index);
}

/// <summary>
/// Un png o jpg cargado como referencia, ya repartido en celdas.
/// </summary>
public sealed class ReferenceImage
{
    private readonly Bitmap _source;

    private ReferenceImage(string path, int cellSize, Bitmap source)
    {
        Path = path;
        CellSize = cellSize;
        _source = source;

        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(source.PixelSize, cellSize);
        var tiles = new List<ReferenceTile>(cells.Count);

        for (int index = 0; index < cells.Count; index++)
        {
            PixelRect cell = cells[index];

            // Una celda que cubre la imagen entera no necesita recorte, y así el caso de
            // la imagen pequeña no arrastra un envoltorio para nada.
            IImage image = cell.Size == source.PixelSize
                ? source
                : new CroppedBitmap(source, cell);

            tiles.Add(new ReferenceTile(this, index, image, cell.Size));
        }

        Tiles = tiles;

        // Las columnas salen de las propias celdas y no de dividir el ancho: así vale
        // igual cuando la imagen entra de una pieza y cuando la última columna está
        // recortada.
        Columns = Math.Max(1, cells.Count(cell => cell.Y == 0));
        Rows = cells.Count / Columns;
    }

    /// <summary>Ruta del fichero, tal cual se cargó. Es parte de la identidad de las celdas.</summary>
    public string Path { get; }

    /// <summary>
    /// Lado de celda con el que se troceó. Cero cuando la imagen entró de una pieza por
    /// caber en <see cref="ReferenceImageSlicer.SingleTileMax"/>.
    /// </summary>
    public int CellSize { get; }

    public IReadOnlyList<ReferenceTile> Tiles { get; }

    /// <summary>Columnas de la retícula. Uno cuando la imagen no se troceó.</summary>
    public int Columns { get; }

    /// <summary>Filas de la retícula. Uno cuando la imagen no se troceó.</summary>
    public int Rows { get; }

    /// <summary>
    /// Si hay que preguntar qué celda. Con una sola no hay nada que elegir: esa imagen
    /// <i>es</i> el fondo.
    /// </summary>
    public bool IsSheet => Tiles.Count > 1;

    public PixelSize Size => _source.PixelSize;

    /// <summary>La imagen entera, para enseñar la retícula encima al elegir celda.</summary>
    public IImage Source => _source;

    /// <summary>
    /// Lo que se ve en la lista de imágenes. Lleva el número de fondos porque es lo que
    /// se va a llevar por delante al borrarla.
    /// </summary>
    public string DisplayName => $"{System.IO.Path.GetFileName(Path)} ({Tiles.Count})";

    /// <summary>
    /// Tamaño de un fichero sin quedárselo. Hace falta para saber si hay que preguntar
    /// el tamaño de celda antes de cargarlo, y decodificar dos veces una imagen que se
    /// carga a mano de tarde en tarde sale más barato que arrastrar un bitmap a medias.
    /// </summary>
    public static PixelSize Measure(string path)
    {
        using var bitmap = new Bitmap(path);

        return bitmap.PixelSize;
    }

    public static ReferenceImage Load(string path, int cellSize)
    {
        var source = new Bitmap(path);

        // Si cabe entera, el tamaño de celda que se pidiera da igual y guardarlo mentiría
        // sobre cómo se troceó.
        bool single = source.PixelSize.Width <= ReferenceImageSlicer.SingleTileMax
            && source.PixelSize.Height <= ReferenceImageSlicer.SingleTileMax;

        return new ReferenceImage(path, single ? 0 : cellSize, source);
    }
}
