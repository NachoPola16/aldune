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
        // "27 sept" y no "Sep 27". La hora, en el formato corto de cada cultura (16:48 / 4:48 PM).
        var format = new CultureInfo(UiLanguages.CultureFor(Strings.Current));
        var local = updatedAt.ToLocalTime();
        if (parameter as string == "full")
            return $"{local.ToString("D", format)}, {local.ToString("t", format)}";

        var (dayMonth, dayMonthYear) = Strings.Current switch
        {
            "en" => ("MMM d", "MMM d, yyyy"),
            "de" => ("d. MMM", "d. MMM yyyy"),
            _ => ("d MMM", "d MMM yyyy")
        };
        return NoteListing.DateKind(updatedAt, DateTimeOffset.Now) switch
        {
            NoteDateKind.Today => local.ToString("t", format),
            NoteDateKind.ThisYear => local.ToString(dayMonth, format),
            _ => local.ToString(dayMonthYear, format)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
