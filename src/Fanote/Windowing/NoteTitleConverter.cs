using System.Globalization;
using System.Windows.Data;
using Fanote.Core;

namespace Fanote.Windowing;

public sealed class NoteTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return NoteTitleHelper.GetTitle(value as string ?? string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
