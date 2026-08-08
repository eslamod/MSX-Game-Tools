using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.Entities
{
    public class SpriteBank
    {
        public enum SpriteType
        {
            MSX,
            MSX2
        }

        private IList<Sprite> _spritesList;
        private SpriteType _spriteType;

        public IList<Sprite> SpritesList
        {
            get
            {
                return _spritesList;
            }            
        }

        public SpriteBank()
        {
            _spriteType = SpriteType.MSX;
            _spritesList = new List<Sprite>();
            Sprite sp = new SpriteMSX();
            ImageMini im = new ImageMini(ImageMini.ImagePreviewType.ImagePreview16x16);
            sp.ImageMini = im;
            _spritesList.Add(sp);
        }

        public SpriteBank(SpriteType spType)
        {
            _spriteType = spType;

        }

        public Sprite NewSprite()
        {
            if ( _spritesList.Count==64)
            {
                return null;
            }
            Sprite sp;
            if (_spriteType == SpriteType.MSX)
                sp = new SpriteMSX();
            else 
                sp = new SpriteMSX2();

            _spritesList.Add(sp);
            return sp;
        }

        public void DeleteSprite(int pos)
        {
            _spritesList.RemoveAt(pos);
        }
    }
}
