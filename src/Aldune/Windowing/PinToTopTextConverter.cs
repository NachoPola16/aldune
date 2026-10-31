using System.Globalization;
using System.Windows.Data;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>Texto de la chincheta de «fijar arriba» según el estado: lo que hará al pulsarla (ver <see cref="Strings.PinToTop"/>).</summary>
public sealed class PinToTopTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Strings.UnpinFromTop : Strings.PinToTop;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
