using Aldune.Core;

namespace Aldune.Interop;

/// <summary>
/// Una sola Aldune por sesión de Windows (el mutex sin prefijo vive en la sesión: con el mismo usuario
/// en dos sesiones de escritorio remoto habría dos). Sin esto, arrancar con Windows y luego abrirla desde el
/// acceso directo dejaba dos instancias: dos docks, el atajo global registrado dos veces y dos
/// autoguardados escribiendo sobre las mismas notas.
///
/// La segunda instancia no se queda callada: avisa a la primera con un evento con nombre, y la
/// primera responde abriendo el gestor de notas, para que quien la abrió vea que ya estaba en marcha.
/// El mutex usa el mismo nombre que <c>AppMutex</c> en el instalador, que así pide cerrar Aldune antes
/// de actualizar o desinstalar.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>La instancia, si es la primera; null si ya había otra (a la que se ha avisado).</summary>
    internal static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, BrandIdentity.SingleInstanceMutexName);
        bool owned;
        try
        {
            owned = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // La instancia anterior se cerró de golpe sin soltarlo: el mutex pasa a ser nuestro.
            owned = true;
        }

        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, BrandIdentity.SingleInstanceActivateEventName);
        if (owned) return new SingleInstance(mutex, activate);

        // Deja que la primera se ponga delante: sin permiso, Windows no le deja traer el gestor al
        // frente desde segundo plano y se quedaba parpadeando en la barra de tareas.
        AllowSetForegroundWindow(AnyProcess);
        activate.Set();
        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Llama a <paramref name="onActivate"/> (en un hilo del pool) cada vez que otra
    /// instancia intenta arrancar.</summary>
    internal void ListenForActivation(Action onActivate)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    private const int AnyProcess = -1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        _mutex.Dispose();
    }
}
