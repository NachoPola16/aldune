using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AutoScrollMathTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-10)] // justo en el borde de la zona muerta: aún quieto
    public void Velocity_InsideTheDeadZone_IsZero(double offset)
    {
        Assert.Equal(0, AutoScrollMath.Velocity(offset));
    }

    [Fact]
    public void Velocity_FollowsTheSignOfTheOffset()
    {
        Assert.True(AutoScrollMath.Velocity(60) > 0);
        Assert.True(AutoScrollMath.Velocity(-60) < 0);
        Assert.Equal(AutoScrollMath.Velocity(60), -AutoScrollMath.Velocity(-60));
    }

    [Fact]
    public void Velocity_GrowsWithDistance_AndStopsAtTheCap()
    {
        Assert.True(AutoScrollMath.Velocity(40) < AutoScrollMath.Velocity(80));
        Assert.True(AutoScrollMath.Velocity(80) < AutoScrollMath.Velocity(120));
        Assert.Equal(AutoScrollMath.MaxSpeed, AutoScrollMath.Velocity(AutoScrollMath.DeadZone + AutoScrollMath.FullSpeedDistance));
        Assert.Equal(AutoScrollMath.MaxSpeed, AutoScrollMath.Velocity(5000));
    }

    [Fact]
    public void Velocity_IsGentlerNearTheOriginThanALinearRamp()
    {
        // A un tercio del recorrido, menos de un tercio de la velocidad máxima: control fino cerca del origen.
        double third = AutoScrollMath.DeadZone + AutoScrollMath.FullSpeedDistance / 3;
        Assert.True(AutoScrollMath.Velocity(third) < AutoScrollMath.MaxSpeed / 3);
    }

    [Fact]
    public void Distance_DependsOnElapsedTime_NotOnTheNumberOfTicks()
    {
        double oneTick = AutoScrollMath.Distance(100, TimeSpan.FromMilliseconds(32));
        double twoTicks = AutoScrollMath.Distance(100, TimeSpan.FromMilliseconds(16))
                          + AutoScrollMath.Distance(100, TimeSpan.FromMilliseconds(16));
        Assert.Equal(oneTick, twoTicks, 6);
    }

    [Fact]
    public void Distance_ClampsAHugeGap_SoAStallDoesNotJump()
    {
        // Si la app se congela medio segundo, el siguiente tick no salta medio segundo de golpe.
        Assert.Equal(AutoScrollMath.Distance(100, AutoScrollMath.MaxFrame),
            AutoScrollMath.Distance(100, TimeSpan.FromSeconds(2)));
    }

    [Theory]
    [InlineData(100, false)] // un clic corto: el modo sigue activo al soltar
    [InlineData(349, false)]
    [InlineData(350, true)]  // mantenido y arrastrado: soltar lo apaga, como en Chrome
    [InlineData(2000, true)]
    public void StopsOnRelease_OnlyAfterAHold(int heldMs, bool expected)
    {
        Assert.Equal(expected, AutoScrollMath.StopsOnRelease(TimeSpan.FromMilliseconds(heldMs)));
    }
}
