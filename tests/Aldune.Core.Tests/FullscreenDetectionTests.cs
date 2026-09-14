using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class FullscreenDetectionTests
{
    // Monitor vertical del usuario, en pixeles fisicos, con la barra de tareas abajo.
    private static readonly Rect Monitor = new(-1440, -541, 1440, 2560);
    private static readonly Rect WorkArea = new(-1440, -541, 1440, 2512); // 48px de barra

    [Fact]
    public void CoversMonitor_ExactMatch_IsFullscreen()
    {
        Assert.True(FullscreenDetection.CoversMonitor(Monitor, Monitor));
    }

    [Fact]
    public void CoversMonitor_SlightlyLarger_IsStillFullscreen()
    {
        // Hay aplicaciones a pantalla completa que se declaran un pelin mas grandes que el monitor.
        var oversized = new Rect(Monitor.X - 2, Monitor.Y - 2, Monitor.Width + 4, Monitor.Height + 4);
        Assert.True(FullscreenDetection.CoversMonitor(oversized, Monitor));
    }

    [Fact]
    public void CoversMonitor_MaximisedWindow_IsNotFullscreen()
    {
        // La distincion que importa: una ventana maximizada deja la barra de tareas a la vista.
        // Esconder el dock cada vez que alguien maximiza algo seria insufrible.
        Assert.False(FullscreenDetection.CoversMonitor(WorkArea, Monitor));
    }

    [Fact]
    public void CoversMonitor_MaximisedWindowEvenWhenCoveringEntireMonitor_IsNotFullscreen()
    {
        // Caso real del usuario: la barra de tareas esta auto-oculta o en un monitor secundario
        // donde WorkArea == Monitor. La ventana maximizada cubre todo el monitor pero tiene
        // isZoomed = true (WS_MAXIMIZE): NO debe considerarse pantalla completa.
        Assert.False(FullscreenDetection.CoversMonitor(Monitor, Monitor, isZoomed: true));

        var oversized = new Rect(Monitor.X - 8, Monitor.Y - 8, Monitor.Width + 16, Monitor.Height + 16);
        Assert.False(FullscreenDetection.CoversMonitor(oversized, Monitor, isZoomed: true));
    }

    [Fact]
    public void CoversMonitor_TrueFullscreenGameNotZoomed_IsFullscreen()
    {
        // Un videojuego o video F11 a pantalla completa no esta maximizado por el SO (isZoomed = false)
        // y cubre el monitor: SI debe detectarse como pantalla completa.
        Assert.True(FullscreenDetection.CoversMonitor(Monitor, Monitor, isZoomed: false));
    }

    [Fact]
    public void CoversMonitor_WindowOnAnotherMonitor_IsNotFullscreen()
    {
        var primary = new Rect(0, 0, 2560, 1440);
        Assert.False(FullscreenDetection.CoversMonitor(primary, Monitor));
    }

    [Fact]
    public void CoversMonitor_WindowMissingOneEdge_IsNotFullscreen()
    {
        // Cada borde por separado, para que un signo invertido en la comparacion no pase.
        Assert.False(FullscreenDetection.CoversMonitor(
            new Rect(Monitor.X + 1, Monitor.Y, Monitor.Width, Monitor.Height), Monitor));
        Assert.False(FullscreenDetection.CoversMonitor(
            new Rect(Monitor.X, Monitor.Y + 1, Monitor.Width, Monitor.Height), Monitor));
        Assert.False(FullscreenDetection.CoversMonitor(
            new Rect(Monitor.X, Monitor.Y, Monitor.Width - 1, Monitor.Height), Monitor));
        Assert.False(FullscreenDetection.CoversMonitor(
            new Rect(Monitor.X, Monitor.Y, Monitor.Width, Monitor.Height - 1), Monitor));
    }

    [Fact]
    public void CoversMonitor_DegenerateMonitor_IsNotFullscreen()
    {
        // Si GetMonitorInfo fallara y devolviera ceros, no se debe esconder el dock por eso.
        Assert.False(FullscreenDetection.CoversMonitor(Monitor, new Rect(0, 0, 0, 0)));
    }
}
