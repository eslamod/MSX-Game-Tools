using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.Entities
{
    public class Sprite
    {
        private SpriteRow[] _arraySpriteRows;
        public SpriteRow[] ArraySpriteRows
        {
            get
            {
                return _arraySpriteRows;
            }
        }

        public Sprite()
        {
            _arraySpriteRows = new SpriteRow[16];
            for (int i = 0; i < 16; i++)
            {
                _arraySpriteRows[i] = new SpriteRow();
                _arraySpriteRows[i].Color = 15;
            }            
        }

        private ImageMini _imageMini;

        public ImageMini ImageMini
        {
            get
            {
                return _imageMini;
            }
            set
            {
                _imageMini = value;
            }
        }
    }
    }
