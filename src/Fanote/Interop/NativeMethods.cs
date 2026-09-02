using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Fanote.Interop;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    internal static void MakeNonActivating(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUNDSMALL = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    /// <summary>
    /// Rounds the window's corners using the same DWM composition Windows already applies to
    /// normal (non-borderless) windows — works on an opaque window, so it doesn't need
    /// AllowsTransparency (which the design deliberately avoids: it would disable ClearType
    /// text rendering). No-ops harmlessly on Windows versions that don't support
    /// DWMWA_WINDOW_CORNER_PREFERENCE (pre-Windows 11).
    /// </summary>
    internal static void ApplyRoundedCorners(IntPtr hWnd)
    {
        int preference = DWMWCP_ROUNDSMALL;
        DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    /// <summary>
    /// Rounded corners plus the standard system drop shadow, for a borderless window that
    /// (unlike NoteWindow) doesn't already get the shadow via WindowChrome's own
    /// GlassFrameThickness="-1".
    /// </summary>
    internal static void ApplyRoundedCornersAndShadow(IntPtr hWnd)
    {
        ApplyRoundedCorners(hWnd);

        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
    }

    internal static void ForceActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();

        var foregroundWindow = GetForegroundWindow();
        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
        var currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(foregroundThreadId, currentThreadId, true);
            try
            {
                SetForegroundWindow(hwnd);
            }
            finally
            {
                AttachThreadInput(foregroundThreadId, currentThreadId, false);
            }
        }
        else
        {
            SetForegroundWindow(hwnd);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);
}
