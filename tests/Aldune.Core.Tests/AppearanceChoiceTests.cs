using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AppearanceChoiceTests
{
    [Fact]
    public void Select_ResetsCornersAndSignal_ToTheAspectsDefaults()
    {
        var settings = new AppSettings { SquareCorners = false, ShowSyncSignal = false };

        AppearanceChoice.Select(settings, AppearanceMode.Bash);

        Assert.Equal(AppearanceMode.Bash, settings.Appearance);
        Assert.Null(settings.SquareCorners);
        Assert.Null(settings.ShowSyncSignal);
        Assert.True(settings.CornersSquare);
        Assert.True(settings.SyncSignalVisible);
    }

    [Fact]
    public void AnExplicitChoice_WinsUntilTheNextAspectChange()
    {
        var settings = new AppSettings();
        AppearanceChoice.Select(settings, AppearanceMode.Bash);
        settings.SquareCorners = false;

        Assert.False(settings.CornersSquare);
        AppearanceChoice.Select(settings, AppearanceMode.Dark);
        Assert.False(settings.CornersSquare);
        Assert.False(settings.SyncSignalVisible);
    }

    [Fact]
    public void Select_ProposesTheAspectsTheme_UnlessItIsAlreadyActive()
    {
        var settings = new AppSettings();

        Assert.Equal(NoteThemes.XpId, AppearanceChoice.Select(settings, AppearanceMode.XpLight));
        settings.ActiveThemeId = NoteThemes.XpId;
        Assert.Null(AppearanceChoice.Select(settings, AppearanceMode.XpLight));
        Assert.Null(AppearanceChoice.Select(settings, AppearanceMode.TelecomLight));
        Assert.Null(AppearanceChoice.Select(settings, AppearanceMode.Dark));
    }

    [Fact]
    public void Select_NeverChangesTheNoteTheme()
    {
        var settings = new AppSettings { ActiveThemeId = NoteThemes.OceanId };

        AppearanceChoice.Select(settings, AppearanceMode.Bash);

        Assert.Equal(NoteThemes.OceanId, settings.ActiveThemeId);
    }
}
