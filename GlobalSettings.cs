using MSX_SpritesEditor.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor
{
    public class GlobalSettings
    {
        public static ColorPalette CurrentColorPalette;
        static GlobalSettings()
        {
            CurrentColorPalette = new ColorPalette();
        }
        
    }
}
