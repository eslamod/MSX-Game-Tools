using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El árbol del proyecto y las pestañas.
/// </summary>
/// <remarks>
/// La distinción que sostiene todo esto: cerrar una pestaña la quita de la vista y el
/// elemento sigue en el proyecto; eliminar desde el árbol se lo lleva. Lo primero es
/// reversible con un doble clic, lo segundo no y por eso pregunta.
/// </remarks>
public class ProjectTreeTests
{
    [AvaloniaFact]
    public void Cerrar_una_pestana_no_saca_el_elemento_del_arbol()
    {
        var main = new MainWindowViewModel();
        SpritesEditorViewModel editor = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        main.CloseTabCommand.Execute(editor);

        Assert.Empty(main.Tabs);
        Assert.Null(main.SelectedTab);
        Assert.Single(main.TreeGeneralVm.PrimaryNodes[0].Childs);
    }

    [AvaloniaFact]
    public void Volver_a_abrir_recupera_el_mismo_panel_con_su_estado()
    {
        var main = new MainWindowViewModel();
        SpritesEditorViewModel editor = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        editor.NextSpriteCommand.Execute(null);
        editor.AddGroupCommand.Execute(null);

        main.CloseTabCommand.Execute(editor);

        ItemTree node = main.TreeGeneralVm.PrimaryNodes[0].Childs[0];
        main.OpenTreeItemCommand.Execute(node);

        // El mismo objeto, no uno nuevo: cerrar no reconstruye nada.
        Assert.Same(editor, main.Tabs[0]);
        Assert.Same(editor, main.SelectedTab);
        // El banco tiene siempre 64 huecos, asi que lo que dice que el estado sigue vivo es
        // por cual se habia dejado y el grupo que se creo, no cuantos patrones hay.
        Assert.Equal(1, editor.CurrentSpriteIndex);
        Assert.Single(editor.SpritesBank.Groups);
    }

    [AvaloniaFact]
    public void Abrir_algo_que_ya_esta_abierto_solo_lo_selecciona()
    {
        var main = new MainWindowViewModel();
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Uno"));
        SpritesEditorViewModel second = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Dos"));

        main.OpenTreeItemCommand.Execute(main.TreeGeneralVm.PrimaryNodes[0].Childs[0]);

        Assert.Equal(2, main.Tabs.Count);
        Assert.NotSame(second, main.SelectedTab);
    }

    [AvaloniaFact]
    public void Al_cerrar_la_seleccionada_queda_seleccionada_otra()
    {
        var main = new MainWindowViewModel();
        SpritesEditorViewModel first = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Uno"));
        SpritesEditorViewModel second = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Dos"));

        main.CloseTabCommand.Execute(second);

        Assert.Same(first, main.SelectedTab);
    }

    [AvaloniaFact]
    public async Task Eliminar_pregunta_y_se_lleva_arbol_y_pestana()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        ItemTree node = main.TreeGeneralVm.PrimaryNodes[0].Childs[0];
        await main.DeleteTreeItemCommand.ExecuteAsync(node);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Contains("Bicho", dialogs.LastConfirmMessage);

        Assert.Empty(main.Tabs);
        Assert.Empty(main.TreeGeneralVm.PrimaryNodes[0].Childs);

        // Y el panel se olvida: el nodo ya no está, pero por si acaso.
        Assert.Null(main.GetPanelFromDic(node.Tag));
    }

    [AvaloniaFact]
    public async Task Decir_que_no_deja_el_elemento_donde_estaba()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = false });
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await main.DeleteTreeItemCommand.ExecuteAsync(main.TreeGeneralVm.PrimaryNodes[0].Childs[0]);

        Assert.Single(main.Tabs);
        Assert.Single(main.TreeGeneralVm.PrimaryNodes[0].Childs);
    }

    /// <summary>Los nodos de categoría son cajones, no elementos: no se abren ni se borran.</summary>
    [AvaloniaFact]
    public async Task Un_nodo_de_categoria_no_se_puede_abrir_ni_eliminar()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        ItemTree spriteBanks = main.TreeGeneralVm.PrimaryNodes[0];

        Assert.False(spriteBanks.IsPanelNode);

        // Cuántas hay, no cuántas debería haber: lo que se comprueba es que no se vaya
        // ninguna, y cuáles son las categorías es una decisión de alcance que cambia.
        int categories = main.TreeGeneralVm.PrimaryNodes.Count;

        main.OpenTreeItemCommand.Execute(spriteBanks);
        await main.DeleteTreeItemCommand.ExecuteAsync(spriteBanks);

        Assert.Empty(main.Tabs);
        Assert.Equal(0, dialogs.ConfirmCalls);
        Assert.Equal(categories, main.TreeGeneralVm.PrimaryNodes.Count);
    }

    /// <summary>
    /// El gesto de verdad sobre el árbol montado. Enlazar la propiedad no prueba que el
    /// doble clic llegue: el ContextMenu y el DoubleTapped se resuelven en el code-behind.
    /// </summary>
    [AvaloniaFact]
    public void El_doble_clic_en_el_arbol_reabre_la_pestana()
    {
        var main = new MainWindowViewModel();
        SpritesEditorViewModel editor = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));
        main.CloseTabCommand.Execute(editor);

        var tree = new TreeGeneralView { DataContext = main.TreeGeneralVm };
        var window = new Window { Content = tree, Width = 300, Height = 400 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // El nodo del banco cuelga de «Sprite Banks», así que hay que desplegarlo para
        // que su fila llegue a existir en el árbol visual.
        TreeView view = tree.GetVisualDescendants().OfType<TreeView>().Single();
        TreeViewItem category = view.GetVisualDescendants().OfType<TreeViewItem>().First();
        category.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        Control row = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(text => text.DataContext is ItemTree { IsPanelNode: true });

        row.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(main.Tabs);
        Assert.Same(editor, main.Tabs[0]);

        // Cerrarla no es cortesía: una ventana abierta se queda en la aplicación con su
        // compositor vivo el resto de la serie. Ver «suite-headless-se-cuelga».
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
