using Aldune.Core;

namespace Aldune.Resources;

/// <summary>
/// What each version added, in the user's language, for the window shown once after an update (and from Settings → About).
///
/// To add a release: write its strings in <see cref="Strings"/> (all eight languages), add one line here, and bump the version in the
/// csproj. Only things a person will notice belong here: two or three short lines per version, not the whole changelog (that lives in
/// the GitHub release). Which versions to list after an update is decided by <see cref="WhatsNewPlan"/>.
/// </summary>
internal static class WhatsNew
{
    private static readonly (Version Version, Func<string[]> Items)[] Catalog =
    [
        (new Version(2, 4, 0), () => [Strings.WhatsNew240Links, Strings.WhatsNew240Close]),
        (new Version(2, 5, 0), () => [Strings.WhatsNew250Tags]),
        (new Version(2, 6, 0), () => [Strings.WhatsNew260Label]),
        (new Version(2, 7, 0), () => [Strings.WhatsNew270Window]),
    ];

    /// <summary>The versions that have an entry.</summary>
    internal static IEnumerable<Version> Versions => Catalog.Select(entry => entry.Version);

    /// <summary>The window text for <paramref name="versions"/> (newest first): a heading per version and a bullet per item.</summary>
    internal static string Compose(IEnumerable<Version> versions)
    {
        var lines = new List<string>();
        foreach (var version in versions)
        {
            var entry = Catalog.FirstOrDefault(c => c.Version == version);
            if (entry.Items is null) continue;
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add($"{Strings.AppName} {version.ToString(3)}");
            lines.AddRange(entry.Items().Select(item => "•  " + item));
        }

        lines.Add(string.Empty);
        lines.Add($"{Strings.WhatsNewMore} https://github.com/NachoPola16/aldune/releases");
        return string.Join(Environment.NewLine, lines);
    }
}
