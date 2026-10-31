namespace Aldune.Core;

/// <summary>
/// The order tags are listed in (dock menu, manager, tag panel, the wheel's tour of the views). Alphabetical by default; the user can set
/// their own. Port of <c>TagOrder</c> in <c>android/domain</c>.
///
/// The custom order is a setting of THIS device (<see cref="AppSettings.TagOrder"/>): it never travels with the sync, so no format changes.
/// Tags named in it come first, in that order; the rest follow alphabetically, so a new tag is never lost.
/// </summary>
public static class TagOrder
{
    public static List<string> Apply(IEnumerable<string> tags, IReadOnlyList<string> custom)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < custom.Count; i++) rank.TryAdd(custom[i], i);
        return tags
            .OrderBy(tag => rank.TryGetValue(tag, out var r) ? r : int.MaxValue)
            .ThenBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Moves <paramref name="tag"/> one place (<paramref name="delta"/> -1 up, +1 down) inside the VISIBLE order and returns the whole order to
    /// save; the same list when it cannot move.
    /// </summary>
    public static List<string> Move(IReadOnlyList<string> visible, string tag, int delta)
    {
        var list = visible.ToList();
        int from = list.FindIndex(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        int to = from + delta;
        if (from < 0 || to < 0 || to >= list.Count) return list;
        var moved = list[from];
        list.RemoveAt(from);
        list.Insert(to, moved);
        return list;
    }
}
