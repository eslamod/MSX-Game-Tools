using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Dónde se ve que un documento tiene cambios sin guardar.
/// </summary>
/// <remarks>
/// Sólo se veía en la pestaña, y cerrar una pestaña no toca el proyecto: el documento se
/// queda con sus cambios y el nodo del árbol lo vuelve a traer con un doble clic. Al irse la
/// pestaña se iba con ella la única señal, y eso parecía que se hubiera perdido lo hecho.
/// </remarks>
public class UnsavedMarksTests
{
    /// <summary>
    /// El nodo del árbol lleva el mismo asterisco que la pestaña.
    /// </summary>
    /// <remarks>
    /// En el árbol montado: que el nodo se entere de que su documento ha cambiado depende de
    /// que escuche al panel, y leyendo la propiedad a pelo eso no se ve —se calcula al
    /// preguntar y saldría bien igual, con el árbol de la pantalla sin enterarse—.
    /// </remarks>
    [AvaloniaFact]
    public void El_arbol_marca_el_documento_con_cambios()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        ItemTree node = NodeOf(main, tiles);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        TreeView tree = window.GetVisualDescendants().OfType<TreeView>().First();

        // Desplegando el cajón, como se despliega con el ratón: hasta entonces la fila del
        // documento no existe y no habría nada que mirar.
        TreeViewItem drawer = tree.GetRealizedContainers()
            .OfType<TreeViewItem>()
            .Single(item => item.DataContext is ItemTree parent && parent.Childs.Contains(node));

        drawer.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        string[] Labels() =>
            [.. tree.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty)];

        // Por el texto del nodo y no por el nombre a pelo: ahí pone «Bosque (TS)».
        string[] before = Labels();

        tiles.Touch();
        Dispatcher.UIThread.RunJobs();

        string[] after = Labels();

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(node.DisplayText, before);
        Assert.DoesNotContain($"{node.DisplayText} *", before);
        Assert.Contains($"{node.DisplayText} *", after);
    }

    /// <summary>
    /// Y se queda puesto al cerrar la pestaña, que es cuando hacía falta.
    /// </summary>
    [AvaloniaFact]
    public void El_asterisco_del_arbol_no_se_va_con_la_pestaña()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        tiles.Touch();

        main.CloseTabCommand.Execute(tiles);

        ItemTree node = NodeOf(main, tiles);

        Assert.DoesNotContain(tiles, main.Tabs);
        Assert.Equal($"{node.DisplayText} *", node.Label);
    }

    /// <summary>La barra de estado cuenta los documentos sin guardar, con pestaña o sin ella.</summary>
    [AvaloniaFact]
    public void La_barra_cuenta_lo_que_queda_sin_guardar()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel map = main.OpenMap(new TileMap("Mapa", 8, 8), tiles);

        Assert.Equal(0, main.UnsavedCount);
        Assert.False(main.HasUnsaved);

        tiles.Touch();
        map.Touch();

        Assert.Equal(2, main.UnsavedCount);

        main.CloseTabCommand.Execute(map);

        // Cerrar la pestaña no guarda nada: el documento sigue ahí y sigue contando.
        Assert.Equal(2, main.UnsavedCount);
        Assert.Equal(Localizer.Instance.Format("StatusUnsaved", 2), main.UnsavedLabel);
    }

    /// <summary>Y en la ventana montada se lee en la barra, que es donde se mira.</summary>
    /// <remarks>
    /// Montada porque tanto el texto como que salga o no van por enlace, y un nombre mal
    /// escrito en el XAML no lo ve ninguna prueba del modelo de vista.
    /// </remarks>
    [AvaloniaFact]
    public void La_cuenta_se_lee_en_la_ventana()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        TextBlock counter = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.GetValue(TextBlock.TextProperty) == main.UnsavedLabel
                            || text.Text == Localizer.Instance.Format("StatusUnsaved", 0));

        bool hiddenAtFirst = !counter.IsVisible;

        tiles.Touch();
        Dispatcher.UIThread.RunJobs();

        bool showed = counter.IsVisible;
        string said = counter.Text ?? string.Empty;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(hiddenAtFirst, "Sin nada que guardar, la barra no tendria que decir nada.");
        Assert.True(showed, "Con un documento tocado, la cuenta tendria que verse.");
        Assert.Equal(Localizer.Instance.Format("StatusUnsaved", 1), said);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>El nodo del árbol de ese panel.</summary>
    private static ItemTree NodeOf(MainWindowViewModel main, PanelBaseViewModel panel) =>
        main.TreeGeneralVm.PrimaryNodes
            .SelectMany(node => node.Childs)
            .SelectMany(node => node.Childs.Append(node))
            .Single(node => node.PanelsList.Contains(panel));
}
