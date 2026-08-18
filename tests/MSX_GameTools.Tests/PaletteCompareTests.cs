using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Views;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Comparar la paleta que se edita con otra, ranura a ranura.
/// </summary>
/// <remarks>
/// Dentro del editor y no en un panel aparte: comparar sirve para mover colores hasta que dos
/// paletas se parezcan, y mover colores es justo lo que hace este panel. En uno aparte habría
/// que ir y volver a cada arrastre.
/// </remarks>
public class PaletteCompareTests
{
    /// <summary>Se compara por índice, y se marca en cuáles ya coinciden.</summary>
    [AvaloniaFact]
    public void Se_marca_en_que_ranuras_coinciden()
    {
        (EditPaletteViewModel form, ColorPalette other) = Editing();

        form.Compared = other;

        Assert.True(form.IsComparing);
        Assert.Equal(ColorPalette.Size, form.ComparedColors.Count);

        // Recien clonada son iguales en todas.
        Assert.All(form.ComparedColors, color => Assert.True(color.Same));

        // Y cambiando un color de la que se edita, esa ranura deja de coincidir.
        form.Palette[3].Red = form.Palette[3].Red == 0 ? 7 : 0;

        Assert.False(form.ComparedColors[3].Same);
        Assert.True(form.ComparedColors[4].Same);
    }

    /// <summary>
    /// Mover un color de sitio cambia las marcas, que es para lo que se compara.
    /// </summary>
    /// <remarks>
    /// Sin esto habría que cerrar y volver a abrir para ver si el arrastre ha acercado las dos
    /// paletas o las ha separado más, que es lo único que se está mirando mientras se arrastra.
    /// </remarks>
    [AvaloniaFact]
    public void Mover_un_color_cambia_las_marcas()
    {
        (EditPaletteViewModel form, ColorPalette other) = Editing();

        form.Compared = other;

        Assert.All(form.ComparedColors, color => Assert.True(color.Same));

        // Y que se avise, no solo que la cuenta salga bien: la vista lee las marcas cuando se
        // le dice que han cambiado, asi que sin el aviso se quedan las de antes en pantalla.
        List<string> changed = [];
        form.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        // Al intercambiar dos ranuras, las dos dejan de coincidir: la 3 pasa a tener el color
        // que estaba en la 9 y al reves, y la otra paleta no se ha movido.
        Assert.True(form.SwapColors(3, 9));

        Assert.Contains(nameof(EditPaletteViewModel.ComparedColors), changed);

        Assert.False(form.ComparedColors[3].Same);
        Assert.False(form.ComparedColors[9].Same);
        Assert.True(form.ComparedColors[4].Same);
    }

    /// <summary>La paleta que se edita no sale entre las que se puede comparar.</summary>
    [AvaloniaFact]
    public void No_se_compara_una_paleta_consigo_misma()
    {
        (EditPaletteViewModel form, _) = Editing();

        Assert.DoesNotContain(form.Comparisons, palette => ReferenceEquals(palette, form.Palette));
        Assert.False(form.IsComparing);
        Assert.Empty(form.ComparedColors);
    }

    /// <summary>Un editor abierto sobre una paleta, con otra igual al lado para comparar.</summary>
    private static (EditPaletteViewModel Form, ColorPalette Other) Editing()
    {
        var main = new MainWindowViewModel();

        EditPaletteViewModel form = TestPalette.Create(main);
        ColorPalette other = main.Palettes.Adopt(form.Palette.Clone("Otra"));

        return (form, other);
    }

    /// <summary>
    /// Las dos columnas van al mismo paso, también en la última fila.
    /// </summary>
    /// <remarks>
    /// Emparejadas por índice, así que basta con que una fila mida un pelo distinto para que
    /// abajo estén a media fila de diferencia y la comparación deje de significar nada. Pasó:
    /// el alto de la derecha se puso a ojo y el de la izquierda lo decidía el tema.
    /// </remarks>
    [AvaloniaFact]
    public void Las_dos_columnas_van_a_la_misma_altura()
    {
        var main = new MainWindowViewModel();

        EditPaletteViewModel form = TestPalette.Create(main);
        form.Compared = main.Palettes.Adopt(form.Palette.Clone("Otra"));

        var view = new EditPaletteView { DataContext = form };
        var window = new Window { Content = view, Width = 420, Height = 900 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        ListBox left = view.GetVisualDescendants().OfType<ListBox>().Single();
        ItemsControl right = view.GetVisualDescendants()
            .OfType<ItemsControl>()
            .First(items => items is not ListBox && items.ItemCount == ColorPalette.Size);

        // La última de cada lado, que es donde se acumula cualquier diferencia por fila.
        Control lastLeft = Row(left, ColorPalette.Size - 1);
        Control lastRight = Row(right, ColorPalette.Size - 1);

        double top = lastLeft.TranslatePoint(new Point(0, 0), view)!.Value.Y;
        double other = lastRight.TranslatePoint(new Point(0, 0), view)!.Value.Y;

        Assert.True(
            Math.Abs(top - other) < 1,
            $"la última fila cae en {top} a la izquierda y en {other} a la derecha.");

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private static Control Row(ItemsControl items, int index) =>
        (Control)items.ContainerFromIndex(index)!;
}
