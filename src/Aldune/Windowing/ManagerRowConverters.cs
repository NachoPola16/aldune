using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// The note-colour converters of the rows of Manage notes, aware of "note colour only on the edge" (<see cref="SkinCard.Stripe"/>, also the
/// terminal looks that tint): then the row keeps the app's own surface and ink and the colour is only the stripe on its left side, as on the
/// dock cards. The rows are rebuilt when the look changes, so the choice is read at conversion time.
/// </summary>
internal static class ManagerRows
{
    public static bool ColourOnEdgeOnly => SkinState.Current.Card is SkinCard.Stripe or SkinCard.Tinted;

    public static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}

public sealed class ManagerRowFaceConverter : IValueConverter
{
    private static readonly NoteDisplayColorConverter Filled = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? ManagerRows.Resource("AlduneSurfaceBrush") : Filled.Convert(value, targetType, parameter, culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ManagerRowInkConverter : IValueConverter
{
    private static readonly NoteLabelColorConverter Filled = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? ManagerRows.Resource("AlduneTextBrush") : Filled.Convert(value, targetType, parameter, culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ManagerRowRimConverter : IValueConverter
{
    private static readonly NoteRimConverter Filled = new();
    private static readonly NoteDisplayColorConverter Colour = new();

    // With the colour on the edge only, the 1 px outline of the row is the note's colour too: the line goes all round the row (the stripe on the
    // left is the thicker part of it) instead of a grey frame with a coloured bar glued to one side. Square or rounded, it follows the row.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? Colour.Convert(value, targetType, parameter, culture) : Filled.Convert(value, targetType, parameter, culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The stripe of a row takes the row's own left corners (none with square corners), so it never pokes out of a squared row.</summary>
public sealed class LeftCornersConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CornerRadius radius ? new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft) : new CornerRadius(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible only when the colour is on the edge alone: the stripe of the row.</summary>
public sealed class ManagerRowStripeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
