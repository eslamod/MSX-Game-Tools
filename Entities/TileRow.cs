using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.Entities
{
    public class TileRow
    {
        private bool[] arrayPattern;
        
        public bool[] ArrayPattern
        {
            get
            {
                return arrayPattern;
            }            
        }

        public TileRow()
        {
            arrayPattern = new bool[8];
        }

        public int BackColor { get; set; }
        public int ForeColor { get; set; }
    }
}
