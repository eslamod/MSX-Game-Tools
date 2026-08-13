using System.Collections.ObjectModel;
using System.Windows.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

public class TreeGeneralViewModel : PanelBaseViewModel
{
    private readonly ItemTree _nodeSpriteBanks;
    private readonly ItemTree _nodeTileSets;
    private readonly ItemTree _nodeMaps;

    public TreeGeneralViewModel()
    {
        _nodeSpriteBanks = new ItemTree
        {
            NameKey = "TreeSpriteBanks",
            Tag = Constants.TAG_ID_NODE_SPRITE_BANKS,
        };

        _nodeTileSets = new ItemTree
        {
            NameKey = "TreeTileSets",
            Tag = Constants.TAG_ID_NODE_TILESETS,
        };

        _nodeMaps = new ItemTree
        {
            NameKey = "TreeMaps",
            Tag = Constants.TAG_ID_NODE_MAPS,
        };

        PrimaryNodes =
        [
            _nodeSpriteBanks,
            _nodeTileSets,
            _nodeMaps,
            new ItemTree { NameKey = "TreeAnimations", Tag = Constants.TAG_ID_NODE_ANIMATORS },
            new ItemTree { NameKey = "TreeBehaviours", Tag = Constants.TAG_ID_NODE_BEHAVOURS },
            new ItemTree { NameKey = "TreeSounds", Tag = Constants.TAG_ID_NODE_SOUNDS },
            new ItemTree { NameKey = "TreeMusic", Tag = Constants.TAG_ID_NODE_MUSIC },
        ];

    }

    public ObservableCollection<ItemTree> PrimaryNodes { get; }

    /// <summary>
    /// Abrir y eliminar los pone la ventana principal, que es quien sabe de pestañas.
    /// El árbol sólo dice sobre qué nodo se ha hecho el gesto.
    /// </summary>
    public ICommand? OpenItemCommand { get; set; }

    /// <inheritdoc cref="OpenItemCommand"/>
    public ICommand? DeleteItemCommand { get; set; }

    /// <inheritdoc cref="OpenItemCommand"/>
    public ICommand? ShowPropertiesCommand { get; set; }

    /// <summary>Cambia lo que se lee en el nodo de ese panel.</summary>
    public void Rename(string tagId, string displayText)
    {
        foreach (ItemTree parent in PrimaryNodes)
        {
            foreach (ItemTree child in parent.Childs)
            {
                if (child.Tag == tagId)
                {
                    child.DisplayText = displayText;

                    return;
                }
            }
        }
    }

    /// <summary>Vacía el árbol. Los nodos de primer nivel se quedan, que son fijos.</summary>
    public void Clear()
    {
        foreach (ItemTree parent in PrimaryNodes)
            parent.Childs.Clear();
    }

    /// <summary>Quita un nodo de donde esté colgado. Devuelve si lo encontró.</summary>
    public bool Remove(ItemTree item)
    {
        foreach (ItemTree parent in PrimaryNodes)
        {
            if (parent.Childs.Remove(item))
                return true;
        }

        return false;
    }

    public void AddSpriteBank(string displayName, string tagId, PanelBaseViewModel vm) =>
        AddTo(_nodeSpriteBanks, displayName, tagId, vm);

    /// <param name="blocks">
    /// El panel de bloques del juego, que cuelga de él como un hijo. No se elimina por su
    /// cuenta: no es un elemento aparte, es parte del juego y se va con él.
    /// </param>
    public void AddTileSet(string displayName, string tagId, PanelBaseViewModel vm, PanelBaseViewModel? blocks = null)
    {
        ItemTree item = AddTo(_nodeTileSets, displayName, tagId, vm);

        if (blocks is null)
            return;

        var child = new ItemTree
        {
            NameKey = "TreeBlocks",
            Tag = blocks.TagId,
            CanDelete = false,
        };

        child.PanelsList.Add(blocks);
        item.Childs.Add(child);
    }

    public void AddMap(string displayName, string tagId, PanelBaseViewModel vm) =>
        AddTo(_nodeMaps, displayName, tagId, vm);

    private static ItemTree AddTo(ItemTree parent, string displayName, string tagId, PanelBaseViewModel vm)
    {
        var item = new ItemTree
        {
            DisplayText = displayName,
            Tag = tagId,
        };

        item.PanelsList.Add(vm);
        parent.Childs.Add(item);

        return item;
    }
}
