namespace Aldune.Core;

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

    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        // Windows 11: vista de tareas, Alt+Tab y ajuste de ventanas pasan un instante por una ventana de
        // explorer que cubre el monitor. No es una aplicación a pantalla completa: el dock no debe apartarse.
        "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "ForegroundStaging", "TaskSwitcherWnd",
    };

    /// <summary>Si la clase de ventana es del propio shell de Windows (escritorio, barra de tareas, vista de
    /// tareas…) y por tanto nunca cuenta como "algo a pantalla completa".</summary>
    public static bool IsShellWindowClass(string className) => ShellClasses.Contains(className);
}
