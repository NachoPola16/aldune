namespace Aldune.Core;

/// <summary>
/// Turns wheel deltas into view steps for the dock. A mouse sends full notches (120) and each one is a step, as always. A precision
/// trackpad sends many small deltas per gesture, followed by a tail of inertia: those add up, and one step is given per 120 of travel,
/// no more often than <see cref="CooldownMs"/> so the inertia cannot run through every view. Not thread-safe; one per dock.
/// </summary>
public sealed class WheelNotchAccumulator
{
    public const int NotchDelta = 120;

    /// <summary>Without wheel events for this long the next one starts a new gesture (what was accumulated is dropped).</summary>
    public const int IdleResetMs = 250;

    /// <summary>Minimum time between two steps that come from small deltas (trackpad).</summary>
    public const int CooldownMs = 180;

    private int _accumulated;
    private DateTime _lastEvent = DateTime.MinValue;
    private DateTime _lastStep = DateTime.MinValue;

    /// <summary>
    /// Adds one wheel event. Returns +1 (wheel turned down or away: next view), -1 (previous view) or 0 (no step yet).
    /// </summary>
    public int Add(int delta, DateTime now)
    {
        if (delta == 0) return 0;

        bool idle = (now - _lastEvent).TotalMilliseconds > IdleResetMs;
        _lastEvent = now;
        int sign = delta < 0 ? 1 : -1;

        // A full notch is a mouse: a step right away, whatever came before.
        if (Math.Abs(delta) >= NotchDelta)
        {
            _accumulated = 0;
            _lastStep = now;
            return sign;
        }

        if (idle || Math.Sign(_accumulated) == -Math.Sign(delta)) _accumulated = 0;
        _accumulated += delta;
        if (Math.Abs(_accumulated) < NotchDelta) return 0;

        _accumulated = 0;
        // Enough travel but too soon after the last step: it is inertia, dropped rather than saved for later.
        if ((now - _lastStep).TotalMilliseconds < CooldownMs) return 0;
        _lastStep = now;
        return sign;
    }
}
