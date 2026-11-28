using System.Windows;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// The WPF side of <see cref="DockMotion"/>: reads Windows' «animation effects» switch in one place and combines it with the dock's own setting.
/// </summary>
internal static class DockMotionRuntime
{
    /// <summary>Whether the dock animates now: the system switch first, then the setting.</summary>
    internal static bool On(AppSettings? settings) =>
        DockMotion.Enabled(settings?.DockAnimation ?? DockAnimationMode.Normal, SystemParameters.ClientAreaAnimation);

    /// <summary>A base duration of the dock, scaled for the setting.</summary>
    internal static double Ms(AppSettings? settings, double baseMs) =>
        DockMotion.Scale(baseMs, settings?.DockAnimation ?? DockAnimationMode.Normal);
}
