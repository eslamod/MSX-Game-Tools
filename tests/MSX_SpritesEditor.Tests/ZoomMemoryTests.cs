using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using MSX_SpritesEditor.Views;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// El zoom sobrevive al cambio de pestaña.
/// </summary>
/// <remarks>
/// El TabControl reconstruye la vista cada vez que se cambia de pestaña, asi que sin
/// guardarlo el zoom volvia a X1 constantemente. Se guarda por tipo de editor y no por
/// pestaña: lo que se quiere es seguir donde estabas.
/// </remarks>
public class ZoomMemoryTests
{
    [AvaloniaFact]
    public void El_zoom_de_sprites_sobrevive_a_montar_otra_vista()
    {
        var preferences = new EditorPreferences();

        double canvas = 0;
        double thumbnails = 0;

        WithSpriteView(preferences, view =>
        {
            Click(view, "EditZoom", "2");
            Click(view, "PreviewZoom", "4");

            canvas = view.CellSize;
            thumbnails = view.ThumbnailSize;
        });

        Assert.NotEqual(256d / 16, canvas);

        // Una vista nueva, como la que fabrica el TabControl al volver a la pestaña.
        WithSpriteView(preferences, view =>
        {
            Assert.Equal(canvas, view.CellSize);
            Assert.Equal(thumbnails, view.ThumbnailSize);
        });
    }

    [AvaloniaFact]
    public void El_zoom_de_tilesets_sobrevive_a_montar_otra_vista()
    {
        var preferences = new EditorPreferences();

        double canvas = 0;
        double thumbnails = 0;

        WithTileSetView(preferences, view =>
        {
            Click(view, "TileZoom", "2");
            Click(view, "TilePreviewZoom", "3");

            canvas = view.CellSize;
            thumbnails = view.ThumbnailSize;
        });

        WithTileSetView(preferences, view =>
        {
            Assert.Equal(canvas, view.CellSize);
            Assert.Equal(thumbnails, view.ThumbnailSize);
        });
    }

    /// <summary>
    /// Un editor sin preferencias compartidas arranca en X1. Es lo que garantiza que un
    /// test no se encuentre el zoom que dejo otro: si esto fuera estatico, lo heredaria.
    /// </summary>
    [AvaloniaFact]
    public void Sin_preferencias_compartidas_se_arranca_en_X1()
    {
        WithSpriteView(new EditorPreferences(), view => Assert.Equal(256d / 16, view.CellSize));
        WithTileSetView(new EditorPreferences(), view => Assert.Equal(256d / 8, view.CellSize));
    }

    private static void Click(Control view, string group, string tag) =>
        view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(r => r.GroupName == group && (string?)r.Tag == tag)
            .IsChecked = true;

    private static void WithSpriteView(EditorPreferences preferences, Action<SpritesEditorView> act)
    {
        var vm = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), new PaletteLibrary(), null, null, preferences);

        Mount(new SpritesEditorView { DataContext = vm }, act);
    }

    private static void WithTileSetView(EditorPreferences preferences, Action<TileSetEditorView> act)
    {
        var vm = new TileSetEditorViewModel(new TileSet("Bosque"), new PaletteLibrary(), preferences);

        Mount(new TileSetEditorView { DataContext = vm }, act);
    }

    private static void Mount<T>(T view, Action<T> act)
        where T : Control
    {
        var window = new Window { Content = view, Width = 1000, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        act(view);
        Dispatcher.UIThread.RunJobs();
    }
}
