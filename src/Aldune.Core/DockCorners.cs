namespace Aldune.Core;

/// <summary>
/// The shape of the dock's corners, independent of the app's «square corners». Stored as a number in settings.json; <see cref="FollowLook"/> is 0
/// so a file written before this setting existed keeps today's dock (square or rounded as the look says).
/// </summary>
public enum DockCornerStyle
{
    FollowLook = 0,
    Square = 1,
    Soft = 2,
    Round = 3,
}

/// <summary>
/// How much the radii the app already uses are multiplied for the dock: 0 is square, 1 is the app's rounded look as it is drawn today, 2 is
/// pill-like (the tab's end nearly a half circle). Pure, so the rule is testable without WPF.
/// </summary>
public static class DockCorners
{
    /// <summary>The multiplier for <paramref name="style"/>. Anything that is not a known style (an out-of-range number) follows the look.</summary>
    public static double Scale(DockCornerStyle style, bool lookSquare) => style switch
    {
        DockCornerStyle.Square => 0.0,
        DockCornerStyle.Soft => 1.0,
        DockCornerStyle.Round => 2.0,
        _ => lookSquare ? 0.0 : 1.0,
    };

    public static bool IsSquare(DockCornerStyle style, bool lookSquare) => Scale(style, lookSquare) == 0.0;
}
