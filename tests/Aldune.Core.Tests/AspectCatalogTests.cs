using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class AspectCatalogTests
{
    [Fact]
    public void Retro_AreTheSixAspectsOfTheSpec_WithNumbersFrom5()
    {
        Assert.Equal(
            new[] { AppearanceMode.XpLight, AppearanceMode.XpDark, AppearanceMode.TelecomLight,
                    AppearanceMode.TelecomDark, AppearanceMode.Bash, AppearanceMode.Phosphor },
            AspectCatalog.Retro.Select(a => a.Mode));
        Assert.Equal(new[] { 5, 6, 7, 8, 9, 10 }, AspectCatalog.Retro.Select(a => (int)a.Mode));
        Assert.Equal(6, AspectCatalog.Retro.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public void ExistingAspects_HaveNoCatalogEntry()
    {
        foreach (var mode in new[] { AppearanceMode.Dark, AppearanceMode.Light, AppearanceMode.System, AppearanceMode.Pastel, AppearanceMode.Midnight })
            Assert.Null(AspectCatalog.For(mode));
    }

    [Fact]
    public void EverySlot_HasAValidDefaultAndSuggestions()
    {
        foreach (var aspect in AspectCatalog.Retro)
            foreach (var slot in aspect.Slots)
            {
                Assert.True(NoteDisplayColor.IsActive(slot.Default), $"{aspect.Id}.{slot.Id}");
                Assert.All(slot.Suggestions, s => Assert.True(NoteDisplayColor.IsActive(s), $"{aspect.Id}.{slot.Id} {s}"));
            }
    }

    [Fact]
    public void BashPresets_FillEverySlot()
    {
        var bash = AspectCatalog.For(AppearanceMode.Bash)!;
        Assert.Equal(new[] { "gruvbox", "ubuntu", "tango" }, bash.Presets.Select(p => p.Id));
        foreach (var preset in bash.Presets)
            Assert.Equal(bash.Slots.Select(s => s.Id).Order(), preset.Colors.Keys.Order());
    }

    [Fact]
    public void Resolve_WithNothingChosen_GivesTheDefaults()
    {
        var colors = AspectCatalog.Resolve(AppearanceMode.XpLight, null);

        Assert.Equal("#0055E5", colors["titleBar"]);
        Assert.Equal("#3C9A3C", colors["accent"]);
    }

    [Fact]
    public void Resolve_KeepsValidChoices_AndIgnoresTheRest()
    {
        // Un settings.json editado a mano no puede romper el aspecto: lo inválido cae en el valor por defecto.
        var chosen = new Dictionary<string, string>
        {
            ["titleBar"] = "#5a7a2e",
            ["accent"] = "rojo",
            ["unknown"] = "#123456",
        };

        var colors = AspectCatalog.Resolve(AppearanceMode.XpLight, chosen);

        Assert.Equal("#5A7A2E", colors["titleBar"]);
        Assert.Equal("#3C9A3C", colors["accent"]);
        Assert.False(colors.ContainsKey("unknown"));
    }

    [Fact]
    public void SuggestedThemes_FollowTheSpec()
    {
        Assert.Equal(NoteThemes.XpId, AspectCatalog.For(AppearanceMode.XpLight)!.SuggestedThemeId);
        Assert.Null(AspectCatalog.For(AppearanceMode.TelecomLight)!.SuggestedThemeId);
        foreach (var mode in new[] { AppearanceMode.XpDark, AppearanceMode.TelecomDark, AppearanceMode.Bash, AppearanceMode.Phosphor })
            Assert.Equal(NoteThemes.SereneId, AspectCatalog.For(mode)!.SuggestedThemeId);
    }
}
