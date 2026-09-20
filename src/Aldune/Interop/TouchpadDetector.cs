using Microsoft.Win32;

namespace Aldune.Interop;

/// <summary>
/// ¿Tiene este equipo un trackpad de precisión (el que habla con Windows y manda gestos de dos
/// dedos)? Se mira por el registro y es <b>best-effort</b>: no hay API pública para preguntarlo.
///
/// Windows crea <c>HKCU\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad</c> cuando hay un
/// trackpad de precisión en uso, así que su presencia es la señal más fiable y barata que existe. Si
/// no está, se responde que no: un trackpad antiguo (el que se anuncia como ratón PS/2) no envía
/// gestos y no hay nada que activar para él. Equivocarse por el lado de "no lo he detectado" solo
/// cuesta un ajuste que no se puede marcar; equivocarse por el otro dejaría un ajuste encendido que
/// no hace nada.
/// </summary>
internal static class TouchpadDetector
{
    private const string PrecisionTouchpadKey =
        @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";

    internal static bool HasPrecisionTouchpad()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PrecisionTouchpadKey);
            return key is not null;
        }
        catch
        {
            // Un registro ilegible no es motivo para tumbar Ajustes: se responde que no.
            return false;
        }
    }
}
