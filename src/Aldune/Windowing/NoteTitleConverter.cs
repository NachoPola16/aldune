using System.Globalization;
using System.Windows.Data;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

public sealed class NoteTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Note note && note.IsProtected
            ? Strings.ProtectedNote
            : value is Note unprotectedNote ? LinkedNoteDisplay.Title(unprotectedNote) : NoteTitleHelper.GetTitle(string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
