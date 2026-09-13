using System.Globalization;
using System.Windows.Data;
using Fanote.Core;
using Fanote.Resources;

namespace Fanote.Windowing;

public sealed class NoteTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Note note && note.IsProtected
            ? Strings.ProtectedNote
            : NoteTitleHelper.GetTitle(value is Note unprotectedNote ? unprotectedNote.Text : string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
