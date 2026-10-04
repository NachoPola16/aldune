using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>Visible si la nota es un archivo vinculado. Con ConverterParameter="Path", la ruta (tooltip).</summary>
public sealed class NoteLinkedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Note note) return parameter as string == "Path" ? null : Visibility.Collapsed;
        return parameter as string == "Path"
            ? LinkedNoteDisplay.PathOf(note.Id)
            : LinkedNoteDisplay.IsLinked(note.Id) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
