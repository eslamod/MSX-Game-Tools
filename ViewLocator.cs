using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools;

/// <summary>
/// Reemplaza al antiguo <c>ViewModelFacctory</c>. Registrado en App.axaml, permite que
/// un ContentControl o un TabControl reciba un ViewModel y muestre la vista correcta
/// sin declarar un DataTemplate por cada panel.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "(sin ViewModel)" };

        var viewName = param.GetType().FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var viewType = Type.GetType(viewName);

        return viewType is not null
            ? (Control)Activator.CreateInstance(viewType)!
            : new TextBlock { Text = $"Vista no encontrada: {viewName}" };
    }

    public bool Match(object? data) => data is PanelBaseViewModel;
}
