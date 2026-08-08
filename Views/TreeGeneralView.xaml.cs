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
    /// Interaction logic for TreeGeneralView.xaml
    /// </summary>
    public partial class TreeGeneralView : UserControl
    {
        private PanelBaseVieWModel _vieWModel;
        public TreeGeneralView()
        {
            InitializeComponent();
        }

        public TreeGeneralView(TreeGeneralViewModel vm):this()
        {
            _vieWModel = vm;
            this.DataContext = vm;

        }
    }
}
