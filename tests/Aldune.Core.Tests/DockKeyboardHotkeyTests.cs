using Aldune.Core;

namespace Aldune.Core.Tests;

/// <summary>Fixed global shortcut that hands the keyboard to the dock, so its tabs can be reached without the mouse.</summary>
public class DockKeyboardHotkeyTests
{
    [Fact]
    public void TheDefault_IsValid_AndDoesNotClashWithTheOtherShortcuts()
    {
        var focus = HotkeyBinding.DockFocusDefault;
        Assert.True(focus.IsValid);
        Assert.NotEqual(HotkeyBinding.Default, focus);
        Assert.NotEqual(HotkeyBinding.DockToggleDefault, focus);
        Assert.NotEqual(HotkeyBinding.RecentNoteDefault, focus);
        Assert.NotEqual(HotkeyBinding.ManagerDefault, focus);
        Assert.Equal("Ctrl + Alt + D", focus.DisplayName);
    }
}
