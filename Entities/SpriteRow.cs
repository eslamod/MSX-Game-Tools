using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor.Entities
{
    public class SpriteRow
    {
        private bool[] _arrayColumns;
        private int _color;

        public bool[] ArrayColumns
        {
            get
            {
                return _arrayColumns;
            }            
        }
        public int Color
        {
            get
            {
                return _color;
            }           
            set
            {
                _color = value;
            } 
        }

        public SpriteRow()
        {
            _arrayColumns = new bool[16];
           
        } 
    }
}
