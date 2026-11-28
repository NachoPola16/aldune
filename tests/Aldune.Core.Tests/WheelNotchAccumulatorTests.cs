namespace Aldune.Core.Tests;

/// <summary>
/// The dock's wheel changes the view one step per full wheel notch (120). A precision trackpad sends dozens of small deltas per gesture
/// (and a tail of inertia), so counting events instead of notches made it run through the views.
/// </summary>
public sealed class WheelNotchAccumulatorTests
{
    private static readonly DateTime T0 = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-120, 1)]
    [InlineData(120, -1)]
    [InlineData(-240, 1)]
    public void AMouseNotchIsOneStepAtOnce(int delta, int expected)
    {
        var acc = new WheelNotchAccumulator();
        Assert.Equal(expected, acc.Add(delta, T0));
    }

    [Fact]
    public void ConsecutiveMouseNotchesEachGiveAStep()
    {
        var acc = new WheelNotchAccumulator();
        Assert.Equal(1, acc.Add(-120, T0));
        Assert.Equal(1, acc.Add(-120, T0.AddMilliseconds(60)));
        Assert.Equal(1, acc.Add(-120, T0.AddMilliseconds(120)));
    }

    [Fact]
    public void SmallDeltasAddUpToOneStep()
    {
        var acc = new WheelNotchAccumulator();
        int steps = 0;
        for (int i = 0; i < 12; i++) steps += acc.Add(-10, T0.AddMilliseconds(i * 10)); // 12 x 10 = 120
        Assert.Equal(1, steps);
    }

    [Fact]
    public void AShortTrackpadSwipeThatDoesNotReachANotchGivesNoStep()
    {
        var acc = new WheelNotchAccumulator();
        int steps = 0;
        for (int i = 0; i < 8; i++) steps += acc.Add(-10, T0.AddMilliseconds(i * 10)); // 80
        Assert.Equal(0, steps);
    }

    [Fact]
    public void ALongSwipeWithInertiaMovesOnlyOneStepPerCooldown()
    {
        var acc = new WheelNotchAccumulator();
        int steps = 0;
        // 60 events of 10 over 600 ms = 600 units = five notches of distance, but the views must not run: one step per cooldown.
        for (int i = 0; i < 60; i++) steps += acc.Add(-10, T0.AddMilliseconds(i * 10));
        Assert.InRange(steps, 1, 4);
        Assert.True(steps < 5, "inertia must not turn every notch of distance into a view change");
    }

    [Fact]
    public void ChangingDirectionDropsWhatWasAccumulated()
    {
        var acc = new WheelNotchAccumulator();
        Assert.Equal(0, acc.Add(-100, T0));
        Assert.Equal(0, acc.Add(60, T0.AddMilliseconds(10)));   // reverses: starts again from 60, not from -40
        Assert.Equal(-1, acc.Add(60, T0.AddMilliseconds(20)));  // 120 upwards
    }

    [Fact]
    public void APauseDropsWhatWasAccumulated()
    {
        var acc = new WheelNotchAccumulator();
        Assert.Equal(0, acc.Add(-100, T0));
        Assert.Equal(0, acc.Add(-100, T0.AddMilliseconds(WheelNotchAccumulator.IdleResetMs + 50))); // new gesture: 100 again, not 200
    }

    [Fact]
    public void ZeroDeltaDoesNothing()
    {
        var acc = new WheelNotchAccumulator();
        Assert.Equal(0, acc.Add(0, T0));
    }
}
