using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MSX_SpritesEditor.ViewModels;

namespace MSX_SpritesEditor.Entities;

/// <summary>Nodo del árbol de proyecto (bancos de sprites, tilesets, mapas...).</summary>
public partial class ItemTree : ObservableObject
{
    [ObservableProperty]
    private string _displayText = string.Empty;

    private bool _deletable = true;

    public string? Type { get; set; }

    public int Id { get; set; }

    public string Tag { get; set; } = string.Empty;

    public ObservableCollection<ItemTree> Childs { get; } = [];

    public IList<PanelBaseViewModel> PanelsList { get; } = [];

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
}
