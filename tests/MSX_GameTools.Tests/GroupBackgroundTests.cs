using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Elegir una imagen de referencia como fondo de un grupo o de un patrón, y lo que eso
/// cambia en cómo se dibuja la composición.
/// </summary>
public class GroupBackgroundTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxgbg-{Guid.NewGuid():N}");

    public GroupBackgroundTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Sin_imagenes_cargadas_los_controles_de_fondo_no_se_ensenan()
    {
        var editor = NewEditor(out _);

        Assert.False(editor.HasBackgrounds);
    }

    [AvaloniaFact]
    public void Cargar_una_imagen_hace_aparecer_los_controles()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);

        library.Load(WritePng("fondo.png", 30, 30), cellSize: 0);

        Assert.True(editor.HasBackgrounds);
    }

    [AvaloniaFact]
    public void El_fondo_elegido_se_guarda_en_el_grupo_como_ruta_y_celda()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        string path = WritePng("hoja.png", 64, 16);
        library.Load(path, cellSize: 16);

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;

        group.BackgroundTile = library.Tiles[2];

        Assert.Equal(new BackgroundRef(path, 2), group.Group.Background);

        // Y de vuelta: el grupo guarda el puntero, la vista resuelve la celda.
        Assert.Same(library.Tiles[2], group.BackgroundTile);
    }

    [AvaloniaFact]
    public void Quitar_el_fondo_deja_el_grupo_sin_referencia()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        library.Load(WritePng("hoja.png", 64, 16), cellSize: 16);

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;

        group.BackgroundTile = library.Tiles[0];
        group.BackgroundTile = null;

        Assert.Equal(BackgroundRef.None, group.Group.Background);
        Assert.Null(group.BackgroundTile);
    }

    /// <summary>
    /// La clave de que esto funcione sin tocar el compositor: con referencia, el hueco
    /// deja de pintarse del color de fondo y pasa a ser transparente, y así la miniatura
    /// se puede superponer a la imagen.
    /// </summary>
    [AvaloniaFact]
    public void Con_referencia_el_hueco_de_la_composicion_queda_transparente()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        library.Load(WritePng("hoja.png", 64, 16), cellSize: 16);

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;

        // La esquina del lienzo de 46x46 nunca la cubre un sprite: ahí se ve el hueco.
        Assert.Equal(255, Alpha(group.Preview, 0, 0));

        group.BackgroundTile = library.Tiles[0];

        Assert.Equal(0, Alpha(group.Preview, 0, 0));
    }

    [AvaloniaFact]
    public void Sin_referencia_el_hueco_vuelve_al_color_de_fondo()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        library.Load(WritePng("hoja.png", 64, 16), cellSize: 16);

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;

        group.BackgroundTile = library.Tiles[0];
        group.BackgroundTile = null;

        Assert.Equal(255, Alpha(group.Preview, 0, 0));
        Assert.Equal(ToBgra(editor.BackgroundColor.Color), PixelReader.At(group.Preview, 0, 0));
    }

    /// <summary>
    /// Los pixeles del sprite siguen siendo opacos: lo que se ve del fondo es el hueco,
    /// no el dibujo a medias. Rebajar el sprite entero es cosa del slider de opacidad,
    /// que actúa sobre el control y no sobre los pixeles.
    /// </summary>
    [AvaloniaFact]
    public void Los_pixeles_encendidos_siguen_siendo_opacos_con_referencia()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        library.Load(WritePng("hoja.png", 64, 16), cellSize: 16);

        // Una línea encendida en el patrón, con un color que no sea el 0.
        editor.SpritesBank.SpritesList[0].ArraySpriteRows[0].Color = 15;
        editor.SpritesBank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;
        group.BackgroundTile = library.Tiles[0];

        // Donde caiga el desplazamiento cero, que sale de lo que ocupa el grupo.
        (_, _, int originX, int originY) = SpriteGroupRenderer.CanvasOf(group.Group);

        Assert.Equal(255, Alpha(group.Preview, originX, originY));
    }

    [AvaloniaFact]
    public void La_opacidad_arranca_al_maximo()
    {
        SpritesEditorViewModel editor = NewEditor(out _);
        editor.AddGroupCommand.Execute(null);

        Assert.Equal(1.0, editor.SelectedGroup!.SpriteOpacity);
        Assert.Equal(1.0, editor.PatternOpacity);
    }

    [AvaloniaFact]
    public void El_patron_lleva_su_propio_fondo_independiente_del_grupo()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        string path = WritePng("hoja.png", 64, 16);
        library.Load(path, cellSize: 16);

        editor.AddGroupCommand.Execute(null);
        editor.SelectedGroup!.BackgroundTile = library.Tiles[0];

        editor.PatternBackgroundTile = library.Tiles[3];

        // Son dos preguntas distintas y no se pisan: la escena detrás de la composición
        // y el dibujo que se está calcando en el sprite.
        Assert.Equal(new BackgroundRef(path, 3), editor.CurrentSprite.Background);
        Assert.Equal(new BackgroundRef(path, 0), editor.SelectedGroup.Group.Background);
    }

    [AvaloniaFact]
    public void Borrar_la_imagen_deja_al_grupo_sin_fondo_pero_no_lo_rompe()
    {
        SpritesEditorViewModel editor = NewEditor(out ReferenceImageLibrary library);
        ReferenceImage image = library.Load(WritePng("hoja.png", 64, 16), cellSize: 16);

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;
        group.BackgroundTile = library.Tiles[0];

        library.Remove(image);

        // El puntero sigue guardado, simplemente ya no resuelve. Si la imagen se vuelve
        // a cargar, el fondo reaparece solo.
        Assert.Null(group.BackgroundTile);
        Assert.True(group.Group.Background.HasValue);

        // Y la composición vuelve a pintar el hueco opaco, que si no se vería el gris
        // del contenedor a través de un fondo que ya no existe.
        Assert.Equal(255, Alpha(group.Preview, 0, 0));
    }

    /// <summary>Canal alfa del pixel, que es lo que distingue hueco de hueco pintado.</summary>
    private static int Alpha(ImageMini mini, int x, int y) => (int)((uint)PixelReader.At(mini, x, y) >> 24);

    private static int ToBgra(Color color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;

    private SpritesEditorViewModel NewEditor(out ReferenceImageLibrary library)
    {
        library = new ReferenceImageLibrary();

        return new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard(), null, library);
    }

    private string WritePng(string name, int width, int height)
    {
        string path = Path.Combine(_folder, name);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        bitmap.Save(path, new PngBitmapEncoderOptions());

        return path;
    }
}
