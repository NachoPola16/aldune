namespace Fanote.Core;

public static class EdgeGeometry
{
    // Thick enough to show a small rounded color swatch per note (see EdgeDockWindow's
    // PillSwatches), not just a bare hairline.
    public const double PillThickness = 20;

    // The pill's length (along the edge) grows with how many notes there are instead of always
    // being a fixed size — with only a couple of notes, a fixed-max pill left a lot of empty
    // space below the swatches; with many, it still caps out so it doesn't take over the screen.
    public const double PillPerNoteLength = 20; // matches a swatch's own height + margins
    public const double PillMinLength = 60;
    public const double PillMaxLength = 160;

    public const double ExpandedThickness = 220;
    // Bumped from an original 40 to 88 to give the rotated vertical tab label real room —
    // LayoutTransform's -90° rotation swaps measure axes, so a tab's Height becomes the
    // rotated TextBlock's available WIDTH; a 40px (or 36px rendered) tab left only ~24px for
    // text after padding, trimming every title to 1-2 characters. 88 (80px tab + 4+4 margin)
    // gives ~68px of room — enough for a reasonably short note title before ellipsis.
    public const double ExpandedPerNoteLength = 88;
    public const double ExpandedMinLength = 120;
    // Must stay an exact multiple of ExpandedPerNoteLength. Once noteCount is large enough to
    // hit this clamp, the ScrollViewer's initial (unscrolled) viewport is exactly this many
    // pixels tall — if it isn't a whole number of tab-footprints, the last tab that fits
    // straddles the viewport edge and renders half-cut from the very first hover, before the
    // user has scrolled at all. 352 = 4 * 88 (4 full tabs visible before needing to scroll).
    public const double ExpandedMaxLength = 352;

    // EdgeDockWindow.xaml pins the "+"/gear buttons in their own fixed Grid row below the
    // scrolling tab list, outside the ScrollViewer, so they're always reachable without
    // scrolling (see the Important #4 fix in the fan-tabs redesign). That row's footprint has
    // to come out of the window's own length budget in addition to what the tabs need, or the
    // tabs get squeezed into less room than ExpandedPerNoteLength assumes and the last one ends
    // up clipped by the ScrollViewer's own viewport instead of merely needing a scroll. Two
    // CircularIconButtonStyle buttons (32px + 4+4 margin each = 40) stacked = 80.
    public const double ExpandedFooterLength = 80;

    // Room for the rounded corners and drop shadow the resting pill gets from the OS (see
    // NativeMethods) — without this gap they'd have nothing to render into at the screen edge.
    public const double PillEdgeMargin = 6;

    private static double ClampedLength(int noteCount, double perNote, double min, double max) =>
        Math.Clamp(noteCount * perNote, min, max);

    private static double PillLength(int noteCount) =>
        ClampedLength(noteCount, PillPerNoteLength, PillMinLength, PillMaxLength);

    private static double ExpandedLength(int noteCount) =>
        ClampedLength(noteCount, ExpandedPerNoteLength, ExpandedMinLength, ExpandedMaxLength) + ExpandedFooterLength;

    // The along-edge coordinate (Y for Left/Right docks, X for Top/Bottom) where the length axis
    // starts, computed from the PILL's own length — used for both PillRect and ExpandedRect so
    // the expanded panel always starts exactly where the pill starts and only ever extends past
    // it in one direction. Centering each rect independently (by its own length) used to make the
    // panel's start slide one way while its end dropped the other way as it opened — two edges
    // moving apart from a freshly-recomputed center, rather than one fixed point with the panel
    // simply extending past it. Real user feedback (2026-09-04): that dual-direction motion read
    // as "the growth doesn't make sense" — this anchor is the fix.
    private static double AnchorStart(WorkingArea area, EdgePosition edge, int noteCount)
    {
        double pillLength = PillLength(noteCount);
        return edge switch
        {
            EdgePosition.Top or EdgePosition.Bottom => area.X + (area.Width - pillLength) / 2,
            EdgePosition.Left or EdgePosition.Right => area.Y + (area.Height - pillLength) / 2,
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    public static Rect PillRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        double length = PillLength(noteCount);
        double anchorStart = AnchorStart(area, edge, noteCount);
        return edge switch
        {
            EdgePosition.Top => new Rect(
                anchorStart, area.Y + PillEdgeMargin, length, PillThickness),
            EdgePosition.Bottom => new Rect(
                anchorStart, area.Y + area.Height - PillThickness - PillEdgeMargin, length, PillThickness),
            EdgePosition.Left => new Rect(
                area.X + PillEdgeMargin, anchorStart, PillThickness, length),
            EdgePosition.Right => new Rect(
                area.X + area.Width - PillThickness - PillEdgeMargin, anchorStart, PillThickness, length),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    public static Rect ExpandedRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        double length = ExpandedLength(noteCount);
        double anchorStart = AnchorStart(area, edge, noteCount); // same anchor as the pill — see its remarks
        return edge switch
        {
            EdgePosition.Top => new Rect(
                anchorStart, area.Y, length, ExpandedThickness),
            EdgePosition.Bottom => new Rect(
                anchorStart, area.Y + area.Height - ExpandedThickness, length, ExpandedThickness),
            EdgePosition.Left => new Rect(
                area.X, anchorStart, ExpandedThickness, length),
            EdgePosition.Right => new Rect(
                area.X + area.Width - ExpandedThickness, anchorStart, ExpandedThickness, length),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }
}
