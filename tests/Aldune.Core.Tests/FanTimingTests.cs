using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class FanTimingTests
{
    [Fact]
    public void StaggerDelay_GrowsWithIndexButIsCapped()
    {
        Assert.Equal(0, FanTiming.StaggerDelayMs(0));
        Assert.Equal(FanTiming.TabStaggerMs, FanTiming.StaggerDelayMs(1));
        Assert.Equal(FanTiming.MaxTotalStaggerMs, FanTiming.StaggerDelayMs(1000));
    }

    [Fact]
    public void TotalDuration_IsBoundedRegardlessOfNoteCount()
    {
        // Un diseno anterior (55ms por indice mas 190ms de espera antes de mostrar nada) dejaba la
        // ultima pestana sin asentar hasta ~760ms con 6 notas, muy por encima del presupuesto de
        // 250-350ms que pide una UI de producto.
        double worstCase = FanTiming.TotalDurationMs(1000);
        Assert.Equal(FanTiming.MaxTotalStaggerMs + FanTiming.TabSweepMs, worstCase);
        Assert.True(worstCase <= 350, $"la transicion mas larga posible dura {worstCase}ms");
    }

    [Fact]
    public void TotalDuration_WithOneNote_IsJustTheSweep()
    {
        Assert.Equal(FanTiming.TabSweepMs, FanTiming.TotalDurationMs(1));
    }

    [Fact]
    public void EaseOutQuintic_IsPinnedAtBothEndsAndClamps()
    {
        Assert.Equal(0, FanTiming.EaseOutQuintic(0));
        Assert.Equal(1, FanTiming.EaseOutQuintic(1));
        Assert.Equal(0, FanTiming.EaseOutQuintic(-3));
        Assert.Equal(1, FanTiming.EaseOutQuintic(4));
    }

    [Fact]
    public void EaseOutQuintic_FrontLoadsMostOfTheMotion()
    {
        // Lo que la QuadraticEase original no hacia: a mitad de tiempo ya esta muy cerca del
        // destino y el resto es asentamiento, que es lo que se lee como "viene a pararse".
        Assert.True(FanTiming.EaseOutQuintic(0.5) > 0.9);
        Assert.True(FanTiming.EaseOutQuintic(0.25) > 0.75);
    }

    [Fact]
    public void EaseOutQuintic_IsMonotonic()
    {
        double previous = -1;
        for (int i = 0; i <= 20; i++)
        {
            double value = FanTiming.EaseOutQuintic(i / 20.0);
            Assert.True(value >= previous, $"la curva retrocede en t={i / 20.0}");
            previous = value;
        }
    }
}
