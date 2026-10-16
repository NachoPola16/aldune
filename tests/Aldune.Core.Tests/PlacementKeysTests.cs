using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class PlacementKeysTests
{
    private static MonitorInfo Monitor(string device, string? stable) =>
        new(device, new WorkingArea(0, 0, 1920, 1080), 1.0, false, stable);

    [Fact]
    public void StorageKey_IsTheStableIdWhenThereIsOne()
    {
        Assert.Equal("MONITOR-GSM5B09-id", PlacementKeys.StorageKey(Monitor("DISPLAY2", "MONITOR-GSM5B09-id")));
    }

    [Fact]
    public void StorageKey_FallsBackToTheDeviceName()
    {
        Assert.Equal("DISPLAY2", PlacementKeys.StorageKey(Monitor("DISPLAY2", null)));
    }

    [Fact]
    public void LookupKeys_TryTheStableIdFirstThenTheLegacyDeviceName()
    {
        // Lo guardado antes de 1.7.9 va por DeviceName: sigue encontrándose y se migra al guardar.
        var keys = PlacementKeys.LookupKeys(Monitor("DISPLAY2", "stable"));
        Assert.Equal(new[] { "stable", "DISPLAY2" }, keys);
    }

    [Fact]
    public void LookupKeys_WithoutStableId_IsJustTheDeviceName()
    {
        Assert.Equal(new[] { "DISPLAY2" }, PlacementKeys.LookupKeys(Monitor("DISPLAY2", null)));
    }

    [Fact]
    public void LegacyKey_IsOnlyReportedWhenItDiffersFromTheStorageKey()
    {
        Assert.Equal("DISPLAY2", PlacementKeys.LegacyKey(Monitor("DISPLAY2", "stable")));
        Assert.Null(PlacementKeys.LegacyKey(Monitor("DISPLAY2", null)));
    }
}
