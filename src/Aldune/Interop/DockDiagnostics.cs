using System.Runtime.InteropServices;

namespace Aldune.Interop;

/// <summary>
/// Lecturas de Win32 que usa el dock para vigilarse: qué ventana hay sobre la tira, si es «topmost», dónde está su ventana y qué color
/// se ve de verdad en pantalla. Ver docs/DOCK_DIAGNOSTICS.md (el registro que las acompañaba se retiró al resolverse los fallos).
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
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

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
}
