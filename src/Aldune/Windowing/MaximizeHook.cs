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
