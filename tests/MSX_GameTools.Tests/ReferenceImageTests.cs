using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.Entities;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Troceado de las imágenes de referencia que sirven de fondo a los grupos.
/// </summary>
public class ReferenceImageSlicerTests
{
    [AvaloniaFact]
    public void Una_imagen_que_cabe_en_el_lienzo_del_grupo_entra_de_una_pieza()
    {
        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(new PixelSize(46, 46), cellSize: 16);

        // El tamaño de celda se ignora: no hay nada que trocear.
        Assert.Single(cells);
        Assert.Equal(new PixelRect(0, 0, 46, 46), cells[0]);
    }

    [AvaloniaFact]
    public void Una_imagen_mas_pequena_conserva_su_tamano()
    {
        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(new PixelSize(20, 12), cellSize: 16);

        Assert.Single(cells);
        Assert.Equal(new PixelRect(0, 0, 20, 12), cells[0]);
    }

    /// <summary>
    /// La condición es que quepa por los dos lados. Una tira larga y baja se trocea
    /// aunque sea de 20 pixeles de alto, que si no cada fondo sería la tira entera.
    /// </summary>
    [AvaloniaFact]
    public void Basta_con_pasarse_por_un_lado_para_que_se_trocee()
    {
        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(new PixelSize(200, 20), cellSize: 16);

        // 13 columnas (12 enteras y una de 8) x 2 filas (una de 16 y otra de 4).
        Assert.Equal(26, cells.Count);
        Assert.Equal(new PixelSize(16, 16), cells[0].Size);
        Assert.Equal(new PixelSize(8, 16), cells[12].Size);
        Assert.Equal(new PixelSize(8, 4), cells[25].Size);
    }

    [AvaloniaFact]
    public void Una_hoja_multiplo_del_tamano_de_celda_sale_exacta()
    {
        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(new PixelSize(64, 48), cellSize: 16);

        Assert.Equal(12, cells.Count); // 4 columnas x 3 filas
        Assert.All(cells, cell => Assert.Equal(new PixelSize(16, 16), cell.Size));

        // De izquierda a derecha y de arriba abajo.
        Assert.Equal(new PixelRect(0, 0, 16, 16), cells[0]);
        Assert.Equal(new PixelRect(16, 0, 16, 16), cells[1]);
        Assert.Equal(new PixelRect(0, 16, 16, 16), cells[4]);
    }

    /// <summary>
    /// Lo que sobra en los bordes se coge recortado en vez de tirarlo: en una hoja de
    /// sprites el último trozo suele ser justo el que hace falta.
    /// </summary>
    [AvaloniaFact]
    public void Los_bordes_que_no_llegan_a_la_celda_entera_salen_recortados()
    {
        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(new PixelSize(50, 40), cellSize: 16);

        Assert.Equal(12, cells.Count); // 4 columnas (16,16,16,2) x 3 filas (16,16,8)

        Assert.Equal(new PixelRect(48, 0, 2, 16), cells[3]);    // margen derecho
        Assert.Equal(new PixelRect(0, 32, 16, 8), cells[8]);    // margen inferior
        Assert.Equal(new PixelRect(48, 32, 2, 8), cells[11]);   // la esquina, recortada dos veces
    }

    [AvaloniaFact]
    public void Una_celda_mayor_que_la_imagen_deja_una_sola_celda_con_la_imagen_entera()
    {
        IReadOnlyList<PixelRect> cells = ReferenceImageSlicer.Slice(new PixelSize(100, 60), cellSize: 128);

        Assert.Single(cells);
        Assert.Equal(new PixelRect(0, 0, 100, 60), cells[0]);
    }

    [AvaloniaFact]
    public void Un_tamano_de_celda_invalido_no_pasa_desapercibido()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => ReferenceImageSlicer.Slice(new PixelSize(100, 100), cellSize: 0));

    [AvaloniaFact]
    public void Una_imagen_vacia_no_da_celdas()
        => Assert.Empty(ReferenceImageSlicer.Slice(new PixelSize(0, 0), cellSize: 16));
}

