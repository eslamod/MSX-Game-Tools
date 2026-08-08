using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MSX_SpritesEditor.Entities
{
    public class ImageMini
    {
        public enum ImagePreviewType
        {
            ImagePreview8x8=0,
            ImagePreview16x16
        }

        private BitmapImage _bitmapImage;
        private Bitmap _spritePreview;
        MemoryStream _memory;

        public ImageMini(ImagePreviewType type)
        {
            _memory = new MemoryStream();            
            _bitmapImage = new BitmapImage();

            
            
            switch (type)
            {
                case ImagePreviewType.ImagePreview8x8:
                    _spritePreview = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
                    break;
                case ImagePreviewType.ImagePreview16x16:
                    _spritePreview = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
                    break;
            }

            for (int i = 0; i < 15; i += 2)
            {
                for ( int j=0; j<16;j+=2)
                {
                    _spritePreview.SetPixel(i,j,Color.White);
                }
            }
        }

        public void SetPixel( int x, int y,Color col)
        {
            _spritePreview.SetPixel(x, y, col);
        }


        public BitmapImage SpritePreview
        {
            get
            {
                _memory.Position = 0;

                _spritePreview.Save(_memory, System.Drawing.Imaging.ImageFormat.Bmp);
                _memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();                
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = _memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                return bitmapimage;                
            }
        }
    }
}
