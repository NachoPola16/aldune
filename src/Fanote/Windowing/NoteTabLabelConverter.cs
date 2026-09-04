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
/// Se corta a <see cref="MaxCharacters"/> sin puntos suspensivos: un lomo es una etiqueta de
/// archivador, y "COMPRA" cortado se sigue reconociendo, mientras que "COMP…" gasta un carácter
/// de los pocos que hay en decir que falta algo.
/// </summary>
public sealed class NoteTabLabelConverter : IValueConverter
{
    private const int MaxCharacters = 9;
    private const char ThinSpace = ' ';

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var title = NoteTitleHelper.GetTitle(value as string ?? string.Empty);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        title = title.Trim().ToUpper(culture);
        if (title.Length > MaxCharacters) title = title[..MaxCharacters].TrimEnd();

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
