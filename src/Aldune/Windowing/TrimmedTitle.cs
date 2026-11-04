using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// The title of a note as plain text with an ellipsis, drawn over the title box while it is not being edited. A <see cref="TextBox"/> can only
/// cut its text at the edge (half a letter and no «…»), so when the title does not fit the box's own glyphs go transparent and this draws the
/// same text, from the same font and at the same place, ending in «…». It is a bare <see cref="FrameworkElement"/> on purpose: it has no
/// automation peer, so a screen reader reads the title box once and never this copy.
/// </summary>
internal sealed class TrimmedTitle : FrameworkElement
{
    private readonly TextBox _source;
    private Brush _ink = Brushes.Black;

    internal TrimmedTitle(TextBox source)
    {
        _source = source;
        IsHitTestVisible = false;
        Focusable = false;
    }

    internal void SetInk(Brush ink)
    {
        _ink = ink;
        InvalidateVisual();
    }

    internal void Refresh()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private FormattedText Build(double width)
    {
        var typeface = new Typeface(_source.FontFamily, _source.FontStyle, _source.FontWeight, _source.FontStretch);
        return new FormattedText(_source.Text, CultureInfo.CurrentUICulture, FlowDirection, typeface, _source.FontSize, _ink,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, _source.Text.Length == 0 || width <= 0 ? 0 : Build(width).Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_source.Text.Length == 0 || ActualWidth <= 0) return;
        drawingContext.DrawText(Build(ActualWidth), new Point(0, 0));
    }
}
