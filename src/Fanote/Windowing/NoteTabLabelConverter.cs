using System.Globalization;
using System.Text;
using System.Windows.Data;
using Fanote.Core;

namespace Fanote.Windowing;

/// <summary>
/// Texto de la nota -> etiqueta del lomo: la primera línea, en mayúsculas y con tracking.
///
/// El tracking se hace insertando espacios entre caracteres porque WPF no tiene ninguna propiedad
/// de espaciado entre letras — <c>CharacterSpacing</c> existe en WinUI, no aquí. A este tamaño
/// (11px, en mayúsculas, girado 90°) el tracking no es decorativo: sin él las mayúsculas se
/// apelmazan y la etiqueta se lee peor girada que horizontal.
///
/// Se usa el espacio <b>capilar</b> (U+200A) y no el fino (U+2009): con el fino, "NUEVA NOTA"
/// medía más que el alto de la pestaña y salía cortada por abajo — el tracking se estaba comiendo
/// un 25% de la longitud. Comprobado renderizando ambas anchuras en aislamiento.
///
/// Y el espacio de la propia frase se sustituye por uno duro (U+00A0): es el único hueco donde el
/// texto podría partirse en dos líneas, y una etiqueta girada partida en dos columnas es
/// ilegible.
///
/// <b>No</b> recorta por numero de caracteres. Antes habia un tope duro de 9, elegido a ojo,
/// y "NUEVA NOTA" (el titulo por defecto de una nota recien creada) tiene 10 — salia siempre
/// cortada como "NUEVA NOT". Ahora recorta el propio TextBlock, con elipsis, y solo cuando
/// de verdad no cabe en el alto de la pestana.
/// </summary>
public sealed class NoteTabLabelConverter : IValueConverter
{
    // Escapes explicitos y no los caracteres literales: son invisibles en el editor y ya se
    // perdio uno una vez editando este fichero con herramientas de texto.
    private const char HairSpace = '\u200A';
    private const char NoBreakSpace = '\u00A0';

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var title = NoteTitleHelper.GetTitle(value as string ?? string.Empty);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        title = title.Trim().ToUpper(culture).Replace(' ', NoBreakSpace);

        var builder = new StringBuilder(title.Length * 2);
        for (int i = 0; i < title.Length; i++)
        {
            if (i > 0) builder.Append(HairSpace);
            builder.Append(title[i]);
        }
        return builder.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
