using Xunit;

namespace Aldune.Core.Tests;

/// <summary>Every colour the settings page suggests for an aspect has to give a legible look, not only the factory default.</summary>
public class AspectSuggestionContrastTests
{
    public static IEnumerable<object[]> Suggestions()
    {
        foreach (var aspect in AspectCatalog.Retro)
            foreach (var slot in aspect.Slots)
                foreach (var suggestion in slot.Suggestions)
                    yield return [aspect.Mode, slot.Id, suggestion];
    }

    [Theory]
    [MemberData(nameof(Suggestions))]
    public void ASuggestedColour_KeepsTheLookLegible(AppearanceMode mode, string slot, string colour)
    {
        var p = AppPalette.WithReadableDisabledText(AppPalette.For(mode, windowsUsesLight: false, new Dictionary<string, string> { [slot] = colour }));
        void Check(string a, string b, double min) =>
            Assert.True(ColorFit.Ratio(p[a], p[b]) >= min, $"{mode} {slot}={colour}: {a} on {b} = {ColorFit.Ratio(p[a], p[b]):F2} (< {min})");
        Check("Text", "Ground", 4.5);
        Check("Text", "Surface", 4.5);
        Check("TextDisabled", "Surface", AppPalette.DisabledTextContrast);
        Check("OnAccent", "Accent", 4.5);
        Check("OnAccent", "AccentHover", 4.5);
        Check("Accent", "Ground", 3);
        Check("Accent", "Surface", 3);
    }
}
