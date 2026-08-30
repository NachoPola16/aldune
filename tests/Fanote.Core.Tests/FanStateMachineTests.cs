using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class FanStateMachineTests
{
    [Fact]
    public void StartsCollapsed()
    {
        var sut = new FanStateMachine();
        Assert.False(sut.IsExpanded);
    }

    [Fact]
    public void PointerEntered_Expands()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void PointerLeft_ThenTimerElapsed_Collapses()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        sut.PointerLeft();
        sut.CollapseTimerElapsed();
        Assert.False(sut.IsExpanded);
    }

    [Fact]
    public void PointerLeft_ThenReEntered_CancelsPendingCollapse()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        sut.PointerLeft();
        sut.PointerEntered();
        sut.CollapseTimerElapsed();
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void CollapseTimerElapsed_WithoutPriorPointerLeft_DoesNothing()
    {
        var sut = new FanStateMachine();
        sut.PointerEntered();
        sut.CollapseTimerElapsed();
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void ExpansionChanged_FiresOnlyOnActualTransition()
    {
        var sut = new FanStateMachine();
        int fireCount = 0;
        sut.ExpansionChanged += (_, _) => fireCount++;

        sut.PointerEntered();
        sut.PointerEntered();

        Assert.Equal(1, fireCount);
    }
}
