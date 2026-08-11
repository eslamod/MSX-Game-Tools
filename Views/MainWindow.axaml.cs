using System.ComponentModel;
using Avalonia.Controls;
using MSX_SpritesEditor.ViewModels;

namespace MSX_SpritesEditor.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _watched;

    /// <summary>La columna del lateral. Una definicion de columna no genera campo.</summary>
    private ColumnDefinition RightColumn => MainArea.ColumnDefinitions[4];

    public MainWindow() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_watched is not null)
            _watched.PropertyChanged -= OnMainChanged;

        _watched = DataContext as MainWindowViewModel;

        if (_watched is not null)
            _watched.PropertyChanged += OnMainChanged;

        ApplyRightColumn();
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.RightPanViewModel))
            ApplyRightColumn();
    }

    /// <summary>
    /// Abre o cierra la columna del lateral.
    /// </summary>
    /// <remarks>
    /// Los topes se ponen y se quitan aquí en vez de dejarlos fijos en el XAML: un
    /// <c>MinWidth</c> permanente mantendría la columna abierta con el lateral vacío,
    /// y sin él el separador podría estrecharla hasta dejarla inservible.
    /// </remarks>
    private void ApplyRightColumn()
    {
        bool open = _watched?.RightPanViewModel is not null;

        // Al cerrar se guarda lo que dejó el separador, para volver a abrir con ese ancho.
        if (!open && _watched is not null && RightColumn.ActualWidth > 0)
            _watched.RightPanelWidth = RightColumn.ActualWidth;

        RightColumn.MinWidth = open ? MainWindowViewModel.MinRightPanelWidth : 0;
        RightColumn.MaxWidth = open ? MainWindowViewModel.MaxRightPanelWidth : 0;
        RightColumn.Width = new GridLength(open ? _watched!.RightPanelWidth : 0);
    }
}
