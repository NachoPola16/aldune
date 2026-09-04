using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fanote.Windowing;

/// <summary>
/// Color de nota -> pincel de su borde (ver <see cref="NoteColorPalette.RimFor"/>). Existe porque
/// una pestaña no puede llevar sombra: la spec v1 descarta AllowsTransparency para no romper
/// ClearType, así que todo píxel dentro de la región recortada es opaco y el borde de 1px del
/// mismo hue es lo único que separa una pestaña de la de al lado y del escritorio.
/// </summary>
public sealed class NoteRimConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value as string ?? string.Empty;
        var rim = NoteColorPalette.RimFor(color);

        try
        {
            return (Brush)new BrushConverter().ConvertFromString(rim)!;
        }
        catch (FormatException)
        {
            // Una nota guardada por una versión anterior puede llevar un hex que ya no está en la
            // paleta; RimFor lo devuelve tal cual, y si además no fuese parseable no vale la pena
            // tumbar el binding entero por un borde.
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
