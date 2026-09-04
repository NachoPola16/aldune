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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

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

    private const int VK_LBUTTON = 0x01;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>
    /// Whether the left mouse button is held down right now, anywhere on screen — checked so the
    /// dock's hover polling can ignore the cursor merely passing over it while the user is
    /// dragging something else that happens to share the same screen edge (e.g. a browser's
    /// vertical scrollbar). The high-order bit of GetAsyncKeyState's result is set while the key
    /// is currently pressed, regardless of which window has focus or receives mouse messages —
    /// same reason this file already prefers polling Win32 state directly over WPF's own
    /// Mouse/Keyboard APIs elsewhere (see GetCursorScreenPosition).
    /// </summary>
    internal static bool IsLeftButtonDown() => (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    /// <summary>
    /// Si en el monitor donde vive <paramref name="hWnd"/> hay ahora mismo una ventana de otro
    /// proceso a pantalla completa (un juego, un vídeo). El dock lo consulta para apartarse: es
    /// <c>Topmost</c>, así que si no se aparta se queda dibujado encima.
    ///
    /// Tres exclusiones, todas necesarias:
    /// <list type="bullet">
    /// <item>Ventanas del propio proceso — una nota nunca debe esconder su propio dock.</item>
    /// <item>El escritorio y la barra de tareas (<c>Progman</c>, <c>WorkerW</c>,
    /// <c>Shell_TrayWnd</c>): tapan el monitor entero pero no son aplicaciones, y son justo lo que
    /// <c>GetForegroundWindow</c> devuelve cuando no hay nada en primer plano — sin excluirlas el
    /// dock estaría escondido casi siempre.</item>
    /// <item>Ventanas de otro monitor, comparando el <c>HMONITOR</c> en vez de coordenadas: cada
    /// dock solo se aparta por lo que pasa en su propia pantalla.</item>
    /// </list>
    /// La comparación de rectángulos vive en <see cref="FullscreenDetection.CoversMonitor"/>, que
    /// es donde está la distinción entre pantalla completa y ventana maximizada.
    /// </summary>
    internal static bool IsFullscreenAppCovering(IntPtr hWnd)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == hWnd) return false;

        GetWindowThreadProcessId(foreground, out uint processId);
        if (processId == GetCurrentProcessId()) return false;

        var className = new System.Text.StringBuilder(64);
        GetClassName(foreground, className, className.Capacity);
        switch (className.ToString())
        {
            case "Progman":
            case "WorkerW":
            case "Shell_TrayWnd":
                return false;
        }

        var ourMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        if (ourMonitor == IntPtr.Zero) return false;
        if (MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST) != ourMonitor) return false;

        if (!GetWindowRect(foreground, out var windowRect)) return false;

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(ourMonitor, ref monitorInfo)) return false;

        return FullscreenDetection.CoversMonitor(
            ToRect(windowRect),
            ToRect(monitorInfo.rcMonitor));
    }

    private static Fanote.Core.Rect ToRect(RECT r) =>
        new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

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
    /// <b>No combinar nunca con una sombra DWM.</b> La sombra (y el truco de
    /// DwmExtendFrameIntoClientArea con márgenes negativos que la produce) sigue el RECT exterior
    /// de la ventana, no la región: con las dos activas a la vez, DWM pinta una caja translúcida
    /// justo sobre los huecos que esta región existe para quitar. Confirmado en una ejecución
    /// real (grabación en vídeo): reaparecían como una caja grisácea lavada, más una "L" dentada
    /// durante la animación. Por eso EdgeDockWindow ya no pide ni sombra ni esquinas redondeadas
    /// por DWM — su forma entera la define esta región.
    ///
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
        foreach (var (bounds, cornerRadius, squareRightSide) in pieces)
        {
            int left = (int)bounds.X;
            int top = (int)bounds.Y;
            int right = (int)(bounds.X + bounds.Width);
            int bottom = (int)(bounds.Y + bounds.Height);

            IntPtr piece;
            if (cornerRadius > 0)
            {
                // Acotado al propio tamaño de la pieza: el guión en reposo mide 16x24, y un radio
                // pensado para una pestaña de 104x80 lo deformaría (CreateRoundRectRgn con un
                // diámetro mayor que el lado da una forma degenerada). Acotándolo, el guión sale
                // como una pastilla redondeada y la pestaña con su esquina normal, sin ramas.
                int diameter = (int)Math.Min(cornerRadius * 2, Math.Min(right - left, bottom - top));
                IntPtr rounded = CreateRoundRectRgn(left, top, right, bottom, diameter, diameter);

                if (!squareRightSide)
                {
                    // Pastilla o círculo completo: el contenedor de reposo y los botones del
                    // footer van despegados del canto de la pantalla, así que sus cuatro esquinas
                    // se ven y las cuatro tienen que ir redondeadas.
                    piece = rounded;
                }
                else
                {
                    IntPtr rightHalfSquared = CreateRectRgn(left + (right - left) / 2, top, right, bottom);
                    piece = CreateRectRgn(0, 0, 0, 0);
                    CombineRgn(piece, rounded, rightHalfSquared, RGN_OR);
                    DeleteObject(rounded);
                    DeleteObject(rightHalfSquared);
                }
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
}
