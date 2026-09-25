using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Aldune.Core;

namespace Aldune.Interop;

/// <summary>
/// Registro de diagnóstico del dock y de las pantallas (<c>logs\dock.log</c> junto a los datos).
/// Existe para dos fallos que no se reproducen a voluntad: la tira de reposo que a veces deja de
/// verse y vuelve al pasar el ratón, y el dock que no vuelve a la pantalla principal al encenderla.
/// Ver docs/DOCK_DIAGNOSTICS.md para qué se apunta y cómo leerlo.
///
/// Sin <see cref="Log"/> (las sondas y los tests no lo configuran) no hace nada.
/// </summary>
internal static class DockDiagnostics
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint GA_ROOT = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    internal static DiagnosticLog? Log { get; set; }

    internal static bool Enabled => Log is not null;

    internal static void Write(string category, string message) => Log?.Write(category, message);

    internal static IntPtr Foreground => GetForegroundWindow();

    /// <summary>El color que se ve de verdad en pantalla en ese punto (ya compuesto por DWM).</summary>
    internal static (byte R, byte G, byte B) ScreenPixel(int x, int y)
    {
        var dc = GetDC(IntPtr.Zero);
        try
        {
            uint color = GetPixel(dc, x, y);
            return ((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    /// <summary>Dónde está la ventana de verdad, en píxeles físicos.</summary>
    internal static (int X, int Y, int Width, int Height) WindowRect(IntPtr hwnd) =>
        GetWindowRect(hwnd, out var r) ? (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top) : (0, 0, 0, 0);

    internal static bool IsTopmost(IntPtr hwnd) => (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;

    /// <summary>La ventana de nivel superior que Windows pondría bajo ese punto (píxeles físicos).</summary>
    internal static IntPtr RootWindowAt(int x, int y)
    {
        var hwnd = WindowFromPoint(new POINT { X = x, Y = y });
        return hwnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwnd, GA_ROOT);
    }

    /// <summary>Clase y proceso de una ventana: lo justo para saber qué era, sin su título (puede
    /// llevar datos del usuario, como el nombre de un documento).</summary>
    internal static string Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "ninguna";

        var className = new StringBuilder(128);
        GetClassName(hwnd, className, className.Capacity);
        GetWindowThreadProcessId(hwnd, out uint processId);
        string process;
        try
        {
            using var p = Process.GetProcessById((int)processId);
            process = p.ProcessName;
        }
        catch (ArgumentException) { process = "?"; }
        catch (InvalidOperationException) { process = "?"; }
        return $"'{className}' de {process} (pid {processId})";
    }

    internal static string Describe(IEnumerable<MonitorInfo> monitors) =>
        string.Join(" | ", monitors.Select(monitor =>
            $"{monitor.DeviceName} id={monitor.StableId ?? "-"}{(monitor.IsPrimary ? " principal" : "")} " +
            $"{monitor.WorkArea.Width:0}x{monitor.WorkArea.Height:0}@{monitor.WorkArea.X:0},{monitor.WorkArea.Y:0} " +
            $"escala={monitor.DpiScale:0.##}"));
}
