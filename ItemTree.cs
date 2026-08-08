using MSX_SpritesEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSX_SpritesEditor
{
    public class ItemTree:INotifyPropertyChanged
    {
        public string DisplayText { get; set; }
        public string Type { get; set; }

        public int Id { get; set; }
        public string Tag { get; set; }

        private ObservableCollection<ItemTree> _childs;
        public ObservableCollection<ItemTree> Childs
        {
            get
            {
                return _childs;
            }
        }

        public ItemTree()
        {
            _childs = new ObservableCollection<ItemTree>();
            _panelsList = new List<PanelBaseVieWModel>();
        }

        private IList<PanelBaseVieWModel> _panelsList;



        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        public IList<PanelBaseVieWModel> PanelsList
        {
            get
            {
                return _panelsList;
            }
        }

        
    }
}
