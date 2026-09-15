using System.Text.Json;
using System.Security.Cryptography;

namespace Aldune.Core;

/// <summary>
/// Sobre portable de sincronización. Los metadatos son deliberadamente pequeños y permiten que un
/// servidor enumere versiones sin conocer la clave; el contenido de la nota viaja cifrado dentro del
/// sobre. El formato no contiene tipos de SQLite ni APIs de Windows para que puedan leerlo clientes
/// futuros de Android, iOS, macOS y Linux.
/// </summary>
public sealed class SyncEnvelope
{
    public int Format { get; set; } = SyncCompatibility.CurrentFormat;
    public Guid NoteId { get; set; }
    public bool Tombstone { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? CipherText { get; set; }
    public string? Nonce { get; set; }
    public string? Tag { get; set; }
}

public sealed record SyncRemoteObject(SyncEnvelope Envelope, byte[] Bytes);

public sealed record SyncTombstone(Guid NoteId, DateTimeOffset DeletedAt, string DeviceId);

/// <summary>Ventana de formatos que una versión de Aldune sabe leer.</summary>
public static class SyncCompatibility
{
    // Format 2 added tags. Format 3 adds protected-note ciphertext and metadata. Older clients
    // reject it instead of silently replacing a protected note with an unprotected copy.
    public const int CurrentFormat = 3;
    public const int MinimumSupportedFormat = 1;

    public static bool IsSupported(int format) =>
        format >= MinimumSupportedFormat && format <= CurrentFormat;

    public static string UnsupportedFormatMessage(int format) =>
        format > CurrentFormat
            ? $"This sync data requires a newer Aldune version (format {format})."
            : $"This sync data is too old for this Aldune version (format {format}).";
}

/// <summary>
/// Authentication tokens accepted by the self-hosted server. A comma-separated token list lets an
/// administrator overlap old and new credentials during rotation without exposing a management
/// endpoint that could itself become an attack surface.
/// </summary>
public static class SyncTokenSet
{
    public static IReadOnlyList<string> Parse(string? rotatedTokens, string? legacyToken)
    {
        var source = string.IsNullOrWhiteSpace(rotatedTokens) ? legacyToken : rotatedTokens;
        return (source ?? string.Empty)
            .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool Matches(string? authorization, IReadOnlyList<string> tokens)
    {
        const string bearer = "Bearer ";
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            return false;

        var candidate = authorization[bearer.Length..].Trim();
        if (candidate.Length == 0) return false;

        var candidateBytes = System.Text.Encoding.UTF8.GetBytes(candidate);
        try
        {
            foreach (var token in tokens)
            {
                var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
                try
                {
                    if (System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                            candidateBytes, tokenBytes))
                        return true;
                }
                finally
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(tokenBytes);
                }
            }

            return false;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(candidateBytes);
        }
    }
}

public static class SyncVersion
{
    /// <summary>Orden total y estable: fecha UTC y, en empate, identificador de dispositivo.</summary>
    public static int Compare(SyncEnvelope left, SyncEnvelope right)
    {
        int time = left.UpdatedAt.CompareTo(right.UpdatedAt);
        return time != 0 ? time : string.CompareOrdinal(left.DeviceId, right.DeviceId);
    }
}

