using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.Entities
{
    public class TileSet
    {
        private List<Tile> listaTiles;

        public List<Tile> ListOfTiles
        {
            get
            {
                return listaTiles;
            }
        }

        public TileSet()
        {
            listaTiles = new List<Tile>();
        }

    }
}
