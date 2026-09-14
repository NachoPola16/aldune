using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// Color de nota -> pincel de su etiqueta (ver <see cref="NoteColorPalette.LabelFor"/>). Aparte de
/// <see cref="NoteRimConverter"/> a propósito: el filete y el texto tienen que contrastar cosas
/// distintas, y reutilizar el tono del filete para la etiqueta la dejaba en 1.74:1.
/// </summary>
public sealed class NoteLabelColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(
                NoteColorPalette.LabelFor(value as string ?? string.Empty))!;
        }
        catch (FormatException)
        {
            return Brushes.Black;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
