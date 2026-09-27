using System.Globalization;
using System.Windows.Data;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Fecha de la última edición en la lista de "Gestionar notas": hora si es de hoy, día y mes si es
/// de este año, fecha completa si es anterior (ver <see cref="NoteListing.DateKind"/>). Con
/// <c>ConverterParameter="full"</c> da la fecha y hora completas, para el tooltip.
/// </summary>
public sealed class NoteDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset updatedAt) return string.Empty;

        // El idioma de la interfaz, no el del sistema: con Windows en inglés y Aldune en español,
        // "27 sept" y no "Sep 27".
        bool spanish = Strings.Current == "es";
        var format = spanish ? new CultureInfo("es-ES") : new CultureInfo("en-US");
        var local = updatedAt.ToLocalTime();
        if (parameter as string == "full")
            return local.ToString(spanish ? "dddd, d 'de' MMMM 'de' yyyy, HH:mm" : "dddd, MMMM d, yyyy, h:mm tt", format);

        return NoteListing.DateKind(updatedAt, DateTimeOffset.Now) switch
        {
            NoteDateKind.Today => local.ToString(spanish ? "HH:mm" : "h:mm tt", format),
            NoteDateKind.ThisYear => local.ToString(spanish ? "d MMM" : "MMM d", format),
            _ => local.ToString(spanish ? "d MMM yyyy" : "MMM d, yyyy", format)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
