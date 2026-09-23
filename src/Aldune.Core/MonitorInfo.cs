namespace Aldune.Core;

/// <summary>
/// One connected monitor's real geometry and DPI. <see cref="DeviceName"/> is whatever Win32
/// hands back today (e.g. "\\.\DISPLAY1") — it is NOT a stable id across reconnects.
/// <see cref="StableId"/> is: the monitor's device interface path (EnumDisplayDevices), the same for
/// the same physical screen on the same port after it is turned off and on again. Null when Windows
/// doesn't give one (and in tests that don't care).
/// </summary>
public readonly record struct MonitorInfo(
    string DeviceName, WorkingArea WorkArea, double DpiScale, bool IsPrimary, string? StableId = null);
