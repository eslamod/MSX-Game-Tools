using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MSX_SpritesEditor.Entities
{
    public class ColorPalette
    {
        private Color[] arrayColors;

        public ColorPalette()
        {
            arrayColors = new Color[16];

            // Default values, MSX1 palette

            /*  0 0,0,0->0,0,0 = Transparent
                1 0,0,0->0,0,0
                2 1,6,1->36,219,36
                3 3,7,3->109,255,109
                4 1,1,7->36,36,255
                5 2,3,7->72,109,255
                6 5,1,1->182,36,36
                7 2,6,7->72,219,255
                8 7,1,1->255,36,36
                9 7,3,3->255,109,109
                10 6,6,1->219,219,36
                11 6,6,4->219,219,146
                12 1,4,1->36,146,36
                13 6,2,5->219,72,182
                14 5,5,5->182,182,182
                15 7,7,7->255,255,255
                */

            arrayColors[0] = Colors.Transparent;
            arrayColors[1] = Color.FromRgb(0, 0, 0);
            arrayColors[2] = Color.FromRgb(36, 219, 36);
            arrayColors[3] = Color.FromRgb(109, 255, 109);
            arrayColors[4] = Color.FromRgb(36, 36, 255);
            arrayColors[5] = Color.FromRgb(72, 109, 255);
            arrayColors[6] = Color.FromRgb(182, 36, 36);
            arrayColors[7] = Color.FromRgb(72, 219, 255);
            arrayColors[8] = Color.FromRgb(255, 36, 36);
            arrayColors[9] = Color.FromRgb(255, 109, 109);
            arrayColors[10] = Color.FromRgb(219, 219, 36);
            arrayColors[11] = Color.FromRgb(219, 219, 146);
            arrayColors[12] = Color.FromRgb(36, 146, 36);

            arrayColors[13] = Color.FromRgb(219, 72, 182);
            arrayColors[14] = Color.FromRgb(182, 182, 182);
            arrayColors[15] = Color.FromRgb(255, 255, 255);

        }

        public Color GetColor(int index)
        {
            return arrayColors[index];
        }
    }
}
