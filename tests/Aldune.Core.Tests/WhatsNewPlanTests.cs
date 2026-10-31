using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class WhatsNewPlanTests
{
    private static readonly Version[] Catalog = [new(2, 4, 0), new(2, 5, 0), new(2, 6, 0), new(2, 7, 0)];

    [Fact]
    public void AFreshInstallShowsNothing() =>
        Assert.Empty(WhatsNewPlan.ToShow(null, freshInstall: true, new Version(2, 7, 0), Catalog));

    [Fact]
    public void AnUpdateListsTheVersionsAfterTheLastSeenOne_NewestFirst() =>
        Assert.Equal([new Version(2, 6, 0), new Version(2, 5, 0)], WhatsNewPlan.ToShow("2.4.0", false, new Version(2, 6, 0), Catalog));

    [Fact]
    public void NothingIsShownAgainOnceSeen() =>
        Assert.Empty(WhatsNewPlan.ToShow("2.7.0", false, new Version(2, 7, 0), Catalog));

    [Fact]
    public void AnInstallThatNeverStoredAVersionListsEverythingSinceTheBaseline() =>
        Assert.Equal(4, WhatsNewPlan.ToShow(null, false, new Version(2, 7, 0), Catalog).Count);

    [Fact]
    public void AGarbledStoredVersionIsTreatedAsUnknown() =>
        Assert.Equal(4, WhatsNewPlan.ToShow("not a version", false, new Version(2, 7, 0), Catalog).Count);

    [Fact]
    public void VersionsNewerThanTheRunningOneAreNotListed() =>
        Assert.Equal([new Version(2, 5, 0), new Version(2, 4, 0)], WhatsNewPlan.ToShow("2.3.0", false, new Version(2, 5, 1), Catalog));

    [Fact]
    public void ADowngradeShowsNothing() =>
        Assert.Empty(WhatsNewPlan.ToShow("2.7.0", false, new Version(2, 5, 0), Catalog));
}
