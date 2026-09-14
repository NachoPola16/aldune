using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Aldune.Core;

namespace Aldune.Interop;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_MAXIMIZE = 0x01000000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hMod, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    internal static void MakeNonActivating(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
        EnsureTopmost(hWnd);
    }

    /// <summary>
    /// Permite activar el dock después de una interacción explícita del usuario. Mientras solo se
    /// muestra al pasar el ratón sigue siendo no activable y no roba el foco a la aplicación que se
    /// está usando; al pulsar una flecha, en cambio, el dock necesita recibir las siguientes teclas.
    /// </summary>
    internal static void AllowActivation(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE);
    }

    internal static IntPtr InstallKeyboardHook(LowLevelKeyboardProc callback) =>
        SetWindowsHookEx(WH_KEYBOARD_LL, callback, GetModuleHandle(null), 0);

    internal static void UninstallKeyboardHook(IntPtr hook)
    {
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
    }

    internal static IntPtr ContinueKeyboardHook(IntPtr hook, int code, IntPtr wParam, IntPtr lParam) =>
        CallNextHookEx(hook, code, wParam, lParam);

    internal static int GetKeyboardVirtualKey(IntPtr hookData) => Marshal.ReadInt32(hookData);

    /// <summary>
    /// Reafirma que la ventana este en la capa superior (HWND_TOPMOST) de Windows sin activar
    /// el foco ni robar el primer plano (SWP_NOACTIVATE).
    /// </summary>
    internal static void EnsureTopmost(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
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

    internal static bool IsCursorOverWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !GetCursorPos(out var point) || !GetWindowRect(hWnd, out var rect)) return false;

        return point.X >= rect.Left && point.X < rect.Right
            && point.Y >= rect.Top && point.Y < rect.Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>
    /// El HMONITOR donde está el cursor ahora mismo. Lo usa <c>AppCoordinator</c> para decidir en
    /// qué pantalla abrir una ventana que no tiene "su" monitor propio (Ajustes, el gestor de notas
    /// o una nota nueva por atajo global, todos ellos alcanzables desde la bandeja) — comparado con
    /// <see cref="MonitorFromHwnd"/> de cada dock para encontrar cuál vive en esa misma pantalla.
    /// </summary>
    internal static IntPtr MonitorFromCursor()
    {
        GetCursorPos(out var point);
        return MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
    }

    internal static IntPtr MonitorFromHwnd(IntPtr hWnd) => MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hWnd);

    /// <summary>
    /// Si en el monitor donde vive <paramref name="hWnd"/> hay ahora mismo una ventana de otro
    /// proceso a pantalla completa (un juego, un vídeo). El dock lo consulta para apartarse: es
    /// <c>Topmost</c>, así que si no se aparta se queda dibujado encima.
    ///
    /// Exclusiones necesarias:
    /// <list type="bullet">
    /// <item>Ventanas del propio proceso — una nota nunca debe esconder su propio dock.</item>
    /// <item>El escritorio y la barra de tareas (<c>Progman</c>, <c>WorkerW</c>,
    /// <c>Shell_TrayWnd</c>): tapan el monitor entero pero no son aplicaciones, y son justo lo que
    /// <c>GetForegroundWindow</c> devuelve cuando no hay nada en primer plano — sin excluirlas el
    /// dock estaría escondido casi siempre.</item>
    /// <item>Ventanas de otro monitor, comparando el <c>HMONITOR</c> en vez de coordenadas: cada
    /// dock solo se aparta por lo que pasa en su propia pantalla.</item>
    /// <item>Ventanas maximizadas estándar (<c>IsZoomed</c> o con <c>WS_CAPTION</c>): aunque en pantallas
    /// con barra de tareas auto-oculta o multimonitor cubran todo el monitor, son ventanas normales
    /// (navegador con pestañas, explorador, etc.) y NO videojuegos o vídeos a pantalla completa.</item>
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

        // Si la ventana está maximizada por el SO o tiene barra de título (WS_CAPTION), es una ventana
        // de aplicación con pestañas / controles normales (p. ej. Chrome maximizado), no un videojuego
        // en pantalla completa exclusiva o borderless.
        if (IsZoomed(foreground)) return false;

        int style = GetWindowLong(foreground, GWL_STYLE);
        if ((style & WS_MAXIMIZE) != 0 || (style & WS_CAPTION) == WS_CAPTION)
        {
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

    private static Aldune.Core.Rect ToRect(RECT r) =>
        new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private const int ProcessPowerThrottling = 4; // PROCESS_INFORMATION_CLASS
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint processInformationSize);

    /// <summary>
    /// Desactiva el "power throttling" (EcoQoS) que Windows aplica a procesos en segundo plano
    /// tras un rato de inactividad, para reducir su consumo de CPU. Fanote vive casi siempre en
    /// segundo plano (nadie lo tiene "en primer plano" para escribir salvo cuando abre una nota),
    /// así que Windows lo trata como candidato a ese throttling — y el primer frame de una
    /// animación justo después de que se reactive el proceso sale con tirones, porque el hilo de
    /// UI arranca con el reloj/prioridad todavía reducidos. Esto reproduce el patrón reportado:
    /// "la animación se ve peor cuando llevas un rato sin abrir ninguna nota".
    ///
    /// Llamar una vez al arrancar la app basta: el ajuste es por proceso y persiste mientras viva.
    /// Sin efecto secundario conocido — es la mitigación estándar de Microsoft para apps
    /// interactivas que pasan la mayor parte del tiempo sin foco (reproductores, notificadores).
    /// </summary>
    internal static void DisablePowerThrottling()
    {
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = 0, // 0 = no aplicar throttling de velocidad de ejecucion a este proceso
        };
        SetProcessInformation(
            GetCurrentProcess(), ProcessPowerThrottling, ref state,
            (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
    }
}
