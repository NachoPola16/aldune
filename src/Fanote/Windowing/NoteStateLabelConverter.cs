using System.Globalization;
using System.Windows.Data;
using Fanote.Core;
using Fanote.Resources;

namespace Fanote.Windowing;

public sealed class NoteStateLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            NoteState.Archived => Strings.Archived,
            NoteState.Trashed => Strings.Trash,
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