/// <summary>
/// Carga de ficheros reales y la biblioteca. Necesita Skia, así que va con AvaloniaFact.
/// </summary>
public class ReferenceImageLibraryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxref-{Guid.NewGuid():N}");

    public ReferenceImageLibraryTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Una_hoja_se_carga_con_una_celda_por_trozo()
    {
        string path = WritePng("hoja.png", 50, 40);

        ReferenceImage image = ReferenceImage.Load(path, cellSize: 16);

        Assert.Equal(12, image.Tiles.Count);
        Assert.Equal(16, image.CellSize);
        Assert.Equal(new PixelSize(2, 16), image.Tiles[3].Size);
        Assert.Equal(new PixelSize(2, 8), image.Tiles[11].Size);

        // El índice es la identidad de la celda dentro de su imagen.
        Assert.Equal([.. Enumerable.Range(0, 12)], image.Tiles.Select(tile => tile.Index));
    }

    [AvaloniaFact]
    public void Una_imagen_pequena_no_guarda_tamano_de_celda()
    {
        string path = WritePng("pequena.png", 30, 30);

        ReferenceImage image = ReferenceImage.Load(path, cellSize: 16);

        // Entró de una pieza, así que decir que se troceó a 16 sería mentira.
        Assert.Single(image.Tiles);
        Assert.Equal(0, image.CellSize);
    }

    [AvaloniaFact]
    public void Los_fondos_se_numeran_seguidos_entre_imagenes()
    {
        var library = new ReferenceImageLibrary();

        library.Load(WritePng("una.png", 64, 16), cellSize: 32);   // 2 celdas: 64 de ancho se pasa
        library.Load(WritePng("otra.png", 20, 20), cellSize: 16);  // 1 celda, cabe entera

        Assert.Equal(3, library.Tiles.Count);
        Assert.Equal(["Background 1", "Background 2", "Background 3"], library.Tiles.Select(t => t.Name));
    }

    [AvaloniaFact]
    public void Borrar_una_imagen_se_lleva_todos_sus_fondos()
    {
        var library = new ReferenceImageLibrary();

        ReferenceImage first = library.Load(WritePng("una.png", 64, 16), cellSize: 16); // 4 celdas
        library.Load(WritePng("otra.png", 64, 16), cellSize: 32);                       // 2 celdas

        Assert.Equal(6, library.Tiles.Count);

        library.Remove(first);

        Assert.Single(library.Images);
        Assert.Equal(2, library.Tiles.Count);
        Assert.All(library.Tiles, tile => Assert.NotSame(first, tile.Source));
    }

    /// <summary>
    /// Los números no se reaprovechan: si al borrar volviera a empezar la cuenta, un banco
    /// guardado apuntaría a un fondo con el mismo nombre pero otro dibujo.
    /// </summary>
    [AvaloniaFact]
    public void Los_numeros_no_se_reaprovechan_al_borrar()
    {
        var library = new ReferenceImageLibrary();

        ReferenceImage first = library.Load(WritePng("una.png", 64, 16), cellSize: 32); // 2 celdas
        library.Remove(first);
        library.Load(WritePng("otra.png", 20, 20), cellSize: 16);

        Assert.Equal("Background 3", library.Tiles[0].Name);
    }

    [AvaloniaFact]
    public void Una_celda_se_encuentra_por_ruta_e_indice()
    {
        var library = new ReferenceImageLibrary();
        string path = WritePng("hoja.png", 64, 16), other = WritePng("otra.png", 64, 16);

        library.Load(path, cellSize: 16);
        library.Load(other, cellSize: 16);

        ReferenceTile? found = library.Find(path, 2);

        Assert.NotNull(found);
        Assert.Equal(2, found.Index);
        Assert.Equal(path, found.Source.Path);
    }

    [AvaloniaFact]
    public void Buscar_una_celda_que_ya_no_esta_devuelve_null()
    {
        var library = new ReferenceImageLibrary();
        string path = WritePng("hoja.png", 64, 16);

        ReferenceImage image = library.Load(path, cellSize: 16);
        library.Remove(image);

        // Es lo que pasa al abrir un banco cuyo png se ha movido: no se revienta, no hay fondo.
        Assert.Null(library.Find(path, 0));
    }

    /// <summary>Un png liso del tamaño pedido, que para el troceado es lo único que importa.</summary>
    private string WritePng(string name, int width, int height)
    {
        string path = Path.Combine(_folder, name);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        bitmap.Save(path, new PngBitmapEncoderOptions());

        return path;
    }
}
