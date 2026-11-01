using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aldune.Core;

namespace Aldune.Core.Tests;

public sealed class SyncLookTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private static JsonElement Json(string text)
    {
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void CaptureLeavesOutCustomThemesAndEverythingThatIsNotTheLook()
    {
        var look = SyncLook.Capture(new AppSettings());

        // A field at its default can serialize as null, so presence is checked by key.
        Assert.True(look.ContainsKey("appearance"));
        Assert.True(look.ContainsKey("noteTheme"));
        Assert.False(look.ContainsKey("customThemes"));
        Assert.False(look.ContainsKey("language"));
        Assert.False(look.ContainsKey("dockEdge"));
        Assert.False(look.ContainsKey("hotkeyEnabled"));
    }

    [Fact]
    public void ApplyingACapturedLookReproducesIt()
    {
        var source = new AppSettings { Appearance = AppearanceMode.Dark, SquareCorners = true, NoteColorAsStripe = true, ActiveThemeId = "pastel", NewNoteTone = NoteTone.Dark };
        var target = new AppSettings();

        var changed = SyncLook.Apply(target, Json(SyncLook.Capture(source).ToJsonString()));

        Assert.True(changed);
        Assert.Equal(AppearanceMode.Dark, target.Appearance);
        Assert.True(target.SquareCorners);
        Assert.True(target.NoteColorAsStripe);
        Assert.Equal("pastel", target.ActiveThemeId);
        Assert.Equal(NoteTone.Dark, target.NewNoteTone);
        Assert.Equal(SyncLook.Hash(source), SyncLook.Hash(target));
    }

    [Fact]
    public void ApplyNeverTouchesWhatIsPerDevice()
    {
        var target = new AppSettings { Language = "de", DockEdge = EdgePosition.Left, TextScalePercent = 150 };

        SyncLook.Apply(target, Json(SyncLook.Capture(new AppSettings { Appearance = AppearanceMode.Dark }).ToJsonString()));

        Assert.Equal("de", target.Language);
        Assert.Equal(EdgePosition.Left, target.DockEdge);
        Assert.Equal(150, target.TextScalePercent);
    }

    [Fact]
    public void ApplyKeepsTheCustomThemesOfTheDevice()
    {
        var target = new AppSettings { CustomThemes = [new NoteTheme { Id = "mine", Name = "Mine" }] };

        SyncLook.Apply(target, Json("""{"appearance":1,"customThemes":[]}"""));

        Assert.Single(target.CustomThemes);
    }

    [Fact]
    public void AMissingUnknownOrInvalidFieldLeavesTheSettingAlone()
    {
        var target = new AppSettings { Appearance = AppearanceMode.Dark, NewNoteTone = NoteTone.Dark };

        var changed = SyncLook.Apply(target, Json("""{"newNoteTone":"not-a-number","someFutureField":42}"""));

        Assert.False(changed);
        Assert.Equal(AppearanceMode.Dark, target.Appearance);
        Assert.Equal(NoteTone.Dark, target.NewNoteTone);
    }

    [Fact]
    public void ApplyOfSomethingThatIsNotAnObjectDoesNothing()
    {
        Assert.False(SyncLook.Apply(new AppSettings(), Json("[1,2]")));
    }

    [Fact]
    public void TheHashChangesWhenTheLookChangesAndOnlyThen()
    {
        var settings = new AppSettings();
        var before = SyncLook.Hash(settings);

        settings.Language = "fr";
        Assert.Equal(before, SyncLook.Hash(settings));

        settings.Appearance = settings.Appearance == AppearanceMode.Dark ? AppearanceMode.Light : AppearanceMode.Dark;
        Assert.NotEqual(before, SyncLook.Hash(settings));
    }

    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ALocalChangeIsPushed()
    {
        Assert.Equal(LookAction.Push, SyncLook.Decide("new", "old", T0, T0.AddHours(1)));
    }

    [Fact]
    public void ANewerRemoteIsPulledWhenNothingChangedHere()
    {
        Assert.Equal(LookAction.Pull, SyncLook.Decide("same", "same", T0, T0.AddMinutes(1)));
    }

    [Fact]
    public void NothingHappensWhenBothSidesAreAsLastTime()
    {
        Assert.Equal(LookAction.None, SyncLook.Decide("same", "same", T0, T0));
        Assert.Equal(LookAction.None, SyncLook.Decide("same", "same", T0, null));
        Assert.Equal(LookAction.None, SyncLook.Decide("same", "same", T0, T0.AddMinutes(-5)));
    }

    [Fact]
    public void TheFirstTimeTakesTheRemoteLookIfThereIsOneAndSendsOursIfNot()
    {
        Assert.Equal(LookAction.Pull, SyncLook.Decide("mine", null, null, T0));
        Assert.Equal(LookAction.Push, SyncLook.Decide("mine", null, null, null));
    }

    [Fact]
    public void SealThenOpenRoundTrips()
    {
        var look = SyncLook.Capture(new AppSettings { Appearance = AppearanceMode.Dark });

        var blob = SyncLook.Seal(Key, T0, "device-1", look);
        var opened = SyncLook.Open(Key, blob);

        Assert.NotNull(opened);
        Assert.Equal(T0, opened!.Stamp);
        Assert.Equal("device-1", opened.Device);
        Assert.Equal(look["appearance"]!.ToJsonString(), opened.Look.GetProperty("appearance").GetRawText());
    }

    [Fact]
    public void TheBlobRevealsNothingAndOpensOnlyWithTheKey()
    {
        var blob = SyncLook.Seal(Key, T0, "device-1", SyncLook.Capture(new AppSettings()));
        var text = System.Text.Encoding.UTF8.GetString(blob);

        Assert.DoesNotContain("appearance", text);
        Assert.DoesNotContain("device-1", text);
        Assert.Null(SyncLook.Open(RandomNumberGenerator.GetBytes(32), blob));
    }

    [Fact]
    public void GarbageIsNotALook()
    {
        Assert.Null(SyncLook.Open(Key, [1, 2, 3]));
        Assert.Null(SyncLook.Open(Key, System.Text.Encoding.UTF8.GetBytes("""{"c":"!","n":"!","t":"!"}""")));
        Assert.Null(SyncLook.Open(Key, []));
    }
}
