using System.Globalization;
using System.Windows.Data;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

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
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var note = value as Note;
        var text = note?.Text ?? value as string ?? string.Empty;
        var title = note is { IsProtected: true }
            ? Strings.ProtectedNote
            : note is not null ? LinkedNoteDisplay.Title(note) : NoteTitleHelper.GetTitle(text);
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        // Mayúsculas espaciadas de siempre o el adorno de la piel (NoteLabels, en Core, con tests).
        int channel = note is null ? 0 : NoteChannelDisplay.Of(note.Id);
        return NoteLabels.Dock(title, ThemeManager.Skin.Adornment, channel, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
