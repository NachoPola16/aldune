using System.Runtime.InteropServices;
using System.Windows.Interop;
using Aldune.Core;

namespace Aldune.Interop;

/// <summary>
/// Atajo de teclado global: crear una nota sin tocar el ratón, esté donde esté el foco.
///
/// Es lo que separa a Aldune de tener que ir al borde de la pantalla, esperar el abanico y pulsar
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
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _registered;

    private HotkeyBinding _binding = HotkeyBinding.Default;

    /// <summary>La combinación registrada ahora mismo.</summary>
    internal HotkeyBinding Binding => _binding;

    /// <summary>Si el atajo está activo ahora mismo. Falso también cuando otra aplicación ya tenía
    /// esa combinación: Windows no la comparte, y el primero que la pide se la queda.</summary>
    internal bool IsRegistered => _registered;

    internal GlobalHotkey(Action onPressed)
    {
        _onPressed = onPressed;

        var parameters = new HwndSourceParameters(BrandIdentity.WindowMessageClassName)
        {
            // -3 es HWND_MESSAGE: una ventana que solo existe para recibir mensajes, sin sitio en
            // pantalla, sin barra de tareas y sin aparecer en Alt+Tab.
            ParentWindow = new IntPtr(-3)
        };
        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);
    }

    /// <summary>
    /// Registra <paramref name="binding"/>. Devuelve si lo consiguió.
    ///
    /// Siempre da de baja lo anterior primero: cambiar de combinación sin soltar la vieja dejaría
    /// las dos activas, y la antigua seguiría creando notas hasta cerrar la app.
    /// </summary>
    internal bool Enable(HotkeyBinding binding)
    {
        Disable();
        if (!binding.IsValid) return false;

        _binding = binding;
        _registered = RegisterHotKey(
            _source.Handle, HotkeyId, binding.Modifiers | MOD_NOREPEAT, binding.Key);
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
