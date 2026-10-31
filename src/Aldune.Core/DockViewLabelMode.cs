namespace Aldune.Core;

/// <summary>
/// When the dock shows the name of its view (a tag, Archived, Trash, All or Active) above the notes. The numbers are stored in settings.json:
/// do not renumber them.
/// </summary>
public enum DockViewLabelMode
{
    /// <summary>Fixed above the notes while the dock shows anything but «Active» and is open. The default: it is the cue that the dock is filtered.</summary>
    WhenFiltered = 0,

    /// <summary>Only for about a second after changing the view (the label of 2.4).</summary>
    WhenChanging = 1,

    /// <summary>Fixed above the notes whenever the dock is open, «Active» included.</summary>
    Always = 2
}