public sealed record SyncResult(
    int Uploaded,
    int Downloaded,
    int ConflictsResolved,
    int TombstonesApplied,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

public sealed record SyncConflictVersion(
    DateTimeOffset UpdatedAt,
    string DeviceId,
    bool Tombstone,
    Note? Note);

public sealed record SyncConflict(
    Guid Id,
    Guid NoteId,
    DateTimeOffset OccurredAt,
    SyncConflictVersion Winner,
    SyncConflictVersion Losing);

public static class SyncKeyFormat
{
    public static string Encode(byte[] key) => Convert.ToBase64String(key)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static byte[] Decode(string value)
    {
        var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        var key = Convert.FromBase64String(normalized);
        if (key.Length != 32) throw new FormatException("A sync key must contain 32 bytes.");
        return key;
    }
}

public sealed record SyncShareCodeData(
    byte[] Key,
    SyncScopeKind Scope,
    IReadOnlyList<Guid> NoteIds,
    string? ProfileName = null,
    SyncTransportKind? Transport = null,
    string? ServerUrl = null);

/// <summary>
/// Código opcional para compartir un perfil. A diferencia del código de dispositivo, incluye el
/// ámbito local y los identificadores de las notas seleccionadas. La versión actual puede añadir
/// el nombre del vínculo y la URL pública del servidor, pero nunca incluye el token ni el contenido
/// de las notas.
/// </summary>
public static class SyncShareCodeCodec
{
    // El prefijo vigente lo declara BrandIdentity: así el nombre de marca vive en un único sitio
    // y rebautizar la aplicación no obliga a tocar este códec (solo hay que añadir el prefijo
    // antiguo a BrandIdentity.LegacySyncProfileCodePrefixes).
    private const string Prefix = BrandIdentity.SyncProfileCodePrefix;

    /// <summary>Versión del documento que emite <see cref="Encode"/> (sufijo del prefijo vigente).</summary>
    private const int CurrentDocumentVersion = 2;

    private const int MaxNoteIds = 10_000;
    private const int MaxProfileNameLength = 120;
    private const int MaxServerUrlLength = 2_048;

    public static bool IsShareCode(string value) => TrySplitPrefix(value.Trim(), out _, out _);

    /// <summary>
    /// Separa un código de perfil en el prefijo (marca + versión del documento) y el resto.
    /// Acepta el prefijo vigente y los heredados: los códigos generados antes del cambio de marca
    /// tienen que seguir importándose.
    /// </summary>
    private static bool TrySplitPrefix(string value, out string payload, out int documentVersion)
    {
        payload = string.Empty;
        documentVersion = 0;

        if (value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            payload = value[Prefix.Length..];
            documentVersion = CurrentDocumentVersion;
            return true;
        }

        foreach (var legacyPrefix in BrandIdentity.LegacySyncProfileCodePrefixes)
        {
            if (!value.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            payload = value[legacyPrefix.Length..];
            // El propio prefijo heredado lleva la versión del formato ("…-v1:" / "…-v2:"), así que
            // no hace falta mantener aparte la correspondencia prefijo → versión.
            documentVersion = legacyPrefix.EndsWith("-v1:", StringComparison.Ordinal) ? 1 : CurrentDocumentVersion;
            return true;
        }

        return false;
    }

    public static string Encode(
        byte[] key,
        SyncScopeKind scope,
        IEnumerable<Guid> noteIds,
        string? profileName = null,
        SyncTransportKind? transport = null,
        string? serverUrl = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        var document = new SyncShareCodeDocument
        {
            Version = CurrentDocumentVersion,
            Key = SyncKeyFormat.Encode(key),
            Scope = scope,
            NoteIds = noteIds.Distinct().Take(MaxNoteIds).ToArray(),
            ProfileName = NormalizeProfileName(profileName),
            Transport = transport,
            ServerUrl = NormalizeServerUrl(serverUrl)
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(document);
        return Prefix + ToBase64Url(json);
    }

    public static SyncShareCodeData Decode(string value)
    {
        var trimmed = value.Trim();
        if (!TrySplitPrefix(trimmed, out var payload, out var documentVersion))
            throw new FormatException("That is not an Aldune profile code.");

        try
        {
            var document = JsonSerializer.Deserialize<SyncShareCodeDocument>(FromBase64Url(payload))
                ?? throw new FormatException("The profile code is empty.");
            if (document.Version != documentVersion ||
                !Enum.IsDefined(document.Scope) || string.IsNullOrWhiteSpace(document.Key))
                throw new FormatException("The profile code version is not supported.");
            var noteIds = document.NoteIds ?? Array.Empty<Guid>();
            if (noteIds.Length > MaxNoteIds)
                throw new FormatException("The profile code contains too many notes.");

            if (document.ProfileName is { Length: > MaxProfileNameLength } ||
                document.ServerUrl is { Length: > MaxServerUrlLength } ||
                (document.Transport is not null && !Enum.IsDefined(document.Transport.Value)))
                throw new FormatException("The profile code contains invalid connection details.");

            var serverUrl = NormalizeServerUrl(document.ServerUrl);
            if (document.Transport is SyncTransportKind.Server or SyncTransportKind.WebDav && serverUrl is null)
                throw new FormatException("The profile code does not contain a valid server URL.");

            return new SyncShareCodeData(
                SyncKeyFormat.Decode(document.Key),
                document.Scope,
                noteIds.Distinct().ToArray(),
                NormalizeProfileName(document.ProfileName),
                document.Transport,
                serverUrl);
        }
        catch (JsonException ex)
        {
            throw new FormatException("The profile code is not valid.", ex);
        }
    }

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        return Convert.FromBase64String(normalized);
    }

    private static string? NormalizeProfileName(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, MaxProfileNameLength)];
    }

    private static string? NormalizeServerUrl(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > MaxServerUrlLength ||
            !Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme is not "http" and not "https"))
            throw new FormatException("The profile code contains an invalid server URL.");
        return uri.ToString();
    }

    private sealed class SyncShareCodeDocument
    {
        public int Version { get; set; }
        public string Key { get; set; } = string.Empty;
        public SyncScopeKind Scope { get; set; }
        public Guid[] NoteIds { get; set; } = Array.Empty<Guid>();
        public string? ProfileName { get; set; }
        public SyncTransportKind? Transport { get; set; }
        public string? ServerUrl { get; set; }
    }
}

