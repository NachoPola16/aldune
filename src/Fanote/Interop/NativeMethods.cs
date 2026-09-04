using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Fanote.Core;

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
    /// The standard system drop shadow, via the well-known "extend glass into the whole client
    /// area" trick — DWM draws its normal window shadow around the window's full rectangular
    /// bounds this way, without needing real glass transparency (which would break ClearType).
    /// Only correct for a plain rectangular window: DWM's shadow (and this "glass extended"
    /// treatment) tracks the window's outer RECT, not any custom SetWindowRgn shape, so applying
    /// this while a shaped region is active makes DWM paint a translucent box across the
    /// *unclipped* rectangle — confirmed on a real run (phone recording): exactly the dark
    /// gutter/notches SetWindowRgn was meant to remove reappeared as a washed-out grey box, and
    /// a jagged "L" shape during the resize animation, once the region and this shadow were both
    /// live at once. Callers that shape their window (EdgeDockWindow's expanded panel) must pair
    /// this with ClearShadow whenever the shape is active, and restore it once the shape is gone.
    /// </summary>
    internal static void ApplyShadow(IntPtr hWnd)
    {
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
    }

    /// <summary>
    /// Undoes ApplyShadow — back to a plain window with no extended frame/shadow. Needed while a
    /// custom SetWindowRgn shape is active (see ApplyShadow's remarks).
    /// </summary>
    internal static void ClearShadow(IntPtr hWnd)
    {
        var margins = new MARGINS { Left = 0, Right = 0, Top = 0, Bottom = 0 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
    }

    /// <summary>
    /// Rounded corners plus the standard system drop shadow, for a borderless window that
    /// (unlike NoteWindow) doesn't already get the shadow via WindowChrome's own
    /// GlassFrameThickness="-1". Only valid while the window is a plain rectangle — see
    /// ApplyShadow's remarks for why a shaped window (EdgeDockWindow, expanded) must not call
    /// this while its custom region is active.
    /// </summary>
    internal static void ApplyRoundedCornersAndShadow(IntPtr hWnd)
    {
        ApplyRoundedCorners(hWnd);
        ApplyShadow(hWnd);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// Physical-pixel screen position of the cursor, polled fresh via Win32 rather than read from
    /// WPF's <c>Mouse.GetPosition</c> — that API reflects the last mouse message a given window
    /// received, so once the cursor genuinely leaves a window (and it stops receiving any),
    /// <c>Mouse.GetPosition</c> keeps reporting the last (inside) position forever instead of
    /// updating. Callers must divide by their own window's DPI scale to convert to DIPs.
    /// </summary>
    internal static Point GetCursorScreenPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int cornerWidth, int cornerHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int combineMode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    private const int RGN_OR = 2;

    /// <summary>
    /// Recorta la forma visible de <paramref name="hWnd"/> a la unión de las piezas dadas
    /// (coordenadas en píxeles físicos, relativas a la esquina superior izquierda de la ventana).
    /// CreateRoundRectRgn redondea las 4 esquinas por igual, así que una pieza con radio > 0 se
    /// construye como la unión de un RoundRect completo con un Rect plano que cubre su mitad
    /// derecha — eso "cuadra" las dos esquinas de la derecha encima, dejando solo las de la
    /// izquierda redondeadas (el lado libre de cada pestaña, lejos del borde físico de pantalla;
    /// ver EdgeDockWindow y la spec de este recorte). El HRGN final que llega a SetWindowRgn pasa a
    /// ser propiedad del sistema (se libera solo, al reemplazarlo o cerrar la ventana) — cualquier
    /// HRGN intermedio que no llegue ahí se libera aquí mismo con DeleteObject.
    /// </summary>
    internal static void SetTabFanRegion(IntPtr hWnd, IReadOnlyList<RegionPiece> pieces)
    {
        IntPtr accumulated = CreateRectRgn(0, 0, 0, 0);
        foreach (var (bounds, cornerRadius) in pieces)
        {
            int left = (int)bounds.X;
            int top = (int)bounds.Y;
            int right = (int)(bounds.X + bounds.Width);
            int bottom = (int)(bounds.Y + bounds.Height);

            IntPtr piece;
            if (cornerRadius > 0)
            {
                int diameter = (int)(cornerRadius * 2);
                IntPtr rounded = CreateRoundRectRgn(left, top, right, bottom, diameter, diameter);
                IntPtr rightHalfSquared = CreateRectRgn(left + (right - left) / 2, top, right, bottom);
                piece = CreateRectRgn(0, 0, 0, 0);
                CombineRgn(piece, rounded, rightHalfSquared, RGN_OR);
                DeleteObject(rounded);
                DeleteObject(rightHalfSquared);
            }
            else
            {
                piece = CreateRectRgn(left, top, right, bottom);
            }

            CombineRgn(accumulated, accumulated, piece, RGN_OR);
            DeleteObject(piece);
        }

        SetWindowRgn(hWnd, accumulated, true);
    }

    /// <summary>
    /// Quita cualquier recorte de forma aplicado por SetTabFanRegion, devolviendo la ventana a su
    /// rectángulo completo normal — necesario porque el pill en reposo no usa regiones.
    /// </summary>
    internal static void ClearWindowRegion(IntPtr hWnd) => SetWindowRgn(hWnd, IntPtr.Zero, true);
}
