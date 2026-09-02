using System.Runtime.InteropServices;
using Fanote.Core;

namespace Fanote.Interop;

/// <summary>
/// Real per-monitor bounds and DPI via Win32 — SystemParameters.WorkArea (WPF) only ever returns
/// the primary monitor's work area, which is the gap this fills. Called once at startup
/// (App.xaml.cs); no live hotplug handling (see Phase 3a spec, "Fuera de alcance").
/// </summary>
internal static class MonitorEnumerator
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class MONITORINFOEX
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice = string.Empty;
    }

    private const int MONITORINFOF_PRIMARY = 0x00000001;

    private enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0,
    }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

    internal static IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        var results = new List<MonitorInfo>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var info = new MONITORINFOEX();
            if (!GetMonitorInfo(hMonitor, info))
                return true; // couldn't read this one — skip it, keep enumerating the rest

            // GetDpiForMonitor needs Windows 8.1+ (shcore.dll); if it fails for any reason,
            // assume 96 DPI (100% scale) for that monitor rather than throwing.
            double dpiScale = 1.0;
            if (GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
            {
                dpiScale = dpiX / 96.0;
            }

            var pixelWorkArea = new Rect(
                info.rcWork.Left,
                info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left,
                info.rcWork.Bottom - info.rcWork.Top);

            results.Add(new MonitorInfo(
                info.szDevice,
                DpiConversion.ToWorkingArea(pixelWorkArea, dpiScale),
                dpiScale,
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0));

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return results;
    }
}
