namespace Aldune.Core.Tests;

/// <summary>The global quick-find palette: which notes it lists for a query and in what order, and what it may copy.</summary>
public class QuickFindTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static Note Make(string text, int minutesAgo = 0, string[]? tags = null, NoteState state = NoteState.Active,
        bool isProtected = false, string? visibleName = null) => new()
    {
        Id = Guid.NewGuid(),
        Text = text,
        Color = "#EBD38B",
        CreatedAt = Now.AddMinutes(-minutesAgo),
        UpdatedAt = Now.AddMinutes(-minutesAgo),
        State = state,
        ScreenOrigin = "primary",
        Tags = tags ?? [],
        IsProtected = isProtected,
        ProtectedTitle = visibleName,
    };

    private static string[] Titles(IEnumerable<QuickFindHit> hits) => hits.Select(h => h.Title).ToArray();

    [Fact]
    public void EmptyQuery_ListsActiveNotesMostRecentFirst()
    {
        var notes = new[] { Make("Old", 500), Make("Newest", 1), Make("Archived", 2, state: NoteState.Archived), Make("Trashed", 3, state: NoteState.Trashed), Make("Middle", 60) };

        Assert.Equal(["Newest", "Middle", "Old"], Titles(QuickFind.Search(notes, "")));
        Assert.Equal(["Newest", "Middle", "Old"], Titles(QuickFind.Search(notes, "   ")));
        Assert.Equal(["Newest", "Middle", "Old"], Titles(QuickFind.Search(notes, null)));
    }

    [Fact]
    public void TitleMatches_GoBeforeBodyMatches_EvenWhenTheBodyMatchIsNewer()
    {
        var notes = new[]
        {
            Make("Shopping\nbuy milk and a budget notebook", 1),
            Make("Budget 2027\nrent, food", 900),
        };

        Assert.Equal(["Budget 2027", "Shopping"], Titles(QuickFind.Search(notes, "budget")));
    }

    [Fact]
    public void WithinTheSameTier_TheMostRecentComesFirst()
    {
        var notes = new[] { Make("Trip plan old", 300), Make("Trip plan new", 5), Make("Trip plan mid", 100) };

        Assert.Equal(["Trip plan new", "Trip plan mid", "Trip plan old"], Titles(QuickFind.Search(notes, "trip")));
    }

    [Fact]
    public void ASubstringInTheTitle_BeatsATypoTolerantTitleMatch()
    {
        // "recipe" is contained in the old title; "recipies" is not, and only the tolerant search reaches the newer one.
        var contains = Make("Recipe box", 400);
        var tolerant = Make("Recipies", 1);

        Assert.Equal(["Recipe box", "Recipies"], Titles(QuickFind.Search([tolerant, contains], "recipe")));
    }

    [Fact]
    public void TheSearchForgivesCaseAccentsAndATypo()
    {
        var notes = new[] { Make("Reunión de equipo\nagenda", 10), Make("Groceries", 5) };

        Assert.Equal(["Reunión de equipo"], Titles(QuickFind.Search(notes, "REUNION")));
        Assert.Equal(["Reunión de equipo"], Titles(QuickFind.Search(notes, "reunon")));
        Assert.Empty(QuickFind.Search(notes, "zzzzqq"));
    }

    [Fact]
    public void ABodyOnlyMatch_ShowsTheLineThatMatched_NotTheStartOfTheNote()
    {
        var notes = new[] { Make("Meeting\nagenda\ncall the plumber about the boiler\nthanks", 10) };

        var hit = Assert.Single(QuickFind.Search(notes, "boiler"));
        Assert.Equal("call the plumber about the boiler", hit.Snippet);
    }

    [Fact]
    public void ATitleMatch_ShowsTheUsualPreviewAsSnippet()
    {
        var hit = Assert.Single(QuickFind.Search([Make("Shopping\nmilk\neggs", 1)], "shop"));
        Assert.Equal("milk eggs", hit.Snippet);
    }

    [Fact]
    public void Tags_AreSearchedAndReturned()
    {
        var notes = new[] { Make("Plain note", 1, tags: ["work", "urgent"]), Make("Other", 2) };

        var hit = Assert.Single(QuickFind.Search(notes, "urgent"));
        Assert.Equal("Plain note", hit.Title);
        Assert.Equal(["work", "urgent"], hit.Tags);
    }

    [Fact]
    public void ALockedNote_ShowsOnlyItsVisibleName_AndNothingElseIsSearchedOrShown()
    {
        var locked = Make("secret body that must not leak", 1, tags: ["private"], isProtected: true, visibleName: "Bank codes");
        var hidden = Make("another secret", 2, tags: ["private"], isProtected: true);

        var byName = Assert.Single(QuickFind.Search([locked, hidden], "bank"));
        Assert.True(byName.IsLocked);
        Assert.Equal("Bank codes", byName.Title);
        Assert.Equal("", byName.Snippet);
        Assert.Empty(byName.Tags);

        // Neither the body, nor the tags, nor a note without a visible name can be reached by a query.
        Assert.Empty(QuickFind.Search([locked, hidden], "secret"));
        Assert.Empty(QuickFind.Search([locked, hidden], "private"));
    }

    [Fact]
    public void ALockedNoteWithoutName_IsListedWithoutAQuery_WithAnEmptyTitleForTheUiToName()
    {
        var hit = Assert.Single(QuickFind.Search([Make("x", 1, isProtected: true)], ""));
        Assert.True(hit.IsLocked);
        Assert.Equal("", hit.Title);
    }

    [Fact]
    public void CopyText_NeverReturnsLockedContent_EvenIfTheNoteIsUnlockedInMemory()
    {
        var locked = Make("secret", 1, isProtected: true);
        locked.IsUnlocked = true;

        Assert.Null(QuickFind.CopyText(locked));
        Assert.Equal("line one\nline two", QuickFind.CopyText(Make("line one\nline two")));
    }

    [Fact]
    public void TheListIsCapped()
    {
        var notes = Enumerable.Range(0, 100).Select(i => Make("Note " + i, i)).ToList();

        Assert.Equal(QuickFind.DefaultLimit, QuickFind.Search(notes, "").Count);
        Assert.Equal(5, QuickFind.Search(notes, "", limit: 5).Count);
    }

    [Fact]
    public void ANoteWithNoTitle_UsesThePlaceholderTitle()
    {
        var hit = Assert.Single(QuickFind.Search([Make("\n\nbody only", 1)], ""));
        Assert.Equal(NoteTitleHelper.PlaceholderTitle, hit.Title);
    }

    [Fact]
    public void TheHotkey_IsOffByDefault_AndDoesNotClashWithTheOtherAldunePairs()
    {
        var settings = new AppSettings();
        Assert.False(settings.QuickFindHotkeyEnabled);
        Assert.Equal(HotkeyBinding.QuickFindDefault, settings.QuickFindHotkey);

        var quick = HotkeyBinding.QuickFindDefault;
        Assert.True(quick.IsValid);
        Assert.Equal("Ctrl + Alt + F", quick.DisplayName);
        foreach (var other in new[]
                 {
                     HotkeyBinding.Default, HotkeyBinding.RecentNoteDefault, HotkeyBinding.ManagerDefault,
                     HotkeyBinding.DockToggleDefault, HotkeyBinding.DockFocusDefault,
                 })
            Assert.NotEqual(other, quick);
    }

    [Fact]
    public void ASavedCombination_IsUsed_ABrokenOneFallsBack_AndAnOldSettingsFileLoadsWithTheShortcutOff()
    {
        var saved = new AppSettings { QuickFindHotkeyModifiers = HotkeyBinding.ModControl | HotkeyBinding.ModShift, QuickFindHotkeyKey = 0x4B };
        Assert.Equal("Ctrl + Shift + K", saved.QuickFindHotkey.DisplayName);

        var broken = new AppSettings { QuickFindHotkeyModifiers = 0, QuickFindHotkeyKey = 0x4B };
        Assert.Equal(HotkeyBinding.QuickFindDefault, broken.QuickFindHotkey);

        // A settings.json written by 3.3.0 has none of these fields.
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{ \"dockEdge\": 1 }")!;
        Assert.False(old.QuickFindHotkeyEnabled);
        Assert.Null(old.QuickFindHotkeyModifiers);
        Assert.Null(old.QuickFindHotkeyKey);
    }
}
