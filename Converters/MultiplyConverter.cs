using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MSX_GameTools.Converters;

/// <summary>
/// Multiplica los valores que le llegan. Se usa para colocar una imagen de referencia
/// a escala: su tamaño en pixeles por el tamaño que tiene cada pixel en pantalla.
/// </summary>
/// <remarks>
/// Hace falta porque el zoom vive en la vista (el lado de la celda del lienzo, el de la
/// miniatura del grupo) y el tamaño de la celda de referencia en el modelo, y el
/// resultado tiene que ser 1:1 con el pixel del sprite. Si se dejara estirar la imagen
/// al hueco disponible dejaría de corresponderse con lo que hay debajo, que es
/// justamente para lo que sirve.
/// </remarks>
public sealed class MultiplyConverter : IMultiValueConverter
{
    public static readonly MultiplyConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        double result = 1;

        foreach (object? value in values)
        {
            // Sin fondo elegido no llega nada: se deja sin valor y la imagen, que
            // tampoco tiene Source, no ocupa nada.
            if (value is not IConvertible convertible)
                return AvaloniaProperty.UnsetValue;

            result *= convertible.ToDouble(culture);
        }

        return result;
    }
}
