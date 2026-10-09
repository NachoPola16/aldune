namespace Aldune.Core;

/// <summary>
/// Las cuentas del autoscroll del botón central (ver <c>AutoScrollManager</c>): velocidad según la
/// distancia al origen, distancia recorrida según el tiempo y cuándo soltar el botón apaga el modo.
/// Aparte de la ventana para poder probarlas.
/// </summary>
public static class AutoScrollMath
{
    /// <summary>Zona muerta alrededor del origen, en DIPs: dentro de ella no hay desplazamiento.</summary>
    public const double DeadZone = 10;

    /// <summary>Distancia, pasada la zona muerta, a la que la velocidad llega al tope.</summary>
    public const double FullSpeedDistance = 140;

    /// <summary>Velocidad máxima en DIPs por segundo: ágil pero no instantánea.</summary>
    public const double MaxSpeed = 1600;

    /// <summary>Un tick más largo que esto (la app se quedó parada) se recorta: no hay salto al volver.</summary>
    public static readonly TimeSpan MaxFrame = TimeSpan.FromMilliseconds(50);

    /// <summary>Tiempo con el botón central pulsado a partir del cual soltarlo apaga el modo.</summary>
    public static readonly TimeSpan HoldThreshold = TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// Velocidad con signo (DIPs/s) para un cursor a <paramref name="offset"/> del origen. La curva es
    /// t^1,5, no lineal: cerca del origen se avanza despacio y se puede afinar, y el tope se alcanza igual.
    /// </summary>
    public static double Velocity(double offset)
    {
        double beyond = Math.Abs(offset) - DeadZone;
        if (beyond <= 0) return 0;

        double t = Math.Min(beyond / FullSpeedDistance, 1);
        return Math.Sign(offset) * Math.Pow(t, 1.5) * MaxSpeed;
    }

    /// <summary>Cuánto desplazar a esa velocidad en <paramref name="elapsed"/>: según el reloj y no según los
    /// ticks, que en un <c>DispatcherTimer</c> no llegan a intervalos regulares.</summary>
    public static double Distance(double velocity, TimeSpan elapsed) =>
        velocity * (elapsed > MaxFrame ? MaxFrame : elapsed).TotalSeconds;

    /// <summary>Si al soltar el botón central hay que apagar el modo: solo tras mantenerlo (mantener, arrastrar
    /// y soltar); un clic corto lo deja encendido.</summary>
    public static bool StopsOnRelease(TimeSpan held) => held >= HoldThreshold;
}
