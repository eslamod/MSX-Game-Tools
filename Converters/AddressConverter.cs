using System.Globalization;
using Avalonia.Data.Converters;

namespace MSX_GameTools.Converters;

/// <summary>
/// Una dirección de VRAM como se escribe en el mundo del MSX: cuatro dígitos y una H.
/// </summary>
/// <remarks>
/// En decimal no se reconocen. Las tablas caen en múltiplos de 2 KB y en hexadecimal saltan a
/// la vista —0000H, 1800H, 2000H, 3800H—, que es como vienen en los manuales y en el propio
/// ensamblador que exporta este programa.
/// </remarks>
public sealed class AddressConverter : IValueConverter
{
    public static readonly AddressConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IConvertible number ? $"{number.ToInt32(culture):X4}H" : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
