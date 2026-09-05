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
    /// La comparación tiene que hacerse contra el rectángulo <b>completo</b> del monitor, no
    /// contra su área de trabajo: así una ventana maximizada —que deja la barra de tareas a la
    /// vista— no cuenta como pantalla completa. Es la distinción que importa; esconder el dock
    /// cada vez que alguien maximiza una ventana sería insufrible.
    ///
    /// Se usa &gt;= / &lt;= y no igualdad porque hay aplicaciones a pantalla completa que se
    /// declaran un pelín más grandes que el monitor.
    /// </summary>
    public static bool CoversMonitor(Rect window, Rect monitor)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0) return false;

        return window.X <= monitor.X
            && window.Y <= monitor.Y
            && window.X + window.Width >= monitor.X + monitor.Width
            && window.Y + window.Height >= monitor.Y + monitor.Height;
    }
}
