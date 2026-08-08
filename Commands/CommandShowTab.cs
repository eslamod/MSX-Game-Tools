using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MSX_SpritesEditor.Commands
{
    public class CommandShowTab : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private MainWindowViewModel _mainWindowVm;

        public CommandShowTab(MainWindowViewModel vm)
        {
            _mainWindowVm = vm;
        }
        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if ( parameter is ItemTree)
            {
                ItemTree it = parameter as ItemTree;
                _mainWindowVm.AddVisiblePanel(it.Tag);
            }
        }
    }
}
