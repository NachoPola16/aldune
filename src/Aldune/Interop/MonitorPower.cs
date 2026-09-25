using System.Runtime.InteropServices;
using Aldune.Core;

namespace Aldune.Interop;

/// <summary>
/// Pregunta a cada monitor, por DDC/CI, si está encendido (código VCP 0xD6, modo de energía). Solo
/// LEE: escribir 0xD6 apaga pantallas y hay monitores que se han quedado colgados por ello
/// (PowerToys #50449, Twinkle Tray #1213). Cada lectura tarda unos 60 ms y bloquea, así que se llama
/// fuera del hilo de la interfaz (ver <see cref="Windowing.DockScreenWatcher"/>).
/// </summary>
internal static class MonitorPower
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int Size;
        public RECT Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

    [DllImport("dxva2.dll")]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll")]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll")]
    private static extern bool DestroyPhysicalMonitors(uint count, PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll")]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr monitor, byte code, out uint type, out uint current, out uint maximum);

    private const byte VcpPowerMode = 0xD6;

    /// <summary>El estado de cada una de esas pantallas (por <see cref="MonitorInfo.DeviceName"/>).</summary>
    internal static Dictionary<string, MonitorPowerReading> Read(IReadOnlyCollection<string> deviceNames)
    {
        var readings = new Dictionary<string, MonitorPowerReading>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEX { Size = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info) && deviceNames.Contains(info.Device))
                readings[info.Device] = ReadOne(hMonitor);
            return true;
        }, IntPtr.Zero);
        return readings;
    }

    private static MonitorPowerReading ReadOne(IntPtr hMonitor)
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
            return MonitorPowerReading.NoReply;

        var physical = new PHYSICAL_MONITOR[count];
        if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical)) return MonitorPowerReading.NoReply;
        try
        {
            // Con varias pantallas físicas en un mismo HMONITOR (modo clonado) basta la primera.
            return GetVCPFeatureAndVCPFeatureReply(physical[0].Handle, VcpPowerMode, out _, out uint mode, out _)
                ? MonitorPowerTracker.FromVcpPowerMode(mode)
                : MonitorPowerReading.NoReply;
        }
        finally
        {
            DestroyPhysicalMonitors(count, physical);
        }
    }
}
