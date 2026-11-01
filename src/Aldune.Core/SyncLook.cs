using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aldune.Core;

/// <summary>What a device should do with the look in a sync pass (docs/specs/2026-11-01-sync-look-design.md).</summary>
public enum LookAction { None, Push, Pull }

/// <summary>A look document read from the store: when it was written, by which device, and the look itself.</summary>
public sealed record LookDocument(DateTimeOffset Stamp, string Device, JsonElement Look);

/// <summary>
/// The look (appearance and colours) as a small document that can follow a person across devices. It reuses the fields of the configuration file
/// (<see cref="ConfigFields"/>, section Appearance) so there is one list of what the look is, and leaves out the custom themes (Windows only).
/// It travels sealed with the profile's sync key, in a blob next to the notes and never among them: a version that does not know it never reads it.
/// </summary>
public static class SyncLook
{
    public const int DocumentVersion = 1;

    // Custom themes are a Windows-only list that Android cannot show; everything else in the Appearance section is shared.
    private static IEnumerable<ConfigField> Fields => ConfigFields.All.Where(f => f.Section == ConfigSections.Appearance && f.Id != "customThemes");

    /// <summary>The look of <paramref name="settings"/> as JSON, fields in a fixed order (so equal looks give equal text).</summary>
    public static JsonObject Capture(AppSettings settings)
    {
        var node = new JsonObject();
        foreach (var field in Fields) node[field.Id] = JsonSerializer.SerializeToNode(field.Get(settings));
        return node;
    }

    /// <summary>A hash of the look: the device compares it with the one it last synced to know the person changed something here.</summary>
    public static string Hash(AppSettings settings) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Capture(settings).ToJsonString())));

    /// <summary>
    /// Applies a look to <paramref name="settings"/>. A field that is missing or that does not validate is left alone, and unknown fields are ignored,
    /// so a later version can add some. Returns whether anything changed.
    /// </summary>
    public static bool Apply(AppSettings settings, JsonElement look)
    {
        if (look.ValueKind != JsonValueKind.Object) return false;
        var before = Hash(settings);
        foreach (var field in Fields)
        {
            if (!look.TryGetProperty(field.Id, out var value)) continue;
            var (ok, parsed) = field.Read(value);
            if (ok) field.Set(settings, parsed);
        }

        return Hash(settings) != before;
    }

    /// <summary>The rule of the spec: a local change is pushed; otherwise a newer remote document is pulled; otherwise nothing.</summary>
    public static LookAction Decide(string localHash, string? lastHash, DateTimeOffset? lastStamp, DateTimeOffset? remoteStamp)
    {
        if (lastHash is null || !string.Equals(localHash, lastHash, StringComparison.Ordinal))
        {
            // First time (nothing remembered): if a remote look exists the other device was first, so take it; otherwise send ours.
            if (lastHash is null && remoteStamp is not null) return LookAction.Pull;
            return LookAction.Push;
        }

        return remoteStamp is { } remote && (lastStamp is null || remote > lastStamp) ? LookAction.Pull : LookAction.None;
    }

    /// <summary>Seals the document with the profile's key: nothing in the blob is readable without it, not even that it is a look.</summary>
    public static byte[] Seal(byte[] key, DateTimeOffset stamp, string device, JsonObject look)
    {
        var plain = new JsonObject
        {
            ["v"] = DocumentVersion,
            ["stamp"] = stamp.ToString("O"),
            ["device"] = device,
            ["look"] = look.DeepClone(),
        }.ToJsonString();
        using var cipher = new ContentCipher(key);
        var encrypted = cipher.Encrypt(plain);
        return JsonSerializer.SerializeToUtf8Bytes(new Sealed(
            Convert.ToBase64String(encrypted.CipherText), Convert.ToBase64String(encrypted.Nonce), Convert.ToBase64String(encrypted.Tag)));
    }

    /// <summary>Opens a sealed document, or <c>null</c> if it is not one of ours, it does not open with <paramref name="key"/> or it is of a later version.</summary>
    public static LookDocument? Open(byte[] key, byte[] blob)
    {
        try
        {
            var sealedBlob = JsonSerializer.Deserialize<Sealed>(blob);
            if (sealedBlob is null) return null;
            using var cipher = new ContentCipher(key);
            var plain = cipher.Decrypt(new EncryptedContent(
                Convert.FromBase64String(sealedBlob.C), Convert.FromBase64String(sealedBlob.N), Convert.FromBase64String(sealedBlob.T)));
            using var doc = JsonDocument.Parse(plain);
            var root = doc.RootElement;
            if (!root.TryGetProperty("v", out var version) || version.GetInt32() > DocumentVersion) return null;
            var stamp = DateTimeOffset.Parse(root.GetProperty("stamp").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var device = root.TryGetProperty("device", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            return new LookDocument(stamp, device, root.GetProperty("look").Clone());
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private sealed record Sealed(string C, string N, string T);
}
