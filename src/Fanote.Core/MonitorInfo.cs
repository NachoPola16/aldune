namespace Fanote.Core;

/// <summary>
/// One connected monitor's real geometry and DPI. <see cref="DeviceName"/> is whatever Win32
/// hands back today (e.g. "\\.\DISPLAY1") — it is NOT a stable id across reconnects; that's a
/// later sub-delivery of Phase 3 (see docs/STATUS.md).
/// </summary>
public readonly record struct MonitorInfo(string DeviceName, WorkingArea WorkArea, double DpiScale, bool IsPrimary);
