using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

/// <summary>
/// The dock's corners are their own style, no longer tied to the app's «square corners». The factor multiplies the radii the app already uses
/// (the rounded ones, which are drawn 1.35 times the XAML values): 0 is square, 1 is today's rounded look, 2 is pill-like.
/// </summary>
public sealed class DockCornersTests
{
    [Theory]
    [InlineData(true, 0.0)]
    [InlineData(false, 1.0)]
    public void FollowLook_IsExactlyTodaysDock_WhateverTheLookSays(bool lookSquare, double expected)
    {
        Assert.Equal(expected, DockCorners.Scale(DockCornerStyle.FollowLook, lookSquare));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheDockStyleIsIndependentOfTheLook(bool lookSquare)
    {
        Assert.Equal(0.0, DockCorners.Scale(DockCornerStyle.Square, lookSquare));
        Assert.Equal(1.0, DockCorners.Scale(DockCornerStyle.Soft, lookSquare));
        Assert.Equal(2.0, DockCorners.Scale(DockCornerStyle.Round, lookSquare));
    }

    [Fact]
    public void ANumberFromTheFuture_FollowsTheLook()
    {
        // A value written by another version or by hand must behave as the default, never as «square» or a huge radius.
        var odd = (DockCornerStyle)9;
        Assert.Equal(1.0, DockCorners.Scale(odd, lookSquare: false));
        Assert.Equal(0.0, DockCorners.Scale(odd, lookSquare: true));
    }

    [Theory]
    [InlineData(DockCornerStyle.FollowLook, true, true)]
    [InlineData(DockCornerStyle.FollowLook, false, false)]
    [InlineData(DockCornerStyle.Square, false, true)]
    [InlineData(DockCornerStyle.Soft, true, false)]
    [InlineData(DockCornerStyle.Round, true, false)]
    public void IsSquare_MeansScaleZero(DockCornerStyle style, bool lookSquare, bool expected)
    {
        Assert.Equal(expected, DockCorners.IsSquare(style, lookSquare));
    }

    [Fact]
    public void DefaultIsFollowLook_SoAnOldSettingsFileKeepsTodaysDock()
    {
        Assert.Equal(DockCornerStyle.FollowLook, default(DockCornerStyle));
        Assert.Equal(DockCornerStyle.FollowLook, new AppSettings().DockCorners);
    }
}
