using System.Text.Json;
using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>Atajo global opcional para abrir el gestor de notas: la ruta por teclado a cualquier nota sin pasar por el dock.</summary>
public class ManagerHotkeyTests
{
    [Fact]
    public void ItIsOffByDefault_SoItNeverTakesAShortcutFromAnotherApp()
    {
        var settings = new AppSettings();
        Assert.False(settings.ManagerHotkeyEnabled);
        Assert.Equal(HotkeyBinding.ManagerDefault, settings.ManagerHotkey);
    }

    [Fact]
    public void TheDefault_IsValidAndDoesNotClashWithTheOtherAldunePair()
    {
        var manager = HotkeyBinding.ManagerDefault;
        Assert.True(manager.IsValid);
        Assert.NotEqual(HotkeyBinding.Default, manager);
        Assert.NotEqual(HotkeyBinding.RecentNoteDefault, manager);
        Assert.NotEqual(HotkeyBinding.DockToggleDefault, manager);
        Assert.Equal("Ctrl + Alt + M", manager.DisplayName);
    }

    [Fact]
    public void ASavedCombination_IsUsed_AndABrokenOneFallsBackToTheDefault()
    {
        var saved = new AppSettings { ManagerHotkeyModifiers = HotkeyBinding.ModControl | HotkeyBinding.ModShift, ManagerHotkeyKey = 0x4F };
        Assert.Equal("Ctrl + Shift + O", saved.ManagerHotkey.DisplayName);

        var broken = new AppSettings { ManagerHotkeyModifiers = 0, ManagerHotkeyKey = 0x4F };
        Assert.Equal(HotkeyBinding.ManagerDefault, broken.ManagerHotkey);
    }

    [Fact]
    public void AnOldSettingsFile_LoadsWithTheShortcutOff()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{ \"dockEdge\": 1 }")!;
        Assert.False(settings.ManagerHotkeyEnabled);
    }
}