public static class SyncEnvelopeCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static SyncEnvelope CreateNote(Note note, byte[] key, string deviceId)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SyncNotePayload
        {
            Id = note.Id,
            Text = note.Text,
            Color = note.Color,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
            State = note.State,
            ScreenOrigin = note.ScreenOrigin,
            Tags = note.Tags.ToArray(),
            IsProtected = note.IsProtected,
            ProtectedContent = note.ProtectedContent
        }, JsonOptions);

        try
        {
            using var cipher = new ContentCipher(key);
            var encrypted = cipher.Encrypt(Convert.ToBase64String(payload));
            return new SyncEnvelope
            {
                NoteId = note.Id,
                Tombstone = false,
                UpdatedAt = note.UpdatedAt,
                DeviceId = deviceId,
                CipherText = Convert.ToBase64String(encrypted.CipherText),
                Nonce = Convert.ToBase64String(encrypted.Nonce),
                Tag = Convert.ToBase64String(encrypted.Tag)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public static SyncEnvelope CreateTombstone(SyncTombstone tombstone, string deviceId) => new()
    {
        NoteId = tombstone.NoteId,
        Tombstone = true,
        UpdatedAt = tombstone.DeletedAt,
        DeviceId = deviceId,
    };

    public static byte[] Serialize(SyncEnvelope envelope) => JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);

    public static SyncEnvelope Deserialize(byte[] bytes)
    {
        var envelope = JsonSerializer.Deserialize<SyncEnvelope>(bytes, JsonOptions)
            ?? throw new FormatException("The sync object is empty.");
        Validate(envelope);
        return envelope;
    }

    public static Note DecryptNote(SyncEnvelope envelope, byte[] key)
    {
        Validate(envelope);
        if (envelope.Tombstone || envelope.CipherText is null || envelope.Nonce is null || envelope.Tag is null)
            throw new FormatException("The sync object is not a note.");

        using var cipher = new ContentCipher(key);
        var plainBase64 = cipher.Decrypt(new EncryptedContent(
            Convert.FromBase64String(envelope.CipherText),
            Convert.FromBase64String(envelope.Nonce),
            Convert.FromBase64String(envelope.Tag)));
        var payload = JsonSerializer.Deserialize<SyncNotePayload>(
            Convert.FromBase64String(plainBase64), JsonOptions)
            ?? throw new FormatException("The encrypted sync note is empty.");

        if (payload.Id != envelope.NoteId || payload.UpdatedAt != envelope.UpdatedAt)
            throw new FormatException("The sync note metadata does not match its encrypted content.");
        if (payload.IsProtected && payload.ProtectedContent is null)
            throw new FormatException("The protected sync note is missing its encrypted content.");

        return new Note
        {
            Id = payload.Id,
            Text = payload.Text,
            Color = payload.Color,
            CreatedAt = payload.CreatedAt,
            UpdatedAt = payload.UpdatedAt,
            State = payload.State,
            ScreenOrigin = payload.ScreenOrigin,
            Tags = payload.Tags ?? Array.Empty<string>(),
            IsProtected = payload.IsProtected,
            ProtectedContent = payload.ProtectedContent,
            IsUnlocked = !payload.IsProtected
        };
    }

    private static void Validate(SyncEnvelope envelope)
    {
        if (!SyncCompatibility.IsSupported(envelope.Format))
            throw new FormatException(SyncCompatibility.UnsupportedFormatMessage(envelope.Format));

        if (envelope.NoteId == Guid.Empty ||
            string.IsNullOrWhiteSpace(envelope.DeviceId) || envelope.UpdatedAt == default)
            throw new FormatException("The sync object is incomplete.");

        if (!envelope.Tombstone &&
            (envelope.CipherText is null || envelope.Nonce is null || envelope.Tag is null))
            throw new FormatException("A note sync object is missing encrypted content.");
    }

    private sealed class SyncNotePayload
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public NoteState State { get; set; }
        public string ScreenOrigin { get; set; } = string.Empty;
        public string[]? Tags { get; set; }
        public bool IsProtected { get; set; }
        public ProtectedNoteContent? ProtectedContent { get; set; }
    }
}

public interface ISyncTransport
{
    IReadOnlyList<SyncRemoteObject> ReadAll();
    void Write(SyncEnvelope envelope);
    void Delete(Guid noteId);
}
