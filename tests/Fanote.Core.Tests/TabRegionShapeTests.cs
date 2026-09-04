using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class TabRegionShapeTests
{
    private static readonly Rect FooterRect = new(0, 300, 116, 80);

    [Fact]
    public void BuildRegion_NoTabs_ReturnsOnlyTheFooterPiece()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), FooterRect, cornerRadius: 9);

        var piece = Assert.Single(pieces);
        Assert.Equal(FooterRect, piece.Bounds);
        Assert.Equal(0, piece.CornerRadius);
    }

    [Fact]
    public void BuildRegion_OneTab_ReturnsTabPieceThenFooterPiece()
    {
        var tab = new Rect(0, 0, 32, 80);

        var pieces = TabRegionShape.BuildRegion(new[] { tab }, FooterRect, cornerRadius: 9);

        Assert.Equal(2, pieces.Count);
        Assert.Equal(tab, pieces[0].Bounds);
        Assert.Equal(9, pieces[0].CornerRadius);
        Assert.Equal(FooterRect, pieces[1].Bounds);
        Assert.Equal(0, pieces[1].CornerRadius);
    }

    [Fact]
    public void BuildRegion_MultipleTabs_EachTabGetsTheGivenCornerRadius()
    {
        var tabs = new[]
        {
            new Rect(0, 0, 32, 80),
            new Rect(0, 56, 46, 80),
            new Rect(0, 112, 60, 80),
        };

        var pieces = TabRegionShape.BuildRegion(tabs, FooterRect, cornerRadius: 12);

        Assert.Equal(4, pieces.Count); // 3 pestañas + footer
        for (int i = 0; i < tabs.Length; i++)
        {
            Assert.Equal(tabs[i], pieces[i].Bounds);
            Assert.Equal(12, pieces[i].CornerRadius);
        }
        Assert.Equal(0, pieces[^1].CornerRadius);
    }

    [Fact]
    public void BuildRegion_FooterPiece_AlwaysHasZeroCornerRadius()
    {
        var pieces = TabRegionShape.BuildRegion(Array.Empty<Rect>(), FooterRect, cornerRadius: 999);

        Assert.Equal(0, Assert.Single(pieces).CornerRadius);
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
