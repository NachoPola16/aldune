using System.Globalization;
using System.Text;
using System.Windows.Data;
using Fanote.Core;

namespace Fanote.Windowing;

/// <summary>
/// Texto de la nota -> etiqueta del lomo: la primera línea, en mayúsculas y con tracking.
///
/// El tracking se hace insertando espacios finos (U+2009) entre caracteres porque WPF no tiene
/// ninguna propiedad de espaciado entre letras — <c>CharacterSpacing</c> existe en WinUI, no aquí.
/// A este tamaño (11px, en mayúsculas, girado 90°) el tracking no es decorativo: sin él las
/// mayúsculas se apelmazan y la etiqueta se lee peor girada que horizontal.
///
/// <b>No</b> recorta por numero de caracteres. Antes habia un tope duro de 9, elegido a ojo,
/// y "NUEVA NOTA" (el titulo por defecto de una nota recien creada) tiene 10 — salia siempre
/// cortada como "NUEVA NOT". Ahora recorta el propio TextBlock, con elipsis, y solo cuando
/// de verdad no cabe en el alto de la pestana.
/// </summary>
public sealed class NoteTabLabelConverter : IValueConverter
{
    // Escape explicito y no el caracter literal: U+2009 es invisible en el editor y ya se
    // perdio una vez editando este fichero con herramientas de texto.
    private const char ThinSpace = '\u2009';

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var title = NoteTitleHelper.GetTitle(value as string ?? string.Empty);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        title = title.Trim().ToUpper(culture);

        var builder = new StringBuilder(title.Length * 2);
        for (int i = 0; i < title.Length; i++)
        {
            if (i > 0) builder.Append(ThinSpace);
            builder.Append(title[i]);
        }
        return builder.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
