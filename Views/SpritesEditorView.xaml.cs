using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MSX_SpritesEditor.Views
{


    /// <summary>
    /// Interaction logic for SpritesEditorView.xaml
    /// </summary>
    public partial class SpritesEditorView : UserControl
    {
        //SpritesEditorViewModel _vm;
        private int[] _arrayWidth;
        private int[] _arrayHeight;
        public enum DrawState
        {
            Drawing,
            NotDrawing
        }

        private Point currentPoint;
        private DrawState eDrawingState;
        
        public SpritesEditorView()
        {
            InitializeComponent();
            eDrawingState = DrawState.NotDrawing;
            _arrayWidth = new int[3];
            _arrayHeight = new int[3];

            _arrayWidth[0] = 256;
            _arrayWidth[1] = 512;
            _arrayWidth[2] = 600;

            _arrayHeight[0] = 256;
            _arrayHeight[1] = 512;
            _arrayHeight[2] = 600;


        }
        /*
        public SpritesEditorView(SpritesEditorViewModel vm, ColorPalette cp):this()
        {
            DataContext = vm;
            //_vm = vm;
            _colorPalette = cp;
        }
        */

        public void Draw()
        {
            SpritesEditorViewModel vm = (SpritesEditorViewModel)this.DataContext;
             
            double x = 0;
            double y = 0;
            double  width = CanvSprite.Width / 16.0;
            double height = CanvSprite.Height / 16.0;
                       
            SolidColorBrush background = new SolidColorBrush(Colors.Black);
            bool firtTime = false;
            if (CanvSprite.Children.Count == 0)
                firtTime = true;
            int index = 0;
            foreach ( SpriteRow sr   in vm.CurrentSprite.ArraySpriteRows)
            {
                x = 0;
                for (int i = 0;i< sr.ArrayColumns.Length; i++)
                {
                    Rectangle rec;
                    SolidColorBrush sc = background;
                    if (firtTime)
                    {                                               
                        rec = new Rectangle();
                        CanvSprite.Children.Add(rec);
                    }
                    else
                    {                       
                        rec = CanvSprite.Children[index++] as Rectangle;
                    }


                    if (sr.ArrayColumns[i])
                    {
                        sc = new SolidColorBrush(vm.ColorPalette.GetColor(sr.Color));
                    }
                    else
                        sc = background;

                    rec.Fill = sc;
                    rec.StrokeThickness = 1;
                    rec.Stroke = Brushes.Black;

                    rec.Width = width;
                    rec.Height = height;                                            
                    

                    
                    Canvas.SetLeft(rec, x);
                    Canvas.SetTop(rec, y);
                    x += width;
                }
                y += height;
            }
        }

        /// <summary>
        /// Pone un pixel del sprite a 1 con el color foreground que tenga activo esa fila
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private void SetPixel(double x, double y)
        {
            DrawPixel(x,y,true);            
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private void ErasePixel(double x, double y)
        {
            DrawPixel(x, y, false);
        }

        private void DrawPixel(double x, double y,bool drawErase)
        {
            SpritesEditorViewModel vm = (SpritesEditorViewModel)this.DataContext;

            double tamPixelX = CanvSprite.Width / 16.0;
            double tamPixelY = CanvSprite.Height/ 16.0;
            int posX = (int)(x / tamPixelX);
            int posY = (int)(y / tamPixelY);

            int indexRectangle = posY * 16 + posX;
            Rectangle rec = (Rectangle)CanvSprite.Children[indexRectangle];

            //Get sprite Row
            SpriteRow sr = vm.CurrentSprite.ArraySpriteRows[posY];

            Color co;
            if (drawErase)
                co = vm.ColorPalette.GetColor(sr.Color);
            else
                co = Colors.Black;

            SolidColorBrush sc = new SolidColorBrush(co);
            rec.Fill = sc;

            sr.ArrayColumns[posX] = drawErase;

            vm.CurrentSprite.ImageMini.SetPixel(posX, posY, System.Drawing.Color.FromArgb(co.A,co.R,co.G,co.B));
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SpritesEditorViewModel vm = (SpritesEditorViewModel)this.DataContext;
            vm.EvRefresh += Vm_EvRefresh;
            Draw();
        }

        private void Vm_EvRefresh(Sprite currentSpr)
        {
            Draw();
        }

        private void CanvSprite_MouseMove(object sender, MouseEventArgs e)
        {

            if ( currentPoint != null && eDrawingState== DrawState.Drawing)
            {
                
                bool drawErase = false;
                if (e.LeftButton == MouseButtonState.Pressed)
                    drawErase = true;
                else if (e.RightButton == MouseButtonState.Pressed)
                    drawErase = false;

                if ( drawErase)
                    SetPixel(currentPoint.X, currentPoint.Y);
                else 
                    ErasePixel(currentPoint.X, currentPoint.Y);

                currentPoint = e.GetPosition(CanvSprite);
                //Console.WriteLine(string.Format("{0} : {1}", currentPoint.X.ToString("000.000"), currentPoint.Y.ToString("000.000")));

           
            }
        }

        private void CanvSprite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ( e.LeftButton == MouseButtonState.Pressed)
            {
                currentPoint = e.GetPosition(CanvSprite);
                eDrawingState = DrawState.Drawing;
                SetPixel(currentPoint.X, currentPoint.Y);
            }        
            if ( e.RightButton== MouseButtonState.Pressed)
            {
                currentPoint = e.GetPosition(CanvSprite);
                eDrawingState = DrawState.Drawing;
                ErasePixel(currentPoint.X, currentPoint.Y);
            }
        }

        private void CanvSprite_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Released)
            {
                eDrawingState = DrawState.NotDrawing;                
            }
        }

        private void CanvSprite_MouseLeave(object sender, MouseEventArgs e)
        {
            eDrawingState = DrawState.NotDrawing;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_arrayWidth != null)
            {
                colSpriteMagnif.Width = new GridLength((double)_arrayWidth[0]);
                rowSpriteMagnif.Height = new GridLength((double)_arrayHeight[0]);
                CanvSprite.Width = (double)_arrayWidth[0];
                CanvSprite.Height= (double)_arrayHeight[0];
                Draw();
            }
        }

        private void RadioButton_Checked_1(object sender, RoutedEventArgs e)
        {
            if(_arrayWidth != null)
            {
                colSpriteMagnif.Width = new GridLength((double)_arrayWidth[1]);
                rowSpriteMagnif.Height = new GridLength((double)_arrayHeight[1]);
                CanvSprite.Width = (double)_arrayWidth[1];
                CanvSprite.Height = (double)_arrayHeight[1];                
                Draw();
            }
        }

        private void RadioButton_Checked_2(object sender, RoutedEventArgs e)
        {
            if(_arrayWidth != null)
            {
                colSpriteMagnif.Width = new GridLength((double)_arrayWidth[2]);
                rowSpriteMagnif.Height = new GridLength((double)_arrayHeight[2]);
                CanvSprite.Width = (double)_arrayWidth[2];
                CanvSprite.Height = (double)_arrayHeight[2];
                Draw();
            }
        }
    }


    
}
