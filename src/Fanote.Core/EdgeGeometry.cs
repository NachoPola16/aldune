namespace Fanote.Core;

public static class EdgeGeometry
{
    public const double PillThickness = 12;
    public const double PillLength = 160;
    public const double ExpandedThickness = 220;
    public const double ExpandedLength = 320;

    public static Rect PillRect(WorkingArea area, EdgePosition edge) => edge switch
    {
        EdgePosition.Top => new Rect(
            area.X + (area.Width - PillLength) / 2, area.Y, PillLength, PillThickness),
        EdgePosition.Bottom => new Rect(
            area.X + (area.Width - PillLength) / 2, area.Y + area.Height - PillThickness, PillLength, PillThickness),
        EdgePosition.Left => new Rect(
            area.X, area.Y + (area.Height - PillLength) / 2, PillThickness, PillLength),
        EdgePosition.Right => new Rect(
            area.X + area.Width - PillThickness, area.Y + (area.Height - PillLength) / 2, PillThickness, PillLength),
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
