namespace Aldune.Core.Importing;

public sealed record ImportColorPolicy(NoteTheme Theme, NoteTone Tone, NoteColorAssignment Rule, string? FixedColor);

/// <summary>The notes that would be created and how many were left out because they are already there.</summary>
public sealed record ImportPlan(IReadOnlyList<ImportedNote> ToCreate, int Duplicates);

public sealed record ImportResult(int Created, int Failed);

/// <summary>
/// Turns a preview into notes. It only ever adds: existing notes are not read for anything but the duplicate check and never modified.
///
/// There is no clean place to remember "this note came from that source" without touching the sync format (the envelope has no such
/// field and format 4 must not change), so a note counts as already imported when an existing note has the same text and, if the
/// source knows it, the same original creation date. Importing the same export twice therefore adds nothing; a note that was edited
/// in Aldune after importing is no longer recognised and would come in again.
/// </summary>
public static class NoteImporter
{
    /// <summary>A hue further than this from every colour of the theme is not worth matching: the normal assignment is used instead.</summary>
    private const double MaxHueDistance = 50;

    public static ImportPlan Plan(ImportPreview preview, IEnumerable<Note> existing)
    {
        var known = new Dictionary<string, List<long>>();
        foreach (var note in existing)
        {
            if (note.IsProtected || string.IsNullOrWhiteSpace(note.Text)) continue;
            var key = Normalize(note.Text);
            if (!known.TryGetValue(key, out var dates)) known[key] = dates = [];
            dates.Add(note.CreatedAt.ToUnixTimeSeconds());
        }

        var batch = new HashSet<string>();
        var toCreate = new List<ImportedNote>();
        int duplicates = 0;
        foreach (var note in preview.Notes)
        {
            var key = Normalize(note.Text);
            var created = note.CreatedAt?.ToUnixTimeSeconds();
            bool already = known.TryGetValue(key, out var dates) && (created is null || dates.Contains(created.Value));
            if (already || !batch.Add($"{created}|{key}")) { duplicates++; continue; }
            toCreate.Add(note);
        }

        return new ImportPlan(toCreate, duplicates);
    }

    public static ImportResult Run(ImportPlan plan, NotesRepository repository, ImportColorPolicy colors, DateTimeOffset? now = null)
    {
        var moment = now ?? DateTimeOffset.UtcNow;
        var assigned = new List<string>();
        int created = 0, failed = 0;
        foreach (var imported in plan.ToCreate)
        {
            var color = ColorFor(imported.Hue, colors, assigned);
            assigned.Add(color);

            // Dates from another app can be missing, in the future or out of order; the bin clock restarts so the 30-day purge does not take a
            // note straight to the void just because it was deleted long ago in the other app.
            var createdAt = Clamp(imported.CreatedAt, moment) ?? moment;
            var updatedAt = imported.State == NoteState.Trashed ? moment : Clamp(imported.UpdatedAt, moment) ?? createdAt;
            if (updatedAt < createdAt) updatedAt = createdAt;

            try
            {
                // ApplySyncNote is the one public write that keeps the original dates, state, pin and tags in a single transaction.
                repository.ApplySyncNote(new Note
                {
                    Id = Guid.NewGuid(),
                    Text = imported.Text,
                    Color = color,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt,
                    State = imported.State,
                    ScreenOrigin = "primary",
                    Tags = imported.Tags,
                    IsPinnedToTop = imported.Pinned,
                });
                created++;
            }
            catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException or FormatException)
            {
                failed++;
            }
        }

        return new ImportResult(created, failed);
    }

    private static DateTimeOffset? Clamp(DateTimeOffset? value, DateTimeOffset now) =>
        value is null ? null : value > now ? now : value;

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();

    private static string ColorFor(ImportHue? hue, ImportColorPolicy policy, List<string> assigned)
    {
        if (hue is { } wanted && Closest(policy, wanted) is { } color) return color;
        return NoteColorAssigner.Assign(policy.Theme, policy.Tone, policy.Rule, policy.FixedColor, assigned);
    }

    private static string? Closest(ImportColorPolicy policy, ImportHue hue)
    {
        var theme = policy.Theme;
        IEnumerable<string> candidates = policy.Tone switch
        {
            NoteTone.Dark => theme.DarkColors.Count > 0 ? theme.DarkColors : theme.LightColors,
            NoteTone.Light => theme.LightColors.Count > 0 ? theme.LightColors : theme.DarkColors,
            _ => theme.LightColors.Concat(theme.DarkColors),
        };

        var degrees = ImportHues.Degrees(hue);
        string? best = null;
        double bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            // A grey theme has no hue to match: the caller falls back to the normal assignment.
            if (!OklchColor.TryFromHex(candidate, out var oklch) || oklch.C < ImportHues.NeutralChroma / 3) continue;
            var distance = ImportHues.HueDistance(degrees, oklch.H);
            if (distance < bestDistance) { bestDistance = distance; best = candidate; }
        }

        return bestDistance <= MaxHueDistance ? best : null;
    }
}
