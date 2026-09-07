using System.Globalization;
using System.Windows.Data;
using Fanote.Core;

namespace Fanote.Windowing;

/// <summary>
/// Texto de la nota -> segunda línea de su pestaña: el progreso de sus tareas si las tiene, y lo
/// que va después del título colapsado en una línea (ver <see cref="NoteTitleHelper.GetTabPreview"/>).
/// Vacío cuando la nota solo tiene título.
/// </summary>
public sealed class NoteSnippetConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        NoteTitleHelper.GetTabPreview(value as string ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
