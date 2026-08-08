using MSX_SpritesEditor.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MSX_SpritesEditor.ViewModels
{
    public class ViewModelFacctory
    {
        public UserControl GetViewFromViewModel(PanelBaseVieWModel panelViewModel)
        {
            UserControl uc= null;
            //if (panelViewModel is SpritesEditorViewModel)
             //   uc = new SpritesEditorView((SpritesEditorViewModel)panelViewModel,GlobalSettings.CurrentColorPalette);
            if (panelViewModel is TreeGeneralViewModel)
                uc = new TreeGeneralView((TreeGeneralViewModel)panelViewModel);
            return uc;
        }
    }
}
