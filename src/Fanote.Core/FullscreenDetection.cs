namespace Fanote.Core;

/// <summary>
/// Decide si una ventana está tapando un monitor entero. El dock lo usa para apartarse cuando hay
/// algo a pantalla completa delante (un juego, un vídeo): es <c>Topmost</c>, así que si no se
/// aparta se queda dibujado encima.
/// </summary>
public static class FullscreenDetection
{
    /// <summary>
    /// Si <paramref name="window"/> cubre <paramref name="monitor"/> por completo.
    ///
    /// Si <paramref name="isZoomed"/> es verdadero, la ventana está maximizada por el sistema
    /// operativo (p. ej. un navegador con pestañas en una pantalla donde la barra de tareas se
    /// oculta o no resta área de trabajo): en ese caso NO cuenta como pantalla completa, ya que
    /// esconder el dock al interactuar con ventanas maximizadas sería indeseable.
    ///
    /// Se usa &gt;= / &lt;= y no igualdad porque hay aplicaciones a pantalla completa que se
    /// declaran un pelín más grandes que el monitor.
    /// </summary>
    public static bool CoversMonitor(Rect window, Rect monitor, bool isZoomed = false)
    {
        if (isZoomed) return false;
        if (monitor.Width <= 0 || monitor.Height <= 0) return false;

        return window.X <= monitor.X
            && window.Y <= monitor.Y
            && window.X + window.Width >= monitor.X + monitor.Width
            && window.Y + window.Height >= monitor.Y + monitor.Height;
    }
}
