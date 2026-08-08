using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MSX_SpritesEditor
{
    public class MainWindowViewModel:INotifyPropertyChanged
    {        
        private Dictionary<string, PanelBaseVieWModel> _dicPanels;        
        private TreeGeneralViewModel _TreeGeneralVm;
        private ObservableCollection<PanelBaseVieWModel> _tabs;
        private ICommand _addSpriteBankCommand;
        private bool _canExecute;

        private int currentSpriteBankCounter;
        private PanelBaseVieWModel _rightPanViewModel;

        public ICommand AddSpriteBankCommand
        {
            get
            {
                return _addSpriteBankCommand;
            }
        }
        
        public MainWindowViewModel()
        {
            _TreeGeneralVm = new TreeGeneralViewModel();
            _dicPanels = new Dictionary<string, PanelBaseVieWModel>();
            _tabs = new ObservableCollection<PanelBaseVieWModel>();
            _canExecute = true;
            _addSpriteBankCommand = new CommandHandler(() => AddSpriteBank(), _canExecute);

        }
        
        public TreeGeneralViewModel TreeGeneralVm
        {
            get
            {
                return _TreeGeneralVm;
            }            
        }

        public ObservableCollection<PanelBaseVieWModel> Tabs
        {
            get
            {
                return _tabs;
            }            
        }

        public PanelBaseVieWModel RightPanViewModel
        {
            get
            {
                return _rightPanViewModel;
            }
            set
            {
                _rightPanViewModel = value;
                OnPropertyChanged("RightPanViewModel");
            }
        }

        public int CurrentSpriteBankCounter
        {
            get
            {
                return currentSpriteBankCounter;
            }

            set
            {
                currentSpriteBankCounter = value;
            }
        }

        public PanelBaseVieWModel SelectedTab
        {
            get
            {
                return _selectedTab;
            }

            set
            {
                _selectedTab = value;
                OnPropertyChanged("SelectedTab");
            }
        }

        private PanelBaseVieWModel _selectedTab;
        

        public void AddPanelToDic(PanelBaseVieWModel panelvm)
        {
            if ( !_dicPanels.ContainsKey(panelvm.TagId))
            _dicPanels.Add(panelvm.TagId, panelvm);            
        }

        public PanelBaseVieWModel GetPanelFromDic(string panelId)
        {
            if (_dicPanels.ContainsKey(panelId))
            {
                return _dicPanels[panelId];
            }
            else
                return null;
        }
        
        public void AddVisiblePanel( string panelId)
        {
            PanelBaseVieWModel panelVm = GetPanelFromDic(panelId);
            if (panelVm != null && !_tabs.Contains(panelVm))
                _tabs.Add(panelVm);

            SelectedTab = panelVm;
        }



        public void AddSpriteBank()
        {
            EditSpriteBankViewModel editSpritevm = new EditSpriteBankViewModel(this);
            RightPanViewModel = editSpritevm;      
            
            // TODO: Poner disabled botón de AddSpriteBank      
       }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
