using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// Contorno de los guiones de la tira de reposo del dock (ver
/// <see cref="Aldune.Core.NoteColorDerivation.RestOutlineFor"/>). Con ConverterParameter="Thickness"
/// devuelve el grosor: 0 para las caras claras, que así no pierden ni un píxel de relleno, porque un
/// Border pinta su fondo dentro del borde aunque este sea transparente.
/// </summary>
public sealed class NoteRestOutlineConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var outline = Aldune.Core.NoteColorDerivation.RestOutlineFor(value as string);

        if (parameter as string == "Thickness")
            return new Thickness(outline is null ? 0 : 1);

        if (outline is null) return Brushes.Transparent;
        var brush = (Brush)new BrushConverter().ConvertFromString(outline)!;
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
