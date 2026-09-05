namespace Fanote.Core;

/// <summary>
/// Tiempos y curva del despliegue del abanico.
///
/// Antes esto era <c>TabRegionShape</c> y además construía las piezas de la región recortada. Al
/// pasar el dock a una ventana con transparencia, la forma la dibuja WPF y la región desapareció
/// entera — con ella se fueron el <c>SetWindowRgn</c> por frame, el bucle de
/// <c>CompositionTarget.Rendering</c> y el recorte al viewport. Lo único que sobrevive es esto:
/// cuánto tarda cada pestaña y con qué curva, que ahora alimenta los <c>BeginTime</c> de
/// animaciones WPF normales.
/// </summary>
public static class FanTiming
{
    /// <summary>Duración del recorrido de una pestaña concreta.</summary>
    public const double TabSweepMs = 220;

    /// <summary>Desfase entre el arranque de una pestaña y el de la siguiente.</summary>
    public const double TabStaggerMs = 26;

    /// <summary>
    /// Tope del desfase acumulado. Sin él, el escalonado crece sin límite con el número de notas:
    /// un diseño anterior (55ms por índice más 190ms de espera antes de mostrar nada) dejaba la
    /// última pestaña sin asentarse hasta ~760ms con 6 notas. Con este tope, la última arranca como
    /// muy tarde en 130ms y termina en 350ms pase lo que pase.
    /// </summary>
    public const double MaxTotalStaggerMs = 130;

    /// <summary>Retardo de arranque de la pestaña <paramref name="index"/>.</summary>
    public static double StaggerDelayMs(int index) =>
        Math.Min(index * TabStaggerMs, MaxTotalStaggerMs);

    /// <summary>Duración total de una transición con <paramref name="noteCount"/> notas.</summary>
    public static double TotalDurationMs(int noteCount) =>
        StaggerDelayMs(Math.Max(0, noteCount - 1)) + TabSweepMs;

    /// <summary>
    /// Curva ease-out quíntica. Las leyes de diseño piden salidas exponenciales (quart/quint/expo);
    /// la QuadraticEase original es la ease-out más débil que existe y apenas se lee como un
    /// asentamiento, que es por lo que alargar la duración no arreglaba la sensación.
    /// </summary>
    public static double EaseOutQuintic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        double inv = 1 - t;
        return 1 - inv * inv * inv * inv * inv;
    }
}
