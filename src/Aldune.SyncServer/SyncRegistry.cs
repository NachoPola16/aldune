using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aldune.SyncServer;

public enum RedeemStatus { Ok, Invalid, Full }

public sealed record RedeemResult(RedeemStatus Status, string? Space = null, string? Token = null);

public sealed record CreatedInvite(string Code, DateTimeOffset ExpiresAt);

public sealed record SpaceInfo(string Name, string? Label, DateTimeOffset CreatedAt);

/// <summary>
/// The spaces that exist because someone redeemed an invitation, and the invitations still waiting (the environment can only hold what is
/// there at start-up). One JSON file in the data folder. Neither a code nor a token is stored: only their SHA-256, so a copy of the file
/// opens nothing. A code works once and expires (at most <see cref="MaxInviteLifetime"/>); a space's token is generated here, never chosen.
/// </summary>
public sealed class SyncRegistry
{
    public static readonly TimeSpan MaxInviteLifetime = TimeSpan.FromDays(30);
    private const int MaxLabelLength = 60;

    private sealed class Data
    {
        public List<SpaceRow> Spaces { get; set; } = [];
        public List<InviteRow> Invites { get; set; } = [];
    }

    private sealed record SpaceRow(string Name, string TokenHash, string? Label, DateTimeOffset CreatedAt);

    private sealed record InviteRow(string CodeHash, string? Label, DateTimeOffset ExpiresAt);

    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly int _maxSpaces;
    private readonly object _gate = new();
    private Data _data;

    public SyncRegistry(string path, TimeProvider clock, int maxSpaces)
    {
        _path = path;
        _clock = clock;
        _maxSpaces = maxSpaces;
        _data = Load(path);
    }

    public IReadOnlyList<SpaceInfo> Spaces
    {
        get
        {
            lock (_gate) return _data.Spaces.Select(s => new SpaceInfo(s.Name, s.Label, s.CreatedAt)).ToArray();
        }
    }

    public CreatedInvite CreateInvite(string? label, TimeSpan lifetime)
    {
        if (lifetime > MaxInviteLifetime) lifetime = MaxInviteLifetime;
        var code = Random(16);
        var expires = _clock.GetUtcNow() + lifetime;
        lock (_gate)
        {
            _data.Invites.RemoveAll(i => i.ExpiresAt <= _clock.GetUtcNow());
            _data.Invites.Add(new InviteRow(Hash(code), Clean(label), expires));
            Save();
        }

        return new CreatedInvite(code, expires);
    }

    public RedeemResult Redeem(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return new RedeemResult(RedeemStatus.Invalid);
        var hash = Hash(code.Trim());
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            // Every row is compared (no early exit) so the time does not say how close a guess was.
            InviteRow? found = null;
            foreach (var invite in _data.Invites)
                if (Same(invite.CodeHash, hash)) found = invite;
            if (found is null || found.ExpiresAt <= now) return new RedeemResult(RedeemStatus.Invalid);
            if (_data.Spaces.Count >= _maxSpaces) return new RedeemResult(RedeemStatus.Full);

            var name = "u-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            var token = Random(32);
            _data.Invites.Remove(found);
            _data.Spaces.Add(new SpaceRow(name, Hash(token), found.Label, now));
            Save();
            return new RedeemResult(RedeemStatus.Ok, name, token);
        }
    }

    /// <summary>The name of the space a bearer token opens, or <c>null</c>.</summary>
    public string? Resolve(string? authorization)
    {
        const string bearer = "Bearer ";
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)) return null;
        var hash = Hash(authorization[bearer.Length..].Trim());
        lock (_gate)
        {
            string? found = null;
            foreach (var space in _data.Spaces)
                if (Same(space.TokenHash, hash)) found = space.Name;
            return found;
        }
    }

    /// <summary>Cuts a space's token. Its files stay on disk: deleting them is a decision for the owner, by hand.</summary>
    public bool Revoke(string name)
    {
        lock (_gate)
        {
            if (_data.Spaces.RemoveAll(s => s.Name == name) == 0) return false;
            Save();
            return true;
        }
    }

    private static string? Clean(string? label)
    {
        var text = label?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length > MaxLabelLength ? text[..MaxLabelLength] : text;
    }

    private static string Random(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool Same(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    private static Data Load(string path)
    {
        if (!File.Exists(path)) return new Data();
        try
        {
            return JsonSerializer.Deserialize<Data>(File.ReadAllText(path)) ?? new Data();
        }
        catch (JsonException)
        {
            // A damaged registry must not start a server that then ignores every space it had: refuse and let the owner look at the file.
            throw new InvalidOperationException($"The registry file {path} is damaged; fix or remove it by hand.");
        }
    }

    // Written to a temporary file and renamed, so a cut in the middle never leaves half a registry.
    private void Save()
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_data));
        File.Move(temp, _path, overwrite: true);
    }
}
