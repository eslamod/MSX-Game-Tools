using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using MSX_SpritesEditor.Views;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Lo que acaba en pantalla, no lo que dice una propiedad. Es la única forma de zanjar
/// si la opacidad se está aplicando: que el enlace llegue al control no garantiza que
/// el resultado se vea distinto.
/// </summary>
public class GroupOpacityRenderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxop-{Guid.NewGuid():N}");

    public GroupOpacityRenderTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Bajar_la_opacidad_deja_ver_la_referencia_a_traves_del_sprite()
    {
        var library = new ReferenceImageLibrary();
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        // Un sprite macizo y blanco sobre una referencia verde: cualquier mezcla se nota.
        foreach (SpriteRow row in bank.SpritesList[0].ArraySpriteRows)
        {
            row.Color = 15;
            Array.Fill(row.ArrayColumns, true);
        }

        var vm = new SpritesEditorViewModel(bank, new PaletteLibrary(), null, library);
        var view = new SpritesEditorView { DataContext = vm };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.ThumbnailMode = ThumbnailMode.Groups;
        vm.AddGroupCommand.Execute(null);
        vm.SelectedGroup!.BackgroundTile = library.Load(SolidPng("verde.png", 40, 40, Colors.Lime), 0).Tiles[0];
        Dispatcher.UIThread.RunJobs();

        Color opaque = CentreOfThumbnail(window, view);

        vm.SelectedGroup.SpriteOpacity = 0.25;
        Dispatcher.UIThread.RunJobs();

        Color faded = CentreOfThumbnail(window, view);

        // A tope se ve el sprite blanco; al bajarlo tiene que entrar el verde de debajo.
        Assert.True(opaque.G - opaque.R < 40, $"El sprite deberia verse blanco y salio {opaque}.");
        Assert.True(faded.G - faded.R > 60, $"Al 25% deberia verse el verde de debajo y salio {faded}.");
    }

    /// <summary>Color del pixel central de la miniatura del grupo, en la ventana ya pintada.</summary>
    private static Color CentreOfThumbnail(Window window, SpritesEditorView view)
    {
        ListBox groupList = view.FindControl<ListBox>("GroupList")!;

        Point centre = groupList.TranslatePoint(new Point(0, 0), window)
                       ?? throw new InvalidOperationException("La tira de grupos no está en el árbol visual.");

        using WriteableBitmap frame = window.CaptureRenderedFrame()
                                     ?? throw new InvalidOperationException("No se pudo capturar el fotograma.");

        // El centro de la primera miniatura: el ListBox tiene 4 de padding y el contenedor 3
        // de margen y 3 de borde, y la miniatura mide GroupThumbnailSize.
        var point = new PixelPoint(
            (int)(centre.X + 10 + (view.GroupThumbnailSize / 2)),
            (int)(centre.Y + 10 + (view.GroupThumbnailSize / 2)));

        using ILockedFramebuffer buffer = frame.Lock();

        int[] row = new int[buffer.Size.Width];
        Marshal.Copy(buffer.Address + (point.Y * buffer.RowBytes), row, 0, row.Length);

        int bgra = row[point.X];

        return Color.FromArgb(
            (byte)((uint)bgra >> 24), (byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF));
    }

    private string SolidPng(string name, int width, int height, Color color)
    {
        string path = Path.Combine(_folder, name);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        int value = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        int[] pixels = new int[width];
        Array.Fill(pixels, value);

        using (ILockedFramebuffer buffer = bitmap.Lock())
        {
            for (int y = 0; y < height; y++)
                Marshal.Copy(pixels, 0, buffer.Address + (y * buffer.RowBytes), width);
        }

        bitmap.Save(path);

        return path;
    }
}
