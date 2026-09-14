using System.Globalization;
using System.Windows.Data;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

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
