using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DockPlacementTests
{
    private static readonly WorkingArea Area = new(0, 0, 1440, 2520);
    private static readonly Rect Expected = new(1214, 1080, 226, 360);

    [Fact]
    public void InPlace_NeedsNothing()
    {
        Assert.Equal(DockPlacementFix.None, DockPlacement.Check(Expected, Expected, Area, Area));
    }

    [Fact]
    public void ARoundingPixel_IsNotAMove()
    {
        var actual = new Rect(1215, 1079, 226, 361);

        Assert.Equal(DockPlacementFix.None, DockPlacement.Check(actual, Expected, Area, Area));
    }

    [Fact]
    public void MovedByWindows_OnTheSameScreen_GoesBack()
    {
        // Lo que se vio al volver a extender el escritorio: Windows desplaza la ventana con la
        // pantalla, pero la pantalla del dock sigue igual.
        var actual = new Rect(-1440, 419, 226, 360);

        Assert.Equal(DockPlacementFix.MoveBack, DockPlacement.Check(actual, Expected, Area, Area));
    }

    [Fact]
    public void WithAnAutoHideTaskbar_TheScreenAreaIsCompared_NotTheInsetOne()
    {
        // El dock que comparte borde con una barra de tareas oculta se construye con el área recortada
        // unos DIP, pero Windows devuelve el área de la pantalla sin recortar. Comparando la recortada,
        // cada desplazamiento de Windows parecía un cambio de pantalla y reconstruía los docks.
        var actual = new Rect(-1440, 419, 226, 360);
        var inset = DockHoverZone.Inset(Area, EdgePosition.Right, DockHoverZone.AutoHideTaskbarClearance);

        Assert.Equal(DockPlacementFix.Rebuild, DockPlacement.Check(actual, Expected, inset, Area));
        Assert.Equal(DockPlacementFix.MoveBack, DockPlacement.Check(actual, Expected, Area, Area));
    }

    [Fact]
    public void WhenTheScreenItselfChanged_TheDocksAreRebuilt()
    {
        var actual = new Rect(900, 400, 226, 360);
        var nowLandscape = new WorkingArea(0, 0, 2560, 1400);

        Assert.Equal(DockPlacementFix.Rebuild, DockPlacement.Check(actual, Expected, Area, nowLandscape));
    }

    [Fact]
    public void WhenTheScreenIsGone_TheDocksAreRebuilt()
    {
        var actual = new Rect(900, 400, 226, 360);

        Assert.Equal(DockPlacementFix.Rebuild, DockPlacement.Check(actual, Expected, Area, currentArea: null));
    }
}
