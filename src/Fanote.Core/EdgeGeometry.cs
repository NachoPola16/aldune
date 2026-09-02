namespace Fanote.Core;

public static class EdgeGeometry
{
    // Thick enough to show a small rounded color swatch per note (see EdgeDockWindow's
    // PillSwatches), not just a bare hairline.
    public const double PillThickness = 20;
    public const double PillLength = 160;
    public const double ExpandedThickness = 220;
    public const double ExpandedLength = 320;

    // Room for the rounded corners and drop shadow the resting pill gets from the OS (see
    // NativeMethods) — without this gap they'd have nothing to render into at the screen edge.
    public const double PillEdgeMargin = 6;

    public static Rect PillRect(WorkingArea area, EdgePosition edge) => edge switch
    {
        EdgePosition.Top => new Rect(
            area.X + (area.Width - PillLength) / 2, area.Y + PillEdgeMargin, PillLength, PillThickness),
        EdgePosition.Bottom => new Rect(
            area.X + (area.Width - PillLength) / 2, area.Y + area.Height - PillThickness - PillEdgeMargin, PillLength, PillThickness),
        EdgePosition.Left => new Rect(
            area.X + PillEdgeMargin, area.Y + (area.Height - PillLength) / 2, PillThickness, PillLength),
        EdgePosition.Right => new Rect(
            area.X + area.Width - PillThickness - PillEdgeMargin, area.Y + (area.Height - PillLength) / 2, PillThickness, PillLength),
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };

    public static Rect ExpandedRect(WorkingArea area, EdgePosition edge) => edge switch
    {
        EdgePosition.Top => new Rect(
            area.X + (area.Width - ExpandedLength) / 2, area.Y, ExpandedLength, ExpandedThickness),
        EdgePosition.Bottom => new Rect(
            area.X + (area.Width - ExpandedLength) / 2, area.Y + area.Height - ExpandedThickness, ExpandedLength, ExpandedThickness),
        EdgePosition.Left => new Rect(
            area.X, area.Y + (area.Height - ExpandedLength) / 2, ExpandedThickness, ExpandedLength),
        EdgePosition.Right => new Rect(
            area.X + area.Width - ExpandedThickness, area.Y + (area.Height - ExpandedLength) / 2, ExpandedThickness, ExpandedLength),
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };
}
