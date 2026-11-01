using System.Globalization;
using System.Windows.Data;

namespace Aldune.Windowing;

/// <summary>
/// Horizontal shift of the note's scroll bar from the right margin of the text box (a negative number: the box reaches past the body host, see
/// ApplySkin). The bar sits at the box's right edge and its visual place is 15 px beyond the host's edge, so the shift is 15 plus that margin.
/// </summary>
public sealed class NoteBarShiftConverter : IValueConverter
{
    private const double BaseShift = 15;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BaseShift + (value is double margin ? margin : 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
