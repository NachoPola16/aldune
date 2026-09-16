using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class MonitorLookupTests
{
    private static readonly MonitorInfo Primary = new(
        "\\\\.\\DISPLAY1", new WorkingArea(0, 0, 1920, 1080), DpiScale: 1.0, IsPrimary: true);

    private static readonly MonitorInfo Vertical = new(
        "\\\\.\\DISPLAY2", new WorkingArea(-1080, 0, 1080, 1920), DpiScale: 1.0, IsPrimary: false);

    [Fact]
    public void DeviceNameAt_CenterInsideAMonitor_ReturnsItsDeviceName()
    {
        // Rectangulo 300x200 en (100,100): centro en (250,200), dentro de Primary.
        Assert.Equal(Primary.DeviceName, MonitorLookup.DeviceNameAt(
            100, 100, 300, 200, new[] { Primary, Vertical }));
    }

    [Fact]
    public void DeviceNameAt_CenterInsideTheOtherMonitor_ReturnsThatOne()
    {
        // Rectangulo en el monitor vertical (coordenadas negativas): centro en (-700, 900).
        Assert.Equal(Vertical.DeviceName, MonitorLookup.DeviceNameAt(
            -900, 800, 400, 200, new[] { Primary, Vertical }));
    }

    [Fact]
    public void DeviceNameAt_StraddlingTwoMonitors_UsesTheCenter()
    {
        // Ventana de x=-100 a x=300 (a caballo entre los dos monitores): centro en x=100, dentro
        // de Primary, aunque una parte de la ventana asome por el otro lado.
        Assert.Equal(Primary.DeviceName, MonitorLookup.DeviceNameAt(
            -100, 100, 400, 200, new[] { Primary, Vertical }));
    }

    [Fact]
    public void DeviceNameAt_CenterOffAllMonitors_ReturnsNull()
    {
        Assert.Null(MonitorLookup.DeviceNameAt(
            5000, 5000, 300, 200, new[] { Primary, Vertical }));
    }

    [Fact]
    public void DeviceNameAt_NoMonitorsConnected_ReturnsNull()
    {
        Assert.Null(MonitorLookup.DeviceNameAt(
            100, 100, 300, 200, Array.Empty<MonitorInfo>()));
    }

    // --- MonitorAt (mismo criterio del centro, pero devuelve el monitor entero) -------------------

    [Fact]
    public void MonitorAt_CenterInsideAMonitor_ReturnsIt()
    {
        var result = MonitorLookup.MonitorAt(100, 100, 300, 200, new[] { Primary, Vertical });
        Assert.Equal(Primary, result);
    }

    [Fact]
    public void MonitorAt_CenterInsideTheOtherMonitor_ReturnsThatOne()
    {
        var result = MonitorLookup.MonitorAt(-900, 800, 400, 200, new[] { Primary, Vertical });
        Assert.Equal(Vertical, result);
    }

    [Fact]
    public void MonitorAt_CenterOffAllMonitors_ReturnsNull()
    {
        Assert.Null(MonitorLookup.MonitorAt(5000, 5000, 300, 200, new[] { Primary, Vertical }));
    }

    // --- A que pantalla se manda una disposicion de notas (menu del dock) ---------------------------

    [Fact]
    public void ForDeviceName_KnownDevice_ReturnsIt()
    {
        Assert.Equal(Vertical, MonitorLookup.ForDeviceName(Vertical.DeviceName, new[] { Primary, Vertical }));
    }

    [Fact]
    public void ForDeviceName_UnknownDevice_ReturnsNull()
    {
        Assert.Null(MonitorLookup.ForDeviceName(@"\\.\DISPLAY9", new[] { Primary, Vertical }));
    }

    [Fact]
    public void TargetOrFallback_TargetStillConnected_Wins()
    {
        Assert.Equal(Vertical, MonitorLookup.TargetOrFallback(
            Vertical.DeviceName, Primary.DeviceName, new[] { Primary, Vertical }));
    }

    [Fact]
    public void TargetOrFallback_NoTarget_IsTheDockScreen()
    {
        Assert.Equal(Primary, MonitorLookup.TargetOrFallback(
            null, Primary.DeviceName, new[] { Primary, Vertical }));
    }

    [Fact]
    public void TargetOrFallback_TargetDisconnected_FallsBackToTheDockScreen()
    {
        // La pantalla elegida en el menu se desenchufo entre que se abrio el menu y el clic.
        Assert.Equal(Primary, MonitorLookup.TargetOrFallback(
            @"\\.\DISPLAY9", Primary.DeviceName, new[] { Primary, Vertical }));
    }

    [Fact]
    public void TargetOrFallback_NeitherExists_ReturnsNull()
    {
        Assert.Null(MonitorLookup.TargetOrFallback(
            @"\\.\DISPLAY9", @"\\.\DISPLAY8", new[] { Primary, Vertical }));
    }
}
