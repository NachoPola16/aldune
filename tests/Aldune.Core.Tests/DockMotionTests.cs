using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

/// <summary>
/// One factor for every dock animation. Normal has to be today's numbers exactly (the setting must not change the dock of anyone who never
/// touches it), Off is zero and the system's «animation effects» switch always wins.
/// </summary>
public sealed class DockMotionTests
{
    [Theory]
    [InlineData(95)]
    [InlineData(190)]
    [InlineData(26)]
    [InlineData(0)]
    public void Normal_IsTodaysNumbers(double baseMs)
    {
        Assert.Equal(baseMs, DockMotion.Scale(baseMs, DockAnimationMode.Normal));
    }

    [Fact]
    public void Factors()
    {
        Assert.Equal(1.5, DockMotion.Factor(DockAnimationMode.Calm));
        Assert.Equal(0.6, DockMotion.Factor(DockAnimationMode.Quick));
        Assert.Equal(1.0, DockMotion.Factor(DockAnimationMode.Normal));
    }

    [Fact]
    public void Off_IsZero_AndNotEnabled()
    {
        Assert.Equal(0, DockMotion.Scale(190, DockAnimationMode.Off));
        Assert.False(DockMotion.Enabled(DockAnimationMode.Off, systemAnimations: true));
    }

    [Theory]
    [InlineData(DockAnimationMode.Normal)]
    [InlineData(DockAnimationMode.Off)]
    [InlineData(DockAnimationMode.Calm)]
    [InlineData(DockAnimationMode.Quick)]
    public void SystemSwitchAlwaysWins(DockAnimationMode mode)
    {
        Assert.False(DockMotion.Enabled(mode, systemAnimations: false));
    }

    [Theory]
    [InlineData(DockAnimationMode.Normal)]
    [InlineData(DockAnimationMode.Calm)]
    [InlineData(DockAnimationMode.Quick)]
    public void WithSystemAnimationsOn_EveryModeButOffIsEnabled(DockAnimationMode mode)
    {
        Assert.True(DockMotion.Enabled(mode, systemAnimations: true));
    }

    [Fact]
    public void OutOfRangeNumberBehavesAsNormal()
    {
        // A number written by a future version or by hand in settings.json: it must never mean Off, nor crash.
        var odd = (DockAnimationMode)9;
        Assert.Equal(1.0, DockMotion.Factor(odd));
        Assert.True(DockMotion.Enabled(odd, systemAnimations: true));
        Assert.Equal(95, DockMotion.Scale(95, odd));
    }

    [Fact]
    public void NeverRoundsAShortDurationToZero()
    {
        // A zero-length Duration with a BeginTime reorders the frames of a staggered entrance.
        Assert.True(DockMotion.Scale(35, DockAnimationMode.Quick) >= 1);
        Assert.True(DockMotion.Scale(1, DockAnimationMode.Quick) >= 1);
    }

    [Fact]
    public void CalmStaysBoundedWithManyNotes()
    {
        // The entrance stagger is capped (FanTiming), so slowing it down must not make it grow with the number of notes.
        Assert.InRange(DockMotion.Scale(FanTiming.TotalDurationMs(1000), DockAnimationMode.Calm), 0, 600);
    }
}
