using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class TabRegionShapeTests
{
    private static readonly Rect[] NoCircles = Array.Empty<Rect>();
    private static readonly Rect[] TwoCircles = { new(0, 300, 32, 32), new(0, 340, 32, 32) };

    [Fact]
    public void BuildRegion_NoTabsNoCirclesNoContainer_ReturnsNothing()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), NoCircles, null, cornerRadius: 9);
        Assert.Empty(pieces);
    }

    [Fact]
    public void BuildRegion_Tabs_KeepTheGivenRadiusAndAreSquaredOnTheRight()
    {
        var tabs = new[]
        {
            new Rect(0, 0, 104, 100),
            new Rect(0, 108, 104, 100),
        };

        var pieces = TabRegionShape.BuildRegion(tabs, NoCircles, null, cornerRadius: 12);

        Assert.Equal(2, pieces.Count);
        for (int i = 0; i < tabs.Length; i++)
        {
            Assert.Equal(tabs[i], pieces[i].Bounds);
            Assert.Equal(12, pieces[i].CornerRadius);
            // El lado derecho va a ras del canto de la pantalla: redondearlo dejaria ver el
            // escritorio por una muesca en el borde.
            Assert.True(pieces[i].SquareRightSide);
        }
    }

    [Fact]
    public void BuildRegion_FooterButtons_AreFullCircles()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), TwoCircles, null, cornerRadius: 12);

        Assert.Equal(2, pieces.Count);
        foreach (var piece in pieces)
        {
            // Circulos completos, no la caja rectangular que los envolvia antes.
            Assert.False(piece.SquareRightSide);
            Assert.Equal(16, piece.CornerRadius);
        }
    }

    [Fact]
    public void BuildRegion_RestContainer_ComesFirstAndIsAFullPill()
    {
        var container = new Rect(0, 100, 26, 122);
        var tabs = new[] { new Rect(0, 0, 104, 100) };

        var pieces = TabRegionShape.BuildRegion(tabs, TwoCircles, container, cornerRadius: 12);

        // Primero, para que quede debajo del resto en el orden de union.
        Assert.Equal(container, pieces[0].Bounds);
        Assert.Equal(13, pieces[0].CornerRadius); // media anchura: pastilla completa
        Assert.False(pieces[0].SquareRightSide);
        Assert.Equal(1 + tabs.Length + TwoCircles.Length, pieces.Count);
    }

    [Fact]
    public void BuildRegion_RestContainer_IsDroppedOnceItHasNoWidth()
    {
        // ContainerProgress lo lleva a cero en cuanto arranca la transicion; una pieza de ancho 0
        // no debe llegar al interop.
        var pieces = TabRegionShape.BuildRegion(
            Array.Empty<Rect>(), NoCircles, new Rect(0, 100, 0, 122), cornerRadius: 12);
        Assert.Empty(pieces);
    }

    // --- Contenedor de reposo ------------------------------------------------------------------

    [Fact]
    public void ContainerProgress_ReachesOneWellBeforeTheFirstTabDoes()
    {
        // Se va antes que la primera pestana a proposito: al abrirse, las pestanas se reparten por
        // toda la ventana mientras el contenedor solo cubre la tira corta de reposo, asi que
        // dejarlo puesto lo convertiria en una barra oscura suelta en medio del abanico.
        Assert.Equal(1, TabRegionShape.ContainerProgress(0.4));
        Assert.True(TabRegionShape.ContainerProgress(0.2) > 0.2);
    }

    [Fact]
    public void ContainerProgress_IsPinnedAtRestAndClamped()
    {
        Assert.Equal(0, TabRegionShape.ContainerProgress(0));
        Assert.Equal(1, TabRegionShape.ContainerProgress(1));
        Assert.Equal(0, TabRegionShape.ContainerProgress(-1));
    }

    // --- Curva ---------------------------------------------------------------------------------

    [Fact]
    public void EaseOutQuintic_IsPinnedAtBothEnds()
    {
        Assert.Equal(0, TabRegionShape.EaseOutQuintic(0));
        Assert.Equal(1, TabRegionShape.EaseOutQuintic(1));
    }

    [Fact]
    public void EaseOutQuintic_ClampsOutsideTheUnitInterval()
    {
        Assert.Equal(0, TabRegionShape.EaseOutQuintic(-3));
        Assert.Equal(1, TabRegionShape.EaseOutQuintic(4));
    }

    [Fact]
    public void EaseOutQuintic_FrontLoadsMostOfTheMotion()
    {
        // Lo que la QuadraticEase anterior no hacía: a mitad de tiempo ya está muy cerca del
        // destino, y el resto es asentamiento. Es lo que se lee como "viene a pararse" en vez de
        // como un deslizamiento a velocidad constante.
        Assert.True(TabRegionShape.EaseOutQuintic(0.5) > 0.9);
        Assert.True(TabRegionShape.EaseOutQuintic(0.25) > 0.75);
    }

    [Fact]
    public void EaseOutQuintic_IsMonotonic()
    {
        double previous = -1;
        for (int i = 0; i <= 20; i++)
        {
            double value = TabRegionShape.EaseOutQuintic(i / 20.0);
            Assert.True(value >= previous, $"la curva retrocede en t={i / 20.0}");
            previous = value;
        }
    }

    // --- Escalonado ----------------------------------------------------------------------------

    [Fact]
    public void StaggerDelay_GrowsWithIndexButIsCapped()
    {
        Assert.Equal(0, TabRegionShape.StaggerDelayMs(0));
        Assert.Equal(TabRegionShape.TabStaggerMs, TabRegionShape.StaggerDelayMs(1));
        Assert.Equal(TabRegionShape.MaxTotalStaggerMs, TabRegionShape.StaggerDelayMs(1000));
    }

    [Fact]
    public void TotalDuration_IsBoundedRegardlessOfNoteCount()
    {
        // El modelo anterior (55ms por índice, más 190ms de espera antes de mostrar nada) dejaba
        // la última pestaña sin asentar hasta ~760ms con 6 notas, muy por encima del presupuesto
        // de 250-350ms que pide una UI de producto.
        double worstCase = TabRegionShape.TotalDurationMs(1000);
        Assert.Equal(TabRegionShape.MaxTotalStaggerMs + TabRegionShape.TabSweepMs, worstCase);
        Assert.True(worstCase <= 350, $"la transición más larga posible dura {worstCase}ms");
    }

    [Fact]
    public void TabProgress_StartsAtZeroAndReachesOneWithinTheTotalDuration()
    {
        const int noteCount = 6;
        double total = TabRegionShape.TotalDurationMs(noteCount);

        for (int i = 0; i < noteCount; i++)
        {
            Assert.Equal(0, TabRegionShape.TabProgress(i, 0));
            Assert.Equal(1, TabRegionShape.TabProgress(i, total));
        }
    }

    [Fact]
    public void TabProgress_LaterTabsLagBehindEarlierOnes()
    {
        // A media transición, la primera pestaña va por delante de la última: eso es el abanico.
        double first = TabRegionShape.TabProgress(0, 90);
        double last = TabRegionShape.TabProgress(5, 90);
        Assert.True(first > last, $"la primera ({first}) debería ir por delante de la última ({last})");
    }

    [Fact]
    public void TabCollapseProgress_RunsInReverseOrder()
    {
        // Al replegar se va primero la más larga (la última), para que el abanico se cierre sobre
        // sí mismo en vez de deshacerse por donde se hizo.
        const int noteCount = 6;
        double first = TabRegionShape.TabCollapseProgress(0, noteCount, 90);
        double last = TabRegionShape.TabCollapseProgress(5, noteCount, 90);
        Assert.True(last < first, $"la última ({last}) debería haberse ido antes que la primera ({first})");
    }

    [Fact]
    public void TabCollapseProgress_StartsFullyOpenAndEndsFullyClosed()
    {
        const int noteCount = 4;
        double total = TabRegionShape.TotalDurationMs(noteCount);

        for (int i = 0; i < noteCount; i++)
        {
            Assert.Equal(1, TabRegionShape.TabCollapseProgress(i, noteCount, 0));
            Assert.Equal(0, TabRegionShape.TabCollapseProgress(i, noteCount, total));
        }
    }

    // --- Barrido -------------------------------------------------------------------------------

    [Fact]
    public void SweptWidth_InterpolatesBetweenSliverAndFullWidth()
    {
        Assert.Equal(20, TabRegionShape.SweptWidth(20, 120, 0));
        Assert.Equal(120, TabRegionShape.SweptWidth(20, 120, 1));
        Assert.Equal(70, TabRegionShape.SweptWidth(20, 120, 0.5));
    }

    [Fact]
    public void SweptWidth_ClampsProgressSoTheRegionNeverOvershoots()
    {
        Assert.Equal(20, TabRegionShape.SweptWidth(20, 120, -1));
        Assert.Equal(120, TabRegionShape.SweptWidth(20, 120, 2));
    }
}
