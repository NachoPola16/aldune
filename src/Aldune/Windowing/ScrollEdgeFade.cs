using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// Soft edges for a scrolling list that sits on the window's own background: where the content continues past the top or the bottom of the
/// viewport it fades into the background instead of being cut in a straight line, and the fade is gone when the list is at that end. The same look
/// as the pages of Settings, shared by the other list windows. Two overlay borders are added to the grid the list lives in; they never take the mouse
/// and leave the scroll bar's own column uncovered, so the thumb is never dimmed.
/// </summary>
public static class ScrollEdgeFade
{
    /// <summary>Width of the vertical scroll bar the overlays leave free (see the ScrollBar style in App.xaml).</summary>
    private const double BarWidth = 16;

    /// <summary>
    /// Adds the soft edges over <paramref name="scroller"/>. <paramref name="host"/> is the grid that contains it (or one that covers the same area);
    /// <paramref name="place"/>, when the scroller sits in a cell of that grid with a margin, is the element whose row, column and margin the edges copy.
    /// </summary>
    public static void Attach(ScrollViewer scroller, Panel host, FrameworkElement? place = null, double height = 26)
    {
        var top = Edge(true, height);
        var bottom = Edge(false, height);
        foreach (var edge in new[] { top, bottom })
        {
            if (place is not null)
            {
                Grid.SetRow(edge, Grid.GetRow(place));
                Grid.SetColumn(edge, Grid.GetColumn(place));
                Grid.SetRowSpan(edge, Grid.GetRowSpan(place));
                Grid.SetColumnSpan(edge, Grid.GetColumnSpan(place));
            }
            var margin = place?.Margin ?? new Thickness(0);
            edge.Margin = new Thickness(margin.Left, margin.Top, margin.Right + BarWidth, margin.Bottom);
            Panel.SetZIndex(edge, 10);
            host.Children.Add(edge);
        }

        void Update()
        {
            top.Opacity = scroller.VerticalOffset > 1 ? 1 : 0;
            bottom.Opacity = scroller.VerticalOffset < scroller.ScrollableHeight - 1 ? 1 : 0;
        }

        scroller.ScrollChanged += (_, _) => Update();
        scroller.Loaded += (_, _) => Update();
        Update();
    }

    private static Border Edge(bool atTop, double height)
    {
        var edge = new Border
        {
            Height = height,
            VerticalAlignment = atTop ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Opacity = 0,
            // Opaque at the border of the list and clear inwards, so what scrolls under it thins out.
            OpacityMask = new LinearGradientBrush(
                atTop ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(0, 0, 0, 0),
                atTop ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(255, 0, 0, 0),
                new Point(0, 0), new Point(0, 1))
        };
        // The window's ground, by reference, so the soft edge follows the look and a change of appearance.
        edge.SetResourceReference(Border.BackgroundProperty, "AlduneGroundBrush");
        return edge;
    }
}
