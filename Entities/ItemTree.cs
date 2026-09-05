using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MSX_GameTools.Localization;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Entities;

/// <summary>Nodo del árbol de proyecto (bancos de sprites, tilesets, mapas...).</summary>
public partial class ItemTree : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private string _displayText = string.Empty;

    private bool _deletable = true;

    /// <summary>
    /// La clave de su texto, si es un nodo fijo. Lo pone traducido al asignarla.
    /// </summary>
    /// <remarks>
    /// Se resuelve una vez y se queda: el nodo no escucha al idioma. Enlazarlo a algo del
    /// <see cref="Localizer"/>, que es único para todo el proceso, ata cada TextBlock del
    /// árbol a un objeto que no muere nunca, y entonces no se recoge ninguno. Cambiar de
    /// idioma mueve los menús y los paneles al momento; los cajones del árbol esperan al
    /// siguiente arranque.
    /// </remarks>
    public string? NameKey
    {
        init
        {
            if (value is { } key)
                DisplayText = Localizer.Instance[key];
        }
    }

    public string? Type { get; set; }

    public int Id { get; set; }

    public string Tag { get; set; } = string.Empty;

    public ObservableCollection<ItemTree> Childs { get; } = [];

    public ObservableCollection<PanelBaseViewModel> PanelsList { get; } = [];

    /// <summary>
    /// Lo que se lee en el árbol. El asterisco marca lo que está sin guardar.
    /// </summary>
    /// <remarks>
    /// El mismo que sale en la pestaña, y por el mismo sitio. Cerrar una pestaña no toca el
    /// proyecto —el documento se queda con sus cambios y el nodo lo vuelve a traer con un
    /// doble clic—, pero al irse la pestaña se iba con ella la única señal de que había algo
    /// sin guardar, y eso parecía que se hubiera perdido.
    /// </remarks>
    public string Label => PanelsList.Any(panel => panel.IsModified)
        ? $"{DisplayText} *"
        : DisplayText;

    /// <summary>
    /// Si el nodo representa algo que se puede abrir y eliminar. Los de categoría
    /// («Sprite Banks», «TileSets»...) no lo son: son cajones, no elementos.
    /// </summary>
    public bool IsPanelNode => PanelsList.Count > 0;

    /// <summary>
    /// Si el nodo se puede eliminar del proyecto.
    /// </summary>
    /// <remarks>
    /// Los bloques de un juego de tiles se abren como cualquier otro nodo pero no se
    /// eliminan: no son un elemento aparte, son parte del juego, y se van con él.
    /// </remarks>
    public bool CanDelete
    {
        // Los de categoría siguen sin poder eliminarse, como antes: son cajones.
        get => IsPanelNode && _deletable;
        set => _deletable = value;
    }

    public ItemTree() => PanelsList.CollectionChanged += OnPanelsChanged;

    /// <summary>Se escucha a los paneles que le cuelgan, para saber si tienen cambios.</summary>
    private void OnPanelsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (PanelBaseViewModel panel in args.OldItems?.OfType<PanelBaseViewModel>() ?? [])
            panel.PropertyChanged -= OnPanelChanged;

        foreach (PanelBaseViewModel panel in args.NewItems?.OfType<PanelBaseViewModel>() ?? [])
            panel.PropertyChanged += OnPanelChanged;

        OnPropertyChanged(nameof(Label));
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(PanelBaseViewModel.IsModified))
            OnPropertyChanged(nameof(Label));
    }
}
