using System.Globalization;
using System.Text.Json;

namespace Aldune.Core;

public sealed record ReleaseUpdate(Version Version, Uri DownloadUri);

/// <summary>Consulta solo versiones estables. El llamador posee el HttpClient y decide el timeout.</summary>
public sealed class ReleaseUpdateChecker(HttpClient client)
{
    public const string LatestReleaseApi = "https://api.github.com/repos/NachoPola16/aldune/releases/latest";
    private const string ReleasePage = "https://github.com/NachoPola16/aldune/releases/tag/";

    public async Task<ReleaseUpdate?> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (!TryParseVersion(currentVersion, out var current))
            throw new ArgumentException("Invalid current version.", nameof(currentVersion));

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.ParseAdd("Aldune/" + current.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("draft", out var draft) ||
            !root.TryGetProperty("prerelease", out var prerelease) ||
            draft.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            prerelease.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Invalid release metadata.");
        if (draft.GetBoolean() || prerelease.GetBoolean()) return null;
        if (!root.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing release tag.");
        var tag = tagElement.GetString();
        if (!TryParseVersion(tag, out var latest))
            throw new InvalidDataException("Invalid stable release tag.");
        if (latest <= current) return null;

        // Nunca abrir html_url ni assets del JSON: solo nuestra página HTTPS con un tag numérico validado.
        return new ReleaseUpdate(latest, new Uri(ReleasePage + tag));
    }

    /// <summary>Normaliza componentes omitidos a cero; nunca acepta sufijos beta/rc ni rutas.</summary>
    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrEmpty(text) || text.Length > 48) return false;
        if (text[0] is 'v' or 'V') text = text[1..];
        var parts = text.Split('.');
        if (parts.Length is < 2 or > 4) return false;
        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || parts[i].Any(c => c is < '0' or > '9') ||
                !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }
        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }
}
