using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aldune.Core;

/// <summary>One sync pass as the person would describe it: when, how long, what moved, and the error if it failed.</summary>
public sealed record SyncAttempt(DateTimeOffset At, TimeSpan Duration, bool Automatic, SyncResult Result);

public sealed class SyncDiagnosticsInfo
{
    public string AppVersion { get; init; } = "";
    public string Platform { get; init; } = "";
    public string Transport { get; init; } = "";
    public string Scope { get; init; } = "";
    /// <summary>Host of the server or WebDAV address, never the full URL (paths and queries can carry secrets).</summary>
    public string? ServerHost { get; init; }
    public int ProfilesTotal { get; init; }
    public int ProfilesEnabled { get; init; }
    public int ActiveNotes { get; init; }
    public int ArchivedNotes { get; init; }
    public int TrashedNotes { get; init; }
    public int OpenConflicts { get; init; }
    public DateTimeOffset? LastSyncAt { get; init; }
    public IReadOnlyList<SyncAttempt> Attempts { get; init; } = [];
}

/// <summary>
/// A plain-text sync report that can be pasted into a message or saved to a file. It holds counts, times and error texts, never note text,
/// keys, tokens or full addresses: error messages are scrubbed (URLs reduced to their host, long key-like strings hidden).
/// </summary>
public static partial class SyncDiagnostics
{
    public const int MaxAttempts = 20;

    public static string Render(SyncDiagnosticsInfo info)
    {
        var text = new StringBuilder();
        text.AppendLine($"Aldune {info.AppVersion} · {info.Platform}");
        text.AppendLine($"Sync: {info.Transport}" + (string.IsNullOrEmpty(info.ServerHost) ? "" : $" ({info.ServerHost})") + $" · scope {info.Scope}");
        text.AppendLine($"Profiles: {info.ProfilesEnabled} enabled of {info.ProfilesTotal}");
        text.AppendLine($"Notes: {info.ActiveNotes} active, {info.ArchivedNotes} archived, {info.TrashedNotes} in trash · open conflicts: {info.OpenConflicts}");
        text.AppendLine("Last successful sync: " + (info.LastSyncAt is { } last ? Stamp(last) : "never"));
        text.AppendLine();

        if (info.Attempts.Count == 0)
        {
            text.AppendLine("There are no sync attempts since Aldune started.");
            return text.ToString();
        }

        text.AppendLine("Recent attempts (newest first):");
        foreach (var attempt in info.Attempts.OrderByDescending(a => a.At).Take(MaxAttempts))
        {
            var result = attempt.Result;
            text.Append(CultureInfo.InvariantCulture, $"- {Stamp(attempt.At)} · {attempt.Duration.TotalSeconds:0.0} s · {(attempt.Automatic ? "automatic" : "manual")} · ");
            text.Append(result.Succeeded ? "OK" : "FAILED");
            text.Append(CultureInfo.InvariantCulture,
                $" · up {result.Uploaded}, down {result.Downloaded}, conflicts {result.ConflictsResolved}, deletions {result.TombstonesApplied}");
            if (result.Error is { } error) text.Append(" · ").Append(Scrub(error));
            text.AppendLine();
        }
        return text.ToString();
    }

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    /// <summary>Reduces every URL in an error text to its host and hides long base64-like runs (keys, tokens).</summary>
    public static string Scrub(string error)
    {
        string withoutUrls = UrlPattern().Replace(error, match =>
        {
            var host = Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) ? uri.Host : "address";
            return host;
        });
        return SecretPattern().Replace(withoutUrls, "<hidden>").Replace('\r', ' ').Replace('\n', ' ');
    }

    [GeneratedRegex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"[A-Za-z0-9_\-+/=]{24,}")]
    private static partial Regex SecretPattern();
}
