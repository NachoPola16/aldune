namespace Aldune.Core;

/// <summary>
/// Win32 gives monitor bounds in physical pixels; WPF's Window.Left/Top/Width/Height are
/// interpreted in DIPs relative to that specific monitor's own DPI scale once the app declares
/// PerMonitorV2 awareness (see app.manifest). This is the pixel-to-DIP conversion — kept as pure
/// math in Fanote.Core so it's unit-testable without any Win32/WPF dependency.
/// </summary>
public static class DpiConversion
{
    public static WorkingArea ToWorkingArea(Rect pixelBounds, double dpiScale) => new(
        pixelBounds.X / dpiScale,
        pixelBounds.Y / dpiScale,
        pixelBounds.Width / dpiScale,
        pixelBounds.Height / dpiScale);
}
