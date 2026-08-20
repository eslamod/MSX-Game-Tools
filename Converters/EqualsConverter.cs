using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MSX_GameTools.Converters;

/// <summary>
/// Dice si un valor es el que le pasan por parámetro, para encender un botón de una tira.
/// </summary>
/// <remarks>
/// Es lo que hace falta para cambiar un número de un puñado con un solo clic en vez de dos:
/// una tira de botones donde cada uno lleva su valor, en lugar de un desplegable que hay que
/// abrir primero. Enlazándolo así, los botones se encienden solos cuando el valor cambia por
/// otro lado —al cargar un banco, por ejemplo— sin tener que acordarse de ponerlos a mano.
/// </remarks>
public sealed class EqualsConverter : IValueConverter
{
    public static readonly EqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && Text(value) == Text(parameter);

    /// <summary>
    /// De vuelta, el botón que se enciende escribe su valor y el que se apaga no escribe nada.
    /// </summary>
    /// <remarks>
    /// Lo segundo importa: en una tira siempre se apaga uno justo antes de encenderse otro, y
    /// si el que se apaga escribiera algo, ese algo llegaría después y se comería la elección.
    /// </remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
            return BindingOperations.DoNothing;

        Type wanted = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return wanted.IsEnum
            ? Enum.Parse(wanted, Text(parameter))
            : System.Convert.ChangeType(parameter, wanted, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Se comparan como texto, que es como llega el parámetro del XAML.
    /// </summary>
    /// <remarks>
    /// Convertir el parámetro al tipo del valor obligaría a saber cuál es, y así vale igual
    /// para un número que para un enumerado sin preguntar por ninguno de los dos.
    /// </remarks>
    private static string Text(object value) =>
        System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
