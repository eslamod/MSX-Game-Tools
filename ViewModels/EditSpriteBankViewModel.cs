using MSX_SpritesEditor.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static MSX_SpritesEditor.Entities.SpriteBank;

namespace MSX_SpritesEditor.ViewModels
{
    

    public class EditSpriteBankViewModel:PanelBaseVieWModel
    {
        private ICommand _AcceptSpriteBankCommand;
        private bool _canExecuteAccept;
        private ICommand _CancelSpriteBankCommand;      
        private bool _canExecuteCancel;

        


        private MainWindowViewModel _mainWindowVM;

        private int _numberSprties;
        private int _currentSprite;


        public int NumberSprties
        {
            get
            {
                return _numberSprties;
            }

            set
            {
                _numberSprties = value;
                OnPropertyChanged("NumberSprties");
            }
        }

        public int CurrentSprite
        {
            get
            {
                return _currentSprite;
            }

            set
            {
                _currentSprite = value;
                OnPropertyChanged("CurrentSprite");
            }
        }

        public EditSpriteBankViewModel(MainWindowViewModel mvm)
        {
            _mainWindowVM = mvm;
            _canExecuteCancel = true;
            _canExecuteAccept = true;
            
            _AcceptSpriteBankCommand = new CommandHandler(() => AcceptAddSpriteBank(), _canExecuteAccept);
            _CancelSpriteBankCommand = new CommandHandler(() => CancelAddSpriteBank(), _canExecuteCancel);
            


        }

        private void AcceptAddSpriteBank()
        {
            if (Validate())
            {

                SpriteBank spb = new SpriteBank();

                PanelBaseVieWModel vm = new SpritesEditorViewModel(spb, GlobalSettings.CurrentColorPalette);
                vm.TagId = "spb" + _mainWindowVM.CurrentSpriteBankCounter.ToString();

                vm.Header = Name + " (SP)";
                _mainWindowVM.CurrentSpriteBankCounter++;
                _mainWindowVM.Tabs.Add(vm);

                ItemTree it = new ItemTree();

                _mainWindowVM.TreeGeneralVm.AddSpriteBank(vm.Header , vm.TagId, vm);


                _mainWindowVM.RightPanViewModel = null;

                
                
            }
        }

        private bool Validate()
        {          
            if ( string.IsNullOrEmpty(Name))
            {
                MessageBox.Show("Can't leave name of bank sprite in blank");
                return false;
            }
            return true;
        }

        private void CancelAddSpriteBank()
        {
            _mainWindowVM.RightPanViewModel = null;
        }
        

        public string Name {get; set; }
        public SpriteType Type { get; set;}

        public ICommand AcceptSpriteBankCommand
        {
            get
            {
                return _AcceptSpriteBankCommand;
            }            
        }

        public ICommand CancelSpriteBankCommand
        {
            get
            {
                return _CancelSpriteBankCommand;
            }
         }


    }
}
