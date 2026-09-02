using System.Globalization;
using System.Windows.Data;
using Fanote.Core;

namespace Fanote.Windowing;

public sealed class NoteStateLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            NoteState.Archived => "Archivada",
            NoteState.Trashed => "Papelera",
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
