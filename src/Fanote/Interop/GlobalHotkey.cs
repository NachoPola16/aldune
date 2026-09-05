using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Fanote.Interop;

/// <summary>
/// Atajo de teclado global: crear una nota sin tocar el ratón, esté donde esté el foco.
///
/// Es lo que separa a Fanote de tener que ir al borde de la pantalla, esperar el abanico y pulsar
/// "+". Una nota que cuesta tres gestos se escribe en otro sitio.
///
/// Se registra contra una ventana <b>solo de mensajes</b> (HWND_MESSAGE) propia, no contra un dock:
/// los docks se destruyen y se vuelven a crear cada vez que cambia la configuración de pantallas
/// (ver App.OnDisplaySettingsChanged), y el atajo moriría con ellos sin que nadie se enterara.
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 1;

    // MOD_NOREPEAT: sin esto, mantener pulsada la combinación dispara sin parar y crea una nota por
    // repetición de teclado.
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint VK_N = 0x4E;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _registered;

    /// <summary>Cómo se llama el atajo de cara al usuario. Un único sitio, para que la interfaz no
    /// pueda contar una combinación distinta de la que de verdad está registrada.</summary>
    internal const string DisplayName = "Ctrl + Alt + N";

    /// <summary>Si el atajo está activo ahora mismo. Falso también cuando otra aplicación ya tenía
    /// esa combinación: Windows no la comparte, y el primero que la pide se la queda.</summary>
    internal bool IsRegistered => _registered;

    internal GlobalHotkey(Action onPressed)
    {
        _onPressed = onPressed;

        var parameters = new HwndSourceParameters("FanoteHotkey")
        {
            // -3 es HWND_MESSAGE: una ventana que solo existe para recibir mensajes, sin sitio en
            // pantalla, sin barra de tareas y sin aparecer en Alt+Tab.
            ParentWindow = new IntPtr(-3)
        };
        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);
    }

    /// <summary>Registra el atajo. Devuelve si lo consiguió.</summary>
    internal bool Enable()
    {
        if (_registered) return true;
        _registered = RegisterHotKey(_source.Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_N);
        return _registered;
    }

    internal void Disable()
    {
        if (!_registered) return;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _onPressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Disable();
        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }
}
