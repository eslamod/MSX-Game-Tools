using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace MSX_GameTools.Localization;

/// <summary>
/// Un texto traducido, para el XAML: <c>Header="{l:Localize MenuFile}"</c>.
/// </summary>
/// <remarks>
/// Devuelve un enlace y no el texto ya resuelto: si devolviera la cadena, cambiar de
/// idioma no movería nada de lo que ya está en pantalla. Y a una propiedad normal y no al
/// indizador del <see cref="Localizer"/>, que Avalonia no reevalúa aunque se le avise.
/// </remarks>
public sealed class LocalizeExtension(string key) : MarkupExtension
{
    /// <summary>La clave del texto en el fichero de recursos.</summary>
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(LocalizedText.Value))
        {
            Source = Localizer.Instance.Bind(Key),
            Mode = BindingMode.OneWay,
        };
}
