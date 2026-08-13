using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _watched;

    /// <summary>Ya se ha preguntado por los cambios sin guardar y toca cerrar de verdad.</summary>
    private bool _confirmed;

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

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Cerrar avisa de lo que está sin guardar.
    /// </summary>
    /// <remarks>
    /// Preguntar es asíncrono y esto no lo es: se para el cierre, se pregunta, y si se
    /// puede seguir se vuelve a cerrar, ya sin preguntar. Vale igual para el aspa del
    /// título que para el menú, que también cierra la ventana.
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_confirmed || e.Cancel || _watched is null)
            return;

        e.Cancel = true;

        // Se pregunta fuera de este manejador y no aquí dentro: cerrar de nuevo mientras
        // se está atendiendo un cierre es volver a entrar por donde se ha salido.
        MainWindowViewModel main = _watched;

        Dispatcher.UIThread.Post(() => _ = CloseWhenConfirmedAsync(main));
    }

    private async Task CloseWhenConfirmedAsync(MainWindowViewModel main)
    {
        if (!await main.ConfirmExitAsync())
            return;

        _confirmed = true;

        Close();
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
