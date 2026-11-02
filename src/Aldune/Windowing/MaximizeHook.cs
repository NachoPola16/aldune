using System.Windows;
using System.Windows.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Quita los topes de tamaño (<c>MaxWidth</c>/<c>MaxHeight</c>) justo antes de que Windows maximice la ventana por su cuenta: doble
/// clic en la barra de título, Win+↑ o arrastrarla hasta arriba. El botón de maximizar ya los quitaba, pero el doble clic no pasa por
/// él, y con el tope del 90 % de alto de Ajustes o del gestor la ventana «maximizada» dejaba una franja libre abajo. El tope se
/// vuelve a poner al salir del estado maximizado (lo hace cada ventana en su <c>StateChanged</c>).
/// </summary>
internal static class MaximizeHook
{
    /// <summary>Room, in DIPs, that a cap of «the work area» needs on top of it: a snapped window carries invisible resize borders (about 7 px a side) that count in its size.</summary>
    internal const double SnapBorderSlack = 16;

    /// <summary>
    /// The size a window may reach: the largest work area of any screen, plus <see cref="SnapBorderSlack"/>. Measured on the screen the window
    /// started on, the cap was wrong as soon as it was snapped on another one (a taller portrait screen was cut at the height of the main one).
    /// </summary>
    internal static (double Width, double Height) SnapCap()
    {
        double w = SystemParameters.WorkArea.Width, h = SystemParameters.WorkArea.Height;
        foreach (var monitor in Aldune.Interop.MonitorEnumerator.EnumerateMonitors())
        {
            w = Math.Max(w, monitor.WorkArea.Width);
            h = Math.Max(h, monitor.WorkArea.Height);
        }
        return (w + SnapBorderSlack, h + SnapBorderSlack);
    }

    private const int WmSysCommand = 0x0112;
    private const int ScMaximize = 0xF030;

    internal static void Install(Window window)
    {
        void Attach()
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == WmSysCommand && (wParam.ToInt64() & 0xFFF0) == ScMaximize)
                {
                    window.MaxHeight = double.PositiveInfinity;
                    window.MaxWidth = double.PositiveInfinity;
                }
                return IntPtr.Zero;
            });
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Attach();
        else window.SourceInitialized += (_, _) => Attach();
    }
}
