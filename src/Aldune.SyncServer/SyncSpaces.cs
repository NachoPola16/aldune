using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aldune.SyncServer;

/// <summary>One person's (or group's) store on this server: its folder name and the token that opens it.</summary>
public sealed record SyncSpace(string Name, string Token);

/// <summary>The space a request's token belongs to; <see cref="Name"/> is <c>null</c> for the original store (the root one).</summary>
public readonly record struct ResolvedSpace(string? Name);

/// <summary>
/// `ALDUNE_SYNC_SPACES=madre=token1,familia=token2`: one server URL, one store per token. Each space lives in
/// <c>/data/spaces/&lt;name&gt;/objects</c>, apart from the others and from the original store (the one the plain
/// `ALDUNE_SYNC_TOKEN(S)` still open), so what a person syncs never shows up in another person's listing. Nothing changes for the clients:
/// they send the same URL and a bearer token, and the token decides the store. Each space is a different sync profile (its own key).
/// </summary>
public static partial class SyncSpaces
{
    private const int MaxNameLength = 32;

    // The name is a folder name: lowercase letters, digits, «-» and «_», never a path.
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$")]
    private static partial Regex NamePattern();

    public static IReadOnlyList<SyncSpace> Parse(string? spec)
    {
        var spaces = new List<SyncSpace>();
        foreach (var part in (spec ?? string.Empty).Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = part.Trim();
            if (entry.Length == 0) continue;

            var equals = entry.IndexOf('=');
            if (equals < 0) throw new FormatException($"A space must be written name=token, and '{Mask(entry)}' has no '='.");
            var name = entry[..equals].Trim();
            var token = entry[(equals + 1)..].Trim();

            if (name.Length == 0 || name.Length > MaxNameLength || !NamePattern().IsMatch(name))
                throw new FormatException($"'{name}' is not a valid space name: 1 to {MaxNameLength} lowercase letters, digits, '-' or '_'.");
            if (token.Length == 0) throw new FormatException($"The space '{name}' has no token.");
            if (spaces.Any(s => s.Name == name)) throw new FormatException($"The space '{name}' is listed twice.");
            if (spaces.Any(s => s.Token == token)) throw new FormatException($"The space '{name}' uses a token that another space already has: they would share a store.");

            spaces.Add(new SyncSpace(name, token));
        }

        return spaces;
    }

    /// <summary>A token that opens two stores would mix them: the original tokens and the spaces' must all be different.</summary>
    public static void EnsureDistinct(IReadOnlyList<string> legacyTokens, IReadOnlyList<SyncSpace> spaces)
    {
        foreach (var space in spaces)
            if (legacyTokens.Contains(space.Token, StringComparer.Ordinal))
                throw new FormatException($"The space '{space.Name}' uses the same token as the original store.");
    }

    /// <summary>The space of the request's bearer token, or <c>null</c> if it opens nothing (a 401).</summary>
    public static ResolvedSpace? Resolve(string? authorization, IReadOnlyList<string> legacyTokens, IReadOnlyList<SyncSpace> spaces)
    {
        // Every candidate is compared (no early exit on the first hit across kinds) so the time does not say which store a token belongs to.
        ResolvedSpace? found = null;
        if (Aldune.Core.SyncTokenSet.Matches(authorization, legacyTokens)) found = new ResolvedSpace(null);
        foreach (var space in spaces)
            if (Aldune.Core.SyncTokenSet.Matches(authorization, new[] { space.Token })) found = new ResolvedSpace(space.Name);
        return found;
    }

    /// <summary>The folder with a space's objects (the original store keeps <c>/data/objects</c>, as always).</summary>
    public static string ObjectsPath(string dataPath, string? space) =>
        space is null ? Path.Combine(dataPath, "objects") : Path.Combine(dataPath, "spaces", space, "objects");

    // Never print a token in an error: an entry without '=' may be just a token.
    private static string Mask(string entry) => entry.Length <= 4 ? "…" : entry[..2] + "…";
}
