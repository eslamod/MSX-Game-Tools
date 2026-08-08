using MSX_SpritesEditor.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.ViewModels
{
    public class TreeGeneralViewModel:PanelBaseVieWModel
    {

        private ObservableCollection<ItemTree> _primaryNodes;
        private ItemTree nodeSpriteBanks;


        public TreeGeneralViewModel()
        {
            _primaryNodes = new ObservableCollection<ItemTree>();
            ItemTree it1 = new ItemTree();
            it1.DisplayText = "Sprite Banks";
            it1.Tag = Constants.TAG_ID_NODE_SPRITE_BANKS;
            
            _primaryNodes.Add(it1);
            nodeSpriteBanks = it1;

            /*
            ItemTree itSpBank1 = new ItemTree();
            itSpBank1.DisplayText = "Sprite Bank 1";
            SpriteBank spBank = new SpriteBank();
            SpritesEditorViewModel spvm = new SpritesEditorViewModel(spBank);
            spvm.Header = itSpBank1.DisplayText;
            itSpBank1.PanelsList.Add(spvm);
            it1.Childs.Add(itSpBank1);

            ItemTree itSpBank2 = new ItemTree();
            itSpBank2.DisplayText = "sprite bank 2";
            it1.Childs.Add(itSpBank2);

    */



            ItemTree it2 = new ItemTree();
            it2.DisplayText = "TileSets";
            it2.Tag = Constants.TAG_ID_NODE_TILESETS;
            _primaryNodes.Add(it2);


            ItemTree it3 = new ItemTree();
            it3.DisplayText = "Maps";
            it3.Tag = Constants.TAG_ID_NODE_MAPS;
            _primaryNodes.Add(it3);


            ItemTree it4 = new ItemTree();
            it4.DisplayText = "Animations";
            it4.Tag = Constants.TAG_ID_NODE_ANIMATORS;
            _primaryNodes.Add(it4);

            ItemTree it5 = new ItemTree();
            it5.DisplayText = "Behavours";
            it5.Tag = Constants.TAG_ID_NODE_BEHAVOURS;
            _primaryNodes.Add(it5);

            ItemTree it6 = new ItemTree();
            it6.DisplayText = "Sounds";
            it6.Tag = Constants.TAG_ID_NODE_SOUNDS;
            _primaryNodes.Add(it6);

            ItemTree it7 = new ItemTree();
            it7.DisplayText = "Musics";
            it7.Tag = Constants.TAG_ID_NODE_MUSIC;
            _primaryNodes.Add(it7);

        }
        public ObservableCollection<ItemTree> PrimaryNodes
        {
            get
            {
                return _primaryNodes;
            }
        }

        public void AddSpriteBank( string displayName, string tagId, PanelBaseVieWModel vm)
        {
            ItemTree item = new ItemTree();
            item.DisplayText = displayName;
            item.Tag = tagId;
            item.PanelsList.Add(vm);
            nodeSpriteBanks.Childs.Add(item);
        }
    }
}
;