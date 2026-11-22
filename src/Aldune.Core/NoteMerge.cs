namespace Aldune.Core;

public sealed record NoteMergeResult(Note? Merged, bool Conflict);

/// <summary>
/// Merges the two versions of a note that both changed since the last agreed one (<c>baseNote</c>). The text is merged by
/// lines (<see cref="LineMerge"/>); preferences that are not content (colour, pinned, completed-tasks policy, dock position)
/// follow whichever side changed them, or the newer side when both did, without raising a conflict; tags are merged as
/// sets against the base. It is symmetric: swapping the sides gives the same content. A conflict is returned for protected
/// notes, for the same lines changed differently, for both sides changing the state differently, and for a trash against a
/// text edit. The caller sets <c>UpdatedAt</c> when it publishes the result.
/// </summary>
public static class NoteMerge
{
    public static NoteMergeResult Merge(Note baseNote, Note local, string localDeviceId, Note remote, string remoteDeviceId)
    {
        if (baseNote.IsProtected || local.IsProtected || remote.IsProtected) return new(null, true);

        var text = LineMerge.Merge(baseNote.Text, local.Text, remote.Text);
        if (text.Conflict) return new(null, true);

        bool localTextChanged = local.Text != baseNote.Text;
        bool remoteTextChanged = remote.Text != baseNote.Text;
        bool localStateChanged = local.State != baseNote.State;
        bool remoteStateChanged = remote.State != baseNote.State;

        NoteState state;
        if (localStateChanged && remoteStateChanged)
        {
            if (local.State != remote.State) return new(null, true);
            state = local.State;
        }
        else if (localStateChanged)
        {
            if (local.State == NoteState.Trashed && remoteTextChanged) return new(null, true);
            state = local.State;
        }
        else if (remoteStateChanged)
        {
            if (remote.State == NoteState.Trashed && localTextChanged) return new(null, true);
            state = remote.State;
        }
        else state = baseNote.State;

        bool localIsNewer = Compare(local, localDeviceId, remote, remoteDeviceId) > 0;
        var newer = localIsNewer ? local : remote;

        T Scalar<T>(Func<Note, T> get)
        {
            var original = get(baseNote);
            bool localChanged = !EqualityComparer<T>.Default.Equals(get(local), original);
            bool remoteChanged = !EqualityComparer<T>.Default.Equals(get(remote), original);
            if (localChanged && !remoteChanged) return get(local);
            if (remoteChanged && !localChanged) return get(remote);
            return get(newer);
        }

        var tags = baseNote.Tags
            .Where(tag => local.Tags.Contains(tag) && remote.Tags.Contains(tag))
            .Union(local.Tags.Where(tag => !baseNote.Tags.Contains(tag)))
            .Union(remote.Tags.Where(tag => !baseNote.Tags.Contains(tag)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        var merged = new Note
        {
            Id = newer.Id,
            Text = text.Text,
            Color = Scalar(note => note.Color),
            CreatedAt = newer.CreatedAt,
            UpdatedAt = newer.UpdatedAt,
            State = state,
            ScreenOrigin = newer.ScreenOrigin,
            Tags = tags,
            DockPosition = Scalar(note => note.DockPosition),
            IsPinnedToTop = Scalar(note => note.IsPinnedToTop),
            CompletedTasks = Scalar(note => note.CompletedTasks)
        };
        return new(merged, false);
    }

    // The same total order as SyncVersion.Compare: date, then device id (ordinal).
    private static int Compare(Note a, string deviceA, Note b, string deviceB)
    {
        int time = a.UpdatedAt.CompareTo(b.UpdatedAt);
        return time != 0 ? time : string.CompareOrdinal(deviceA, deviceB);
    }
}
