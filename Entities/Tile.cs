using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.Entities
{
    public class Tile
    {
        private TileRow[] arrayTileRows;

        public TileRow[] ArrayTileRows
        {
            get
            {
                return arrayTileRows;
            }
        }

        public Tile()
        {
            arrayTileRows = new TileRow[8];
        }
    }
}
