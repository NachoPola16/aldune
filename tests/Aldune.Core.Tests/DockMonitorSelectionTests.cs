using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DockMonitorSelectionTests
{
    private static readonly MonitorInfo Main = new("\\\\.\\DISPLAY1", new WorkingArea(0, 0, 2560, 1400), 1, IsPrimary: true, StableId: "MONITOR-MAIN");
    private static readonly MonitorInfo Side = new("\\\\.\\DISPLAY2", new WorkingArea(2560, 0, 1440, 2520), 1, IsPrimary: false, StableId: "MONITOR-SIDE");

    [Fact]
    public void WithoutAChosenScreen_DocksGoOnEveryScreen()
    {
        Assert.Equal(new[] { Main, Side }, DockMonitorSelection.Select([Main, Side], targetMonitorId: null, legacyIndex: null));
    }

    [Fact]
    public void TheChosenScreen_IsFoundByItsIdWhateverTheOrder()
    {
        // Windows no garantiza el orden de la lista: tras apagar y encender una pantalla puede cambiar.
        Assert.Equal(new[] { Main }, DockMonitorSelection.Select([Side, Main], "MONITOR-MAIN", legacyIndex: 0));
    }

    [Fact]
    public void WhileTheChosenScreenIsOff_TheDockGoesToThePrimaryOneForNow()
    {
        // Al apagar la principal, Windows convierte la otra en principal.
        var sideNowPrimary = Side with { IsPrimary = true };

        Assert.Equal(new[] { sideNowPrimary }, DockMonitorSelection.Select([sideNowPrimary], "MONITOR-MAIN", legacyIndex: 0));
    }

    [Fact]
    public void WhenTheChosenScreenComesBack_TheDockReturnsToIt()
    {
        // El caso del bug: al volver la principal, la secundaria seguía siendo la primera de la lista
        // (y a veces la "principal"), y elegir por posición dejaba el dock en ella.
        var sideStillPrimary = Side with { IsPrimary = true };
        var mainBack = Main with { IsPrimary = false };

        Assert.Equal(new[] { mainBack }, DockMonitorSelection.Select([sideStillPrimary, mainBack], "MONITOR-MAIN", legacyIndex: 0));
    }

    [Fact]
    public void WithoutAnyPrimaryFlag_TheFallbackIsTheFirstScreen()
    {
        var a = Side with { IsPrimary = false };

        Assert.Equal(new[] { a }, DockMonitorSelection.Select([a], "MONITOR-MAIN", legacyIndex: null));
    }

    [Fact]
    public void AnOldSettingWithOnlyAnIndex_StillWorks()
    {
        Assert.Equal(new[] { Side }, DockMonitorSelection.Select([Main, Side], targetMonitorId: null, legacyIndex: 1));
        Assert.Equal(new[] { Main, Side }, DockMonitorSelection.Select([Main, Side], targetMonitorId: null, legacyIndex: 5));
    }

    [Fact]
    public void AnOldIndex_IsTranslatedToTheIdOfThatScreen()
    {
        Assert.Equal("MONITOR-SIDE", DockMonitorSelection.IdForLegacyIndex([Main, Side], 1));
        Assert.Null(DockMonitorSelection.IdForLegacyIndex([Main, Side], 7));
        Assert.Null(DockMonitorSelection.IdForLegacyIndex([Main with { StableId = null }], 0));
    }

    [Fact]
    public void NoScreens_NoDocks()
    {
        Assert.Empty(DockMonitorSelection.Select([], "MONITOR-MAIN", legacyIndex: 0));
    }

    // --- Pantallas apagadas con su botón que Windows sigue dando por conectadas -----------------

    [Fact]
    public void AChosenScreenThatReportsItselfOff_CountsAsAbsent()
    {
        // Lo visto en Gigabyte por DDC/CI: apagada con el botón sigue en la lista de Windows, y
        // como principal, pero contesta "apagada".
        Assert.Equal(new[] { Side }, DockMonitorSelection.Select([Main, Side], "MONITOR-MAIN", legacyIndex: null,
            poweredOff: ["MONITOR-MAIN"]));
    }

    [Fact]
    public void TheFallback_SkipsScreensThatAreOffToo()
    {
        var third = new MonitorInfo(@"\\.\DISPLAY3", new WorkingArea(-1920, 0, 1920, 1080), 1, IsPrimary: false, StableId: "MONITOR-THIRD");

        Assert.Equal(new[] { third }, DockMonitorSelection.Select([Main, Side, third], "MONITOR-SIDE", legacyIndex: null,
            poweredOff: ["MONITOR-SIDE", "MONITOR-MAIN"]));
    }

    [Fact]
    public void IfEveryScreenSaysOff_TheChosenOneStays()
    {
        // No hay a dónde ir: mejor quedarse donde el usuario lo quiere que saltar entre pantallas negras.
        Assert.Equal(new[] { Main }, DockMonitorSelection.Select([Main, Side], "MONITOR-MAIN", legacyIndex: null,
            poweredOff: ["MONITOR-MAIN", "MONITOR-SIDE"]));
    }

    [Fact]
    public void AllScreens_ShowsOnlyTheOnesThatAreOn()
    {
        Assert.Equal(new[] { Side }, DockMonitorSelection.Select([Main, Side], targetMonitorId: null, legacyIndex: null,
            poweredOff: ["MONITOR-MAIN"]));
    }

    // --- El dock va a la pantalla del ratón ------------------------------------------------------

    [Fact]
    public void FollowingTheMouse_UsesTheScreenUnderTheCursor()
    {
        Assert.Equal(new[] { Side }, DockMonitorSelection.ForCursor([Main, Side], Side.DeviceName));
    }

    [Fact]
    public void FollowingTheMouse_WithoutAKnownCursorScreen_UsesThePrimary()
    {
        Assert.Equal(new[] { Main }, DockMonitorSelection.ForCursor([Side, Main], cursorDeviceName: null));
    }
}
