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

    /// <summary>
    /// The face of a row: the note colour with its chroma reduced (same lightness and hue). Five saturated bars in a column are heavy next to the
    /// calm chrome; the note keeps its exact colour on the dock and in its own window, and the rim and the stripe of a row stay the full colour.
    /// The ink and the secondary ink are derived from THIS face, so contrast is guaranteed on what is really drawn.
    /// </summary>
    public const double FaceChroma = 0.6;

    public static Brush BrushOf(string hex, Brush fallback)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    public static string SoftFace(object? noteColor) =>
        NoteColorDerivation.SoftenFace(NoteColorDisplay.Resolve(noteColor as string), FaceChroma);
}

public sealed class ManagerRowFaceConverter : IValueConverter
{
    // Built here and not through NoteDisplayColorConverter: that one resolves the colour again, and with a uniform note colour it would
    // replace the softened face by the uniform one.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? ManagerRows.Resource("AlduneSurfaceBrush") : ManagerRows.BrushOf(ManagerRows.SoftFace(value), Brushes.Transparent);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ManagerRowInkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? ManagerRows.Resource("AlduneTextBrush")
            : ThemeManager.IsHighContrast ? ManagerRows.BrushOf(NoteColorContrast.HighContrastInkFor(ManagerRows.SoftFace(value)), Brushes.Black)
            : ManagerRows.BrushOf(NoteColorPalette.LabelFor(ManagerRows.SoftFace(value)), Brushes.Black);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Secondary text of a row (date, snippet): the row's ink softened as far as still reads at 4.5:1 on its face (an opacity could not promise that).</summary>
public sealed class ManagerRowMutedInkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ManagerRows.ColourOnEdgeOnly) return ManagerRows.Resource("AlduneMutedTextBrush");
        string face = ManagerRows.SoftFace(value);
        // High contrast: the secondary text is as strong as the main one (7:1), not softened to 4.5:1.
        var muted = ThemeManager.IsHighContrast ? NoteColorContrast.HighContrastInkFor(face)
            : Aldune.Core.NoteColorContrast.MutedInkFor(face, NoteColorPalette.LabelFor(face));
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(muted));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ManagerRowRimConverter : IValueConverter
{
    private static readonly NoteRimConverter Filled = new();

    // With the colour on the edge only, the colour is the stripe on the left (the dock card's own, so a note has the same colour on its tab and in
    // its row) and the 1 px outline is the look's neutral line. When the outline was the note's colour too, it met the thicker stripe at the top
    // and bottom corners in a sharp point (and a coloured frame around every row was heavy next to the calm chrome).
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ManagerRows.ColourOnEdgeOnly) return ManagerRows.Resource("AlduneBorderSubtleBrush");
        var rim = Filled.Convert(value is Note note ? note.Color : value, targetType, parameter, culture);
        return SoftenedAgainstFace(rim, value is Note n ? n.Color : value as string);
    }

    // A 1 px rim much darker than the face doubles up along the diagonal of a rounded corner (the curve covers two pixels where a straight
    // edge covers one) and reads as a dark smear around each corner. Halfway between the rim and the face keeps the outline and loses the smear.
    private static object SoftenedAgainstFace(object rim, string? faceColor)
    {
        if (rim is not SolidColorBrush { Color: var rimColor } || faceColor is null) return rim;
        try
        {
            var face = (Color)ColorConverter.ConvertFromString(faceColor);
            var mixed = Color.FromArgb(rimColor.A,
                (byte)((rimColor.R + face.R) / 2), (byte)((rimColor.G + face.G) / 2), (byte)((rimColor.B + face.B) / 2));
            var brush = new SolidColorBrush(mixed);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return rim;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The stripe of a row takes the row's own left corners (none with square corners), so it never pokes out of a squared row.</summary>
public sealed class LeftCornersConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CornerRadius radius ? new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft) : new CornerRadius(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Width of the row's stripe: at least 5 px, and at least the corner radius, so that its outer edge follows the row's own rounded corner
/// (a piece narrower than the radius cut the corner short). With the parameter «cover»: how much of it lies beyond the 5 px of the stripe.
/// </summary>
public sealed class StripeWidthConverter : IValueConverter
{
    private const double Stripe = 5;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double width = Math.Max(Stripe, value is CornerRadius radius ? radius.TopLeft : 0);
        return parameter as string == "cover" ? width - Stripe : width;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible only when the colour is on the edge alone: the stripe of the row.</summary>
public sealed class ManagerRowStripeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ManagerRows.ColourOnEdgeOnly ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
