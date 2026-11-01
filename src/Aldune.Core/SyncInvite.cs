using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Aldune.Core;

/// <summary>
/// The text an owner hands to a guest: the server address and a one-use code, in a single string so the guest pastes one thing. It carries
/// no token and no key (the code is useless after one redemption and expires). The server side is <c>SyncRegistry</c>.
/// </summary>
public static class SyncInvite
{
    public const string Prefix = "aldune-invite:";
    private const int MaxLength = 4_096;

    public static string Format(string serverUrl, string code)
    {
        var json = JsonSerializer.Serialize(new Document(serverUrl.Trim(), code.Trim()));
        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Reads what <see cref="Format"/> wrote (also when a chat added spaces or line breaks). <c>false</c> for anything else.</summary>
    public static bool TryParse(string? text, out string serverUrl, out string code)
    {
        serverUrl = code = string.Empty;
        var clean = new string((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (clean.Length is 0 or > MaxLength || !clean.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var payload = clean[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var document = JsonSerializer.Deserialize<Document>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (document is null || string.IsNullOrWhiteSpace(document.U) || string.IsNullOrWhiteSpace(document.C)) return false;
            // Only http(s): an invitation never points at a file or another scheme.
            if (!Uri.TryCreate(document.U, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;
            serverUrl = document.U.Trim();
            code = document.C.Trim();
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private sealed record Document(string U, string C);
}

/// <summary>The two calls of the invitation protocol (docs/SYNC.md → «Invitations»). Throws <see cref="InviteException"/> with a plain reason.</summary>
public static class SyncInviteClient
{
    /// <summary>Owner side: asks the server for a one-use code (needs the admin token) and returns the text to hand over.</summary>
    public static async Task<string> CreateAsync(HttpClient http, string serverUrl, string adminToken, string? label, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(serverUrl, "admin/v1/invites"))
        {
            Content = JsonContent.Create(new { label })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.Trim());
        using var response = await Send(http, request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new InviteException(InviteFailure.NotAllowed);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new InviteException(InviteFailure.NotSupported);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests) throw new InviteException(InviteFailure.TooManyAttempts);
        if (!response.IsSuccessStatusCode) throw new InviteException(InviteFailure.Server);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return SyncInvite.Format(serverUrl, body.GetProperty("code").GetString()!);
    }

    /// <summary>Guest side: trades the code for the token of a new space. The sync key is NOT part of this: the caller generates it locally.</summary>
    public static async Task<string> RedeemAsync(HttpClient http, string serverUrl, string code, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(serverUrl, "invites/v1/redeem"))
        {
            Content = JsonContent.Create(new { code })
        };
        using var response = await Send(http, request, ct);
        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.BadRequest: throw new InviteException(InviteFailure.InvalidCode);
            case System.Net.HttpStatusCode.NotFound: throw new InviteException(InviteFailure.NotSupported);
            case System.Net.HttpStatusCode.Conflict: throw new InviteException(InviteFailure.ServerFull);
            case System.Net.HttpStatusCode.TooManyRequests: throw new InviteException(InviteFailure.TooManyAttempts);
        }

        if (!response.IsSuccessStatusCode) throw new InviteException(InviteFailure.Server);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("token").GetString()!;
    }

    /// <summary>Owner side: the spaces created from invitations (the ones the admin token can revoke). Env-configured spaces are not listed.</summary>
    public static async Task<IReadOnlyList<SyncSpaceInfo>> ListSpacesAsync(HttpClient http, string serverUrl, string adminToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(serverUrl, "admin/v1/spaces"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.Trim());
        using var response = await Send(http, request, ct);
        ThrowForAdminStatus(response);
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return body.EnumerateArray().Select(item => new SyncSpaceInfo(
                item.GetProperty("name").GetString()!,
                item.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String ? label.GetString() : null,
                item.GetProperty("createdAt").GetDateTimeOffset())).OrderByDescending(space => space.CreatedAt).ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InviteException(InviteFailure.Server);
        }
    }

    /// <summary>Owner side: cuts the token of a space (its files stay on the server). <c>false</c> when it no longer exists.</summary>
    public static async Task<bool> RevokeSpaceAsync(HttpClient http, string serverUrl, string adminToken, string name, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Endpoint(serverUrl, "admin/v1/spaces/" + Uri.EscapeDataString(name)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.Trim());
        using var response = await Send(http, request, ct);
        // A 404 here is ambiguous with «server without admin routes»: the routes exist only with the admin token, so a wrong
        // token is 401 first and a 404 means the space was already gone.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        ThrowForAdminStatus(response);
        return true;
    }

    private static void ThrowForAdminStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new InviteException(InviteFailure.NotAllowed);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new InviteException(InviteFailure.NotSupported);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests) throw new InviteException(InviteFailure.TooManyAttempts);
        if (!response.IsSuccessStatusCode) throw new InviteException(InviteFailure.Server);
    }

    private static Uri Endpoint(string serverUrl, string path)
    {
        var baseText = serverUrl.Trim();
        if (!baseText.EndsWith('/')) baseText += "/";
        return new Uri(new Uri(baseText), path);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InviteException(InviteFailure.Offline);
        }
    }
}

/// <summary>A space created from an invitation, as the owner sees it (the label is only ever shown to the owner).</summary>
public sealed record SyncSpaceInfo(string Name, string? Label, DateTimeOffset CreatedAt);

public enum InviteFailure { Offline, InvalidCode, NotAllowed, NotSupported, ServerFull, TooManyAttempts, Server }

public sealed class InviteException(InviteFailure failure) : Exception($"Invitation failed: {failure}")
{
    public InviteFailure Failure { get; } = failure;
}
