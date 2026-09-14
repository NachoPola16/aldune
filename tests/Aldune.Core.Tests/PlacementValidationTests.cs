using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class PlacementValidationTests
{
    private static readonly MonitorInfo Primary = new(
        "\\\\.\\DISPLAY1", new WorkingArea(0, 0, 1920, 1080), DpiScale: 1.0, IsPrimary: true);

    private static readonly MonitorInfo Secondary = new(
        "\\\\.\\DISPLAY2", new WorkingArea(1920, 0, 1080, 1920), DpiScale: 1.0, IsPrimary: false);

    [Fact]
    public void IsVisibleOnMonitors_FullyInsideAMonitor_IsVisible()
    {
        Assert.True(PlacementValidation.IsVisibleOnMonitors(
            100, 100, 300, 320, new[] { Primary, Secondary }));
    }

    [Fact]
    public void IsVisibleOnMonitors_CompletelyOffAllMonitors_IsNotVisible()
    {
        Assert.False(PlacementValidation.IsVisibleOnMonitors(
            -5000, -5000, 300, 320, new[] { Primary, Secondary }));
    }

    [Fact]
    public void IsVisibleOnMonitors_OnlyASliverOverlapping_IsNotVisible()
    {
        // Menos de 50x50 asomando por el borde izquierdo del monitor principal: no cuenta como
        // "sigue estando a la vista" para restaurar una posición guardada.
        Assert.False(PlacementValidation.IsVisibleOnMonitors(
            -290, 100, 300, 320, new[] { Primary, Secondary }));
    }

    [Fact]
    public void IsVisibleOnMonitors_EnoughOverlapOnASecondMonitor_IsVisible()
    {
        // El monitor desconectado (donde estaba antes) ya no está en la lista, pero sigue
        // solapando lo suficiente con el que queda.
        Assert.True(PlacementValidation.IsVisibleOnMonitors(
            1800, 100, 300, 320, new[] { Secondary }));
    }

    [Fact]
    public void IsVisibleOnMonitors_NoMonitorsConnected_IsNotVisible()
    {
        Assert.False(PlacementValidation.IsVisibleOnMonitors(
            100, 100, 300, 320, Array.Empty<MonitorInfo>()));
    }
}
