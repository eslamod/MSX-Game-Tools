using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MSX_SpritesEditor.ViewModels;

namespace MSX_SpritesEditor.Entities;

/// <summary>Nodo del árbol de proyecto (bancos de sprites, tilesets, mapas...).</summary>
public partial class ItemTree : ObservableObject
{
    [ObservableProperty]
    private string _displayText = string.Empty;

    public string? Type { get; set; }

    public int Id { get; set; }

    public string Tag { get; set; } = string.Empty;

    public ObservableCollection<ItemTree> Childs { get; } = [];

    public IList<PanelBaseViewModel> PanelsList { get; } = [];
}
