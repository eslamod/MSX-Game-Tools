using System.Collections.ObjectModel;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

public class TreeGeneralViewModel : PanelBaseViewModel
{
    private readonly ItemTree _nodeSpriteBanks;

    public TreeGeneralViewModel()
    {
        _nodeSpriteBanks = new ItemTree
        {
            DisplayText = "Sprite Banks",
            Tag = Constants.TAG_ID_NODE_SPRITE_BANKS,
        };

        PrimaryNodes =
        [
            _nodeSpriteBanks,
            new ItemTree { DisplayText = "TileSets", Tag = Constants.TAG_ID_NODE_TILESETS },
            new ItemTree { DisplayText = "Maps", Tag = Constants.TAG_ID_NODE_MAPS },
            new ItemTree { DisplayText = "Animations", Tag = Constants.TAG_ID_NODE_ANIMATORS },
            new ItemTree { DisplayText = "Behavours", Tag = Constants.TAG_ID_NODE_BEHAVOURS },
            new ItemTree { DisplayText = "Sounds", Tag = Constants.TAG_ID_NODE_SOUNDS },
            new ItemTree { DisplayText = "Musics", Tag = Constants.TAG_ID_NODE_MUSIC },
        ];
    }

    public ObservableCollection<ItemTree> PrimaryNodes { get; }

    public void AddSpriteBank(string displayName, string tagId, PanelBaseViewModel vm)
    {
        var item = new ItemTree
        {
            DisplayText = displayName,
            Tag = tagId,
        };
        item.PanelsList.Add(vm);
        _nodeSpriteBanks.Childs.Add(item);
    }
}
