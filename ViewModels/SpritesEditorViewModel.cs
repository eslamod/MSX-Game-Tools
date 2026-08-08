using MSX_SpritesEditor.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MSX_SpritesEditor.ViewModels
{
    public class SpritesEditorViewModel:PanelBaseVieWModel
    {
        public delegate void DlgRefresh(Sprite currentSpr);
        public event DlgRefresh EvRefresh;

        private SpriteBank _spritesBank;
        private ColorPalette _colorPalette;

        private ICommand _NextSpriteCommand;
        private bool _canExecuteNextSprite;
        private ICommand _PreviousSpriteCommand;
        private bool _canExecutePreviousSprite;

        private ICommand _AddSpriteCommand;
        private bool _canExecuteAddSprite;
        private ICommand _DeleteSpriteCommand;
        private bool _canExecuteDeleteSprite;

        private int _numberSprites;
        private int _currentSpritePosition;


        private ObservableCollection<ImageMini> _listSpritesImages;

        public SpriteBank SpritesBank
        {
            get
            {
                return _spritesBank;
            }            
        }

        public SpritesEditorViewModel(SpriteBank bank, ColorPalette colorPalete    )
        {
            _spritesBank = bank;
            _colorPalette = colorPalete;
            currentSprite = bank.SpritesList[0];
            NumberSprites = bank.SpritesList.Count;
            CurrentSpritePosition = 1;

            _canExecuteAddSprite = true;
            _canExecuteDeleteSprite = true;
            _canExecuteNextSprite = true;
            _canExecutePreviousSprite = true;

            _AddSpriteCommand = new CommandHandler(() => AddSprite(), _canExecuteAddSprite);
            _DeleteSpriteCommand = new CommandHandler(() => DeleteSprite(), _canExecuteDeleteSprite);
            _NextSpriteCommand = new CommandHandler(() => NextSprite(), _canExecuteNextSprite);
            _PreviousSpriteCommand = new CommandHandler(() => PreviousSprite(), _canExecutePreviousSprite);

            _listSpritesImages = new ObservableCollection<ImageMini>();
        }

        private Sprite currentSprite;

        public Sprite CurrentSprite
        {
            get
            {
                return currentSprite;
            }
            set
            {
                currentSprite = value;
            }
            

        }

        public ColorPalette ColorPalette
        {
            get
            {
                return _colorPalette;
            }

            set
            {
                _colorPalette = value;
            }
        }

        public ObservableCollection<ImageMini> ImagesMiniList
        {
            get
            {
                return _listSpritesImages;
            }            
        }

        public ICommand NextSpriteCommand
        {
            get
            {
                return _NextSpriteCommand;
            }            
        }

        public ICommand PreviousSpriteCommand
        {
            get
            {
                return _PreviousSpriteCommand;
            }
            
        }

        public ICommand AddSpriteCommand
        {
            get
            {
                return _AddSpriteCommand;
            }            
        }

        public ICommand DeleteSpriteCommand
        {
            get
            {
                return _DeleteSpriteCommand;
            }            
        }


        
        public int NumberSprites
        {
            get
            {
                return _numberSprites;
            }

            set
            {
                _numberSprites = value;
                OnPropertyChanged("NumberSprites");

            }
        }

        public int CurrentSpritePosition
        {
            get
            {
                return _currentSpritePosition;
            }

            set
            {
                _currentSpritePosition = value;
                OnPropertyChanged("CurrentSpritePosition");
            }
        }

        private void AddSprite()
        {
            Sprite newSprite= _spritesBank.NewSprite();
            ImageMini imageMini = new ImageMini(ImageMini.ImagePreviewType.ImagePreview16x16);
            ImagesMiniList.Add(imageMini);
            newSprite.ImageMini = imageMini;
            NumberSprites++;
        }
        private void DeleteSprite()
        {
            if (NumberSprites > 0)
            {
                _spritesBank.DeleteSprite(_currentSpritePosition - 1);              
                NumberSprites--;
                if (CurrentSpritePosition>1)
                    CurrentSpritePosition--;
            }

        }

        private void NextSprite()
        {
            if (CurrentSpritePosition < NumberSprites)
            {
                CurrentSpritePosition++;
                CurrentSprite = _spritesBank.SpritesList[CurrentSpritePosition-1];
                if (EvRefresh != null)
                    EvRefresh(CurrentSprite);

            }
        }

        private void PreviousSprite()
        {
            if (CurrentSpritePosition > 1)
            {
                CurrentSpritePosition--;
                CurrentSprite= _spritesBank.SpritesList[CurrentSpritePosition - 1];
                if (EvRefresh != null)
                    EvRefresh(CurrentSprite);
            }
        }
    }
}
