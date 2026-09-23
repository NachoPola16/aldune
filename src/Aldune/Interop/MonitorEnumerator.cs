using System.Runtime.InteropServices;
using Aldune.Core;

namespace Aldune.Interop;

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    private const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x1;
    private const int DISPLAY_DEVICE_ACTIVE = 0x1;

    /// <summary>
    /// El identificador estable de la pantalla conectada a la salida <paramref name="adapterDeviceName"/>
    /// ("\\.\DISPLAY1"): la ruta de interfaz del monitor, que no cambia al apagarlo y encenderlo,
    /// a diferencia del propio "DISPLAYn". Null si Windows no la da.
    /// </summary>
    private static string? StableIdFor(string adapterDeviceName)
    {
        var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(adapterDeviceName, i, ref device, EDD_GET_DEVICE_INTERFACE_NAME); i++)
        {
            if ((device.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0 && !string.IsNullOrEmpty(device.DeviceID))
                return device.DeviceID;
            device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        }
        return null;
    }

    /// <summary>
    /// El monitor sobre el que está el cursor, o <c>null</c> si no cae en ninguno de los conectados.
    ///
    /// Existe para colocar avisos: <c>SystemParameters.WorkArea</c> (lo que usaba el aviso de
    /// actualizaciones) devuelve SIEMPRE el monitor primario, así que el aviso podía salir en la otra
    /// pantalla. El cursor sirve de origen porque quien dispara un aviso lo hace con un clic — el
    /// botón de ajustes, la bandeja — y en ese momento el cursor está, por definición, en la pantalla
    /// desde la que se pidió.
    ///
    /// La comparación se hace en píxeles físicos, que son las unidades de <c>GetCursorPos</c>: el
    /// área de trabajo se guarda en DIPs (ver <see cref="DpiConversion"/>), así que se deshace la
    /// conversión con la escala de ese mismo monitor.
    /// </summary>
    internal static MonitorInfo? MonitorContainingCursor()
    {
        var cursor = NativeMethods.GetCursorScreenPosition();

        foreach (var monitor in EnumerateMonitors())
        {
            var area = monitor.WorkArea;
            double scale = monitor.DpiScale > 0 ? monitor.DpiScale : 1.0;
            double left = area.X * scale;
            double top = area.Y * scale;

            if (cursor.X >= left && cursor.X < left + area.Width * scale
                && cursor.Y >= top && cursor.Y < top + area.Height * scale)
            {
                return monitor;
            }
        }

        return null;
    }

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
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                StableIdFor(info.szDevice)));

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return results;
    }
}
