using System.Globalization;
using System.Windows.Data;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Texto de la nota -> segunda línea de su pestaña: el progreso de sus tareas si las tiene, y lo
/// que va después del título colapsado en una línea (ver <see cref="NoteTitleHelper.GetTabPreview"/>).
/// Vacío cuando la nota solo tiene título.
/// </summary>
public sealed class NoteSnippetConverter : IValueConverter
{
    /// <summary>
    /// Refleja <c>AppSettings.ShowNotePreview</c>. Estático, como <see cref="Strings.Current"/>: el
    /// mismo conversor sirve al dock y a "Gestionar notas", y apagarlo aquí apaga los dos a la vez
    /// (quien lo cambia llama a <c>RefreshAll</c> para que se vuelva a evaluar). Vacío ya colapsa la
    /// línea en los dos sitios.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    // Recibe la nota entera (hace falta para saber si está protegida). Desde que se añadieron las
    // protegidas el dock le pasaba la nota pero aquí se seguía leyendo "value as string", así que la
    // vista previa salía siempre vacía.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        _ when !Enabled => string.Empty,
        Note { IsProtected: true } => string.Empty,
        Note note => NoteTitleHelper.GetTabPreview(note.Text),
        string text => NoteTitleHelper.GetTabPreview(text),
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
