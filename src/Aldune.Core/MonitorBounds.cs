namespace Aldune.Core;

/// <summary>
/// Geometry helpers for keeping a moving window inside the connected monitor work areas.
/// </summary>
public static class MonitorBounds
{
    private readonly record struct Interval(double Start, double End);

    /// <summary>
    /// Keeps the complete window inside the union of the connected work areas. A window may still
    /// overlap two monitors while crossing their shared edge, but it cannot hang off an outer edge
    /// or enter the empty space below/above a monitor with a different size.
    /// </summary>
    public static (double Left, double Top) ClampIntoMonitorUnion(
        double left,
        double top,
        double width,
        double height,
        IEnumerable<MonitorInfo> monitors)
    {
        var monitorList = monitors.ToList();
        if (monitorList.Count == 0)
            return (left, top);

        // Clamp both axes more than once because the valid range on one axis depends on the
        // current span on the other one. We do the cross-axis (vertical) correction first so a
        // note being dragged down past the shorter monitor stops at that monitor's bottom edge,
        // instead of jumping sideways onto the taller monitor. The extra passes handle offset or
        // stacked displays too.
        for (int i = 0; i < 3; i++)
        {
            top = ClampAxis(top, height, left, width, monitorList, horizontal: false);
            left = ClampAxis(left, width, top, height, monitorList, horizontal: true);
        }

        return (left, top);
    }

    private static double ClampAxis(
        double desiredStart,
        double size,
        double crossStart,
        double crossSize,
        IReadOnlyList<MonitorInfo> monitors,
        bool horizontal)
    {
        var crossEnd = crossStart + crossSize;
        var boundaries = monitors
            .SelectMany(m =>
            {
                var area = m.WorkArea;
                double start = horizontal ? area.Y : area.X;
                double end = start + (horizontal ? area.Height : area.Width);
                return new[] { start, end };
            })
            .Append(crossStart)
            .Append(crossEnd)
            .Where(value => value >= crossStart && value <= crossEnd)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        if (boundaries.Length < 2)
            return ClampToOuterBounds(desiredStart, size, monitors, horizontal);

        IReadOnlyList<Interval>? allowedStarts = null;
        for (int i = 0; i < boundaries.Length - 1; i++)
        {
            double segmentStart = boundaries[i];
            double segmentEnd = boundaries[i + 1];
            if (segmentEnd - segmentStart < 0.001) continue;

            double midpoint = (segmentStart + segmentEnd) / 2;
            var coveredAxis = monitors
                .Where(m => CoversCrossCoordinate(m.WorkArea, midpoint, horizontal))
                .Select(m => ToAxisInterval(m.WorkArea, horizontal))
                .OrderBy(interval => interval.Start)
                .ToList();

            var segmentStarts = Merge(coveredAxis)
                .Select(interval => new Interval(interval.Start, interval.End - size))
                .Where(interval => interval.End >= interval.Start)
                .ToList();

            if (segmentStarts.Count == 0)
                return ClampToOuterBounds(desiredStart, size, monitors, horizontal);

            allowedStarts = allowedStarts is null
                ? segmentStarts
                : Intersect(allowedStarts, segmentStarts);

            if (allowedStarts.Count == 0)
                return ClampToOuterBounds(desiredStart, size, monitors, horizontal);
        }

        return ClampToIntervals(desiredStart, allowedStarts ?? Array.Empty<Interval>());
    }

    private static bool CoversCrossCoordinate(WorkingArea area, double coordinate, bool horizontal) =>
        horizontal
            ? coordinate >= area.Y && coordinate < area.Y + area.Height
            : coordinate >= area.X && coordinate < area.X + area.Width;

    private static Interval ToAxisInterval(WorkingArea area, bool horizontal) => horizontal
        ? new Interval(area.X, area.X + area.Width)
        : new Interval(area.Y, area.Y + area.Height);

    private static List<Interval> Merge(IEnumerable<Interval> intervals)
    {
        var merged = new List<Interval>();
        foreach (var interval in intervals)
        {
            if (merged.Count == 0 || interval.Start > merged[^1].End)
            {
                merged.Add(interval);
                continue;
            }

            var previous = merged[^1];
            merged[^1] = new Interval(previous.Start, Math.Max(previous.End, interval.End));
        }

        return merged;
    }

    private static List<Interval> Intersect(IEnumerable<Interval> first, IEnumerable<Interval> second)
    {
        var result = new List<Interval>();
        foreach (var left in first)
        foreach (var right in second)
        {
            double start = Math.Max(left.Start, right.Start);
            double end = Math.Min(left.End, right.End);
            if (end >= start) result.Add(new Interval(start, end));
        }

        return Merge(result);
    }

    private static double ClampToIntervals(double desiredStart, IReadOnlyList<Interval> intervals)
    {
        if (intervals.Count == 0) return desiredStart;
        if (intervals.Any(interval => desiredStart >= interval.Start && desiredStart <= interval.End))
            return desiredStart;

        var nearest = intervals
            .Select(interval => desiredStart < interval.Start ? interval.Start : interval.End)
            .OrderBy(value => Math.Abs(value - desiredStart))
            .First();
        return nearest;
    }

    private static double ClampToOuterBounds(
        double desiredStart,
        double size,
        IEnumerable<MonitorInfo> monitors,
        bool horizontal)
    {
        double min = monitors.Min(m => horizontal ? m.WorkArea.X : m.WorkArea.Y);
        double max = monitors.Max(m => horizontal
            ? m.WorkArea.X + m.WorkArea.Width
            : m.WorkArea.Y + m.WorkArea.Height);
        return Math.Clamp(desiredStart, min, Math.Max(min, max - size));
    }
}
