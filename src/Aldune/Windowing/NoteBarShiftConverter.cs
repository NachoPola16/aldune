using System.Globalization;
using System.Windows.Data;

namespace Aldune.Windowing;

/// <summary>
/// Horizontal shift of the note's scroll bar from the right margin of the text box (a negative number: the box reaches past the body host, see
/// ApplySkin). The bar sits at the box's right edge and it is centred under the close button (24 px from the note's edge), so the shift is 5 plus that margin.
/// </summary>
public sealed class NoteBarShiftConverter : IValueConverter
{
    private const double BaseShift = 5;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BaseShift + (value is double margin ? margin : 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
