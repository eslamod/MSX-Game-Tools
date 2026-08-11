using System.Collections.ObjectModel;
using System.Windows.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

public class TreeGeneralViewModel : PanelBaseViewModel
{
    private readonly ItemTree _nodeSpriteBanks;
    private readonly ItemTree _nodeTileSets;

    public TreeGeneralViewModel()
    {
        _nodeSpriteBanks = new ItemTree
        {
            DisplayText = "Sprite Banks",
            Tag = Constants.TAG_ID_NODE_SPRITE_BANKS,
        };

        _nodeTileSets = new ItemTree
        {
            DisplayText = "TileSets",
            Tag = Constants.TAG_ID_NODE_TILESETS,
        };

        PrimaryNodes =
        [
            _nodeSpriteBanks,
            _nodeTileSets,
            new ItemTree { DisplayText = "Maps", Tag = Constants.TAG_ID_NODE_MAPS },
            new ItemTree { DisplayText = "Animations", Tag = Constants.TAG_ID_NODE_ANIMATORS },
            new ItemTree { DisplayText = "Behavours", Tag = Constants.TAG_ID_NODE_BEHAVOURS },
            new ItemTree { DisplayText = "Sounds", Tag = Constants.TAG_ID_NODE_SOUNDS },
            new ItemTree { DisplayText = "Musics", Tag = Constants.TAG_ID_NODE_MUSIC },
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
            DisplayText = "Bloques",
            Tag = blocks.TagId,
            CanDelete = false,
        };

        child.PanelsList.Add(blocks);
        item.Childs.Add(child);
    }

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
