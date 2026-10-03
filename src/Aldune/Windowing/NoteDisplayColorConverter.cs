using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>Color guardado de la nota → pincel con el que se pinta (ver <see cref="NoteColorDisplay"/>).</summary>
public sealed class NoteDisplayColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(NoteColorDisplay.Resolve(value as string))!;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
