namespace Aldune.Core;

/// <summary>
/// How fast the dock's animations run. The values are stored as numbers in settings.json: <see cref="Normal"/> is 0 so a file written before
/// this setting existed (or a number from a future version) keeps today's behaviour.
/// </summary>
public enum DockAnimationMode
{
    Normal = 0,
    Off = 1,
    Calm = 2,
    Quick = 3,
}

/// <summary>
/// One factor for every animation of the dock. The dock keeps its own base durations; this only says by how much they are scaled and whether they
/// run at all. Hover and wheel timers are behaviour, not looks, and are deliberately not scaled.
/// </summary>
public static class DockMotion
{
    /// <summary>Multiplier of the base durations. Anything that is not a known mode (an out-of-range number) is Normal.</summary>
    public static double Factor(DockAnimationMode mode) => mode switch
    {
        DockAnimationMode.Calm => 1.5,
        DockAnimationMode.Quick => 0.6,
        _ => 1.0,
    };

    /// <summary>
    /// A base duration scaled for <paramref name="mode"/>. Off is 0 (the dock takes its no-animation branches before it gets here). Normal returns
    /// the base untouched, so it is today's number to the bit. A scaled duration is never rounded below 1 ms: a zero-length animation with a
    /// BeginTime reorders the frames of a staggered entrance.
    /// </summary>
    public static double Scale(double baseMs, DockAnimationMode mode)
    {
        if (mode == DockAnimationMode.Off) return 0;
        double factor = Factor(mode);
        if (factor == 1.0 || baseMs <= 0) return baseMs;
        return Math.Max(1.0, baseMs * factor);
    }

    /// <summary>
    /// Whether the dock animates. Windows' own «animation effects» switch always wins: the setting can only turn more off, never force animations
    /// on for someone who disabled them in the system.
    /// </summary>
    public static bool Enabled(DockAnimationMode mode, bool systemAnimations) => systemAnimations && mode != DockAnimationMode.Off;
}
