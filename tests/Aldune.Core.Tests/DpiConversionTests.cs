using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DpiConversionTests
{
    [Fact]
    public void ToWorkingArea_At100Percent_ReturnsSamePixelValues()
    {
        var pixels = new Rect(0, 0, 1920, 1080);

        var result = DpiConversion.ToWorkingArea(pixels, dpiScale: 1.0);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }

    [Fact]
    public void ToWorkingArea_At150Percent_DividesEverythingByScale()
    {
        var pixels = new Rect(100, 200, 1920, 1080);

        var result = DpiConversion.ToWorkingArea(pixels, dpiScale: 1.5);

        Assert.Equal(100 / 1.5, result.X, precision: 5);
        Assert.Equal(200 / 1.5, result.Y, precision: 5);
        Assert.Equal(1920 / 1.5, result.Width, precision: 5);
        Assert.Equal(1080 / 1.5, result.Height, precision: 5);
    }

    [Fact]
    public void ToWorkingArea_At200Percent_HalvesPixelDimensions()
    {
        var pixels = new Rect(0, 0, 3840, 2160);

        var result = DpiConversion.ToWorkingArea(pixels, dpiScale: 2.0);

        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }
}
