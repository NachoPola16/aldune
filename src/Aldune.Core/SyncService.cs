using System.Security.Cryptography;

namespace Aldune.Core;

/// <summary>
/// Motor de sincronización independiente del transporte. La carpeta local y el servidor HTTP
/// presentan la misma lista de sobres; por eso los clientes futuros pueden implementar otro
/// transporte sin duplicar la resolución de conflictos ni conocer la base de datos de Windows.
/// </summary>
public sealed class SyncService
{
    private readonly NotesRepository _repository;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly object _syncGate = new();

    public SyncService(NotesRepository repository, AppSettings settings, SettingsService settingsService)
    {
        _repository = repository;
        _settings = settings;
        _settingsService = settingsService;
    }

    public string GetOrCreateSyncCode()
    {
        var key = GetOrCreateKey();
        try
        {
            return SyncKeyFormat.Encode(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public string GetOrCreateShareCode()
    {
        var key = GetOrCreateKey();
        try
        {
            var profile = SyncProfileStore.GetActive(_settings);
            return SyncShareCodeCodec.Encode(
                key,
                _settings.SyncScope,
                _settings.SyncNoteIds,
                profile.Name,
                profile.SyncTransport,
                profile.SyncTransport != SyncTransportKind.Folder ? profile.SyncServerUrl : null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public void ImportSyncCode(string code)
    {
        if (SyncShareCodeCodec.IsShareCode(code))
        {
            ImportShareCode(code);
            return;
        }

        var key = SyncKeyFormat.Decode(code);
        try
        {
            _settings.WrappedSyncKey = DatabaseKeyProvider.Wrap(key);
            _settings.WrappedPendingSyncKey = null;
            _settings.SyncKeyRotationPending = false;
            EnsureDeviceId();
            _settingsService.Save(_settings);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private void ImportShareCode(string code)
    {
        var share = SyncShareCodeCodec.Decode(code);
        try
        {
            _settings.WrappedSyncKey = DatabaseKeyProvider.Wrap(share.Key);
            _settings.WrappedPendingSyncKey = null;
            _settings.SyncKeyRotationPending = false;
            _settings.SyncScope = share.Scope;
            _settings.SyncNoteIds = share.NoteIds.ToList();
            if (share.Transport is { } transport)
            {
                _settings.SyncTransport = transport;
                if (transport != SyncTransportKind.Folder && share.ServerUrl is not null)
                    _settings.SyncServerUrl = share.ServerUrl;
            }
            EnsureDeviceId();
            _settingsService.Save(_settings);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(share.Key);
        }
    }

    public string GetServerToken()
    {
        if (_settings.WrappedSyncServerToken is null) return string.Empty;
        var bytes = DatabaseKeyProvider.Unwrap(_settings.WrappedSyncServerToken);
        try
        {
            var rawToken = System.Text.Encoding.UTF8.GetString(bytes);
            var token = NormalizeServerToken(rawToken);
            if (token != rawToken) SetServerToken(token);
            return token;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void SetServerToken(string token)
    {
        token = NormalizeServerToken(token);
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        try
        {
            _settings.WrappedSyncServerToken = string.IsNullOrEmpty(token)
                ? null
                : DatabaseKeyProvider.Wrap(bytes);
            _settingsService.Save(_settings);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public string GetWebDavPassword()
    {
        if (_settings.WrappedSyncWebDavPassword is null) return string.Empty;
        var bytes = DatabaseKeyProvider.Unwrap(_settings.WrappedSyncWebDavPassword);
        try
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void SetWebDavPassword(string password)
    {
        password = password.Trim();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        try
        {
            _settings.WrappedSyncWebDavPassword = string.IsNullOrEmpty(password)
                ? null
                : DatabaseKeyProvider.Wrap(bytes);
            _settingsService.Save(_settings);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Acepta tanto el valor puro como una línea copiada directamente desde .env.</summary>
    public static string NormalizeServerToken(string token)
    {
        var value = token.Trim();
        var tokenPrefix = BrandIdentity.SyncTokenEnvVar + "=";
        if (value.StartsWith(tokenPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[tokenPrefix.Length..].Trim();

        int literalPort = value.IndexOf("\\n" + BrandIdentity.SyncPortEnvVar + "=", StringComparison.OrdinalIgnoreCase);
        if (literalPort >= 0) value = value[..literalPort];

        int actualPort = value.IndexOf('\n');
        if (actualPort >= 0) value = value[..actualPort];
        return value.Trim();
    }

    public IReadOnlyList<SyncConflict> GetConflicts() => _repository.GetSyncConflicts();

    public bool DismissConflict(Guid conflictId)
    {
        if (!_repository.GetSyncConflicts().Any(conflict => conflict.Id == conflictId)) return false;
        _repository.DeleteSyncConflict(conflictId);
        return true;
    }

    public bool RestoreConflict(Guid conflictId)
    {
        var conflict = _repository.GetSyncConflicts().FirstOrDefault(item => item.Id == conflictId);
        if (conflict is null) return false;

        if (conflict.Losing.Tombstone)
        {
            _repository.ApplySyncTombstone(new SyncTombstone(
                conflict.NoteId, DateTimeOffset.UtcNow, EnsureDeviceId()));
        }
        else if (conflict.Losing.Note is not null)
        {
            var note = conflict.Losing.Note;
            note.UpdatedAt = DateTimeOffset.UtcNow;
            _repository.ApplySyncNote(note);
        }
        else
        {
            return false;
        }

        _repository.DeleteSyncConflict(conflictId);
        return true;
    }

    public SyncResult Synchronize()
    {
        lock (_syncGate) return SynchronizeCore();
    }

    /// <summary>
    /// Cambia la clave del vínculo para invalidar los códigos que ya se compartieron. La operación
    /// es reanudable: la clave nueva queda marcada como pendiente antes de tocar el almacén remoto.
    /// </summary>
    public bool RevokeSharedAccess(out string? error)
    {
        lock (_syncGate)
        {
            error = null;
            if (!_settings.SyncEnabled)
            {
                error = "Sync is disabled.";
                return false;
            }

            var beforeRotation = SynchronizeCore();
            if (!beforeRotation.Succeeded)
            {
                error = beforeRotation.Error;
                return false;
            }

            var nextKey = DatabaseKeyProvider.GenerateKey();
            try
            {
                _settings.WrappedPendingSyncKey = DatabaseKeyProvider.Wrap(nextKey);
                _settings.SyncKeyRotationPending = true;
                _settingsService.Save(_settings);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(nextKey);
            }

            return CompletePendingKeyRotation(out error);
        }
    }

    private SyncResult SynchronizeCore()
    {
        try
        {
            if (!_settings.SyncEnabled) return new(0, 0, 0, 0, "Sync is disabled.");

            if (_settings.SyncKeyRotationPending && !CompletePendingKeyRotation(out var rotationError))
                return new(0, 0, 0, 0, rotationError ?? "The sync key rotation is still pending.");

            var key = GetExistingKey();
            if (key is null) return new(0, 0, 0, 0, "Create or import a sync code first.");

            try
            {
                var transport = CreateTransport();
                var remote = transport.ReadAll()
                    .GroupBy(item => item.Envelope.NoteId)
                    .ToDictionary(group => group.Key, group => group
                        .OrderByDescending(item => item.Envelope, Comparer<SyncEnvelope>.Create(SyncVersion.Compare))
                        .First());

                var scopedIds = _settings.SyncScope == SyncScopeKind.SelectedNotes
                    ? _settings.SyncNoteIds.ToHashSet()
                    : null;
                if (scopedIds is not null)
                    remote = remote.Where(pair => scopedIds.Contains(pair.Key))
                        .ToDictionary(pair => pair.Key, pair => pair.Value);

                var deviceId = EnsureDeviceId();
                var localNotes = _repository.GetAllForSync()
                    .Where(note => scopedIds is null || scopedIds.Contains(note.Id))
                    .ToDictionary(note => note.Id);
                var localTombstones = _repository.GetSyncTombstones()
                    .Where(tombstone => scopedIds is null || scopedIds.Contains(tombstone.NoteId))
                    .ToDictionary(item => item.NoteId);
                var local = new Dictionary<Guid, SyncEnvelope>();
                foreach (var note in localNotes.Values)
                    local[note.Id] = SyncEnvelopeCodec.CreateNote(note, key, deviceId);
                foreach (var tombstone in localTombstones.Values)
                    local[tombstone.NoteId] = SyncEnvelopeCodec.CreateTombstone(tombstone, deviceId);

                int uploaded = 0;
                int downloaded = 0;
                int conflicts = 0;
                int tombstones = 0;

                foreach (var id in local.Keys.Union(remote.Keys).ToList())
                {
                    bool hasLocal = local.TryGetValue(id, out var localEnvelope);
                    bool hasRemote = remote.TryGetValue(id, out var remoteObject);

                    if (!hasLocal)
                    {
                        ApplyRemote(remoteObject!, key, ref downloaded, ref tombstones);
                        continue;
                    }

                    if (!hasRemote)
                    {
                        transport.Write(localEnvelope!);
                        uploaded++;
                        continue;
                    }

                    conflicts++;
                    int comparison = SyncVersion.Compare(localEnvelope!, remoteObject!.Envelope);
                    if (comparison != 0)
                    {
                        var localVersion = ToConflictVersion(localEnvelope!, localNotes, key);
                        var remoteVersion = ToConflictVersion(remoteObject.Envelope, null, key);
                        var winner = comparison > 0 ? localVersion : remoteVersion;
                        var losing = comparison > 0 ? remoteVersion : localVersion;
                        _repository.SaveSyncConflict(new SyncConflict(
                            Guid.NewGuid(), id, DateTimeOffset.UtcNow, winner, losing));
                    }
                    if (comparison > 0)
                    {
                        transport.Write(localEnvelope!);
                        uploaded++;
                    }
                    else if (comparison < 0)
                    {
                        ApplyRemote(remoteObject, key, ref downloaded, ref tombstones);
                    }
                }

                // El contador incluye pares ya iguales como "comparados", no como conflictos reales.
                // Devolverlo así permite mostrar actividad sin afirmar que se perdió una edición.
                var result = new SyncResult(uploaded, downloaded, Math.Max(0, conflicts - local.Keys.Intersect(remote.Keys).Count(id =>
                    SyncVersion.Compare(local[id], remote[id].Envelope) == 0)), tombstones);
                _settings.LastSyncAt = DateTimeOffset.UtcNow;
                _settingsService.Save(_settings);
                return result;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or
                                   UriFormatException or FormatException or InvalidOperationException or
                                   CryptographicException)
        {
            return new(0, 0, 0, 0, ex.Message);
        }
    }

    private bool CompletePendingKeyRotation(out string? error)
    {
        error = null;
        if (!_settings.SyncKeyRotationPending) return true;

        byte[]? nextKey = null;
        try
        {
            if (_settings.WrappedPendingSyncKey is null)
                throw new InvalidOperationException("The pending sync key is missing.");

            nextKey = DatabaseKeyProvider.Unwrap(_settings.WrappedPendingSyncKey);
            var transport = CreateTransport();
            var scopedIds = _settings.SyncScope == SyncScopeKind.SelectedNotes
                ? _settings.SyncNoteIds.ToHashSet()
                : null;
            var deviceId = EnsureDeviceId();
            var localNotes = _repository.GetAllForSync()
                .Where(note => scopedIds is null || scopedIds.Contains(note.Id))
                .ToDictionary(note => note.Id);
            var localTombstones = _repository.GetSyncTombstones()
                .Where(tombstone => scopedIds is null || scopedIds.Contains(tombstone.NoteId))
                .ToDictionary(item => item.NoteId);
            var localIds = localNotes.Keys.Union(localTombstones.Keys).ToHashSet();
            var remoteIds = transport.ReadAll()
                .Select(item => item.Envelope.NoteId)
                .Where(id => scopedIds is null || scopedIds.Contains(id))
                .ToHashSet();

            var rekeyedAt = DateTimeOffset.UtcNow;
            foreach (var note in localNotes.Values)
                transport.Write(CreateRekeyedNoteEnvelope(note, nextKey, deviceId, rekeyedAt));
            foreach (var tombstone in localTombstones.Values)
                transport.Write(RekeyEnvelope(SyncEnvelopeCodec.CreateTombstone(tombstone, deviceId), rekeyedAt));
            foreach (var remoteId in remoteIds.Except(localIds))
                transport.Delete(remoteId);

            _settings.WrappedSyncKey = _settings.WrappedPendingSyncKey;
            _settings.WrappedPendingSyncKey = null;
            _settings.SyncKeyRotationPending = false;
            _settingsService.Save(_settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or
                                   UriFormatException or FormatException or InvalidOperationException or
                                   CryptographicException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (nextKey is not null) CryptographicOperations.ZeroMemory(nextKey);
        }
    }

    private void ApplyRemote(
        SyncRemoteObject remote,
        byte[] key,
        ref int downloaded,
        ref int tombstones)
    {
        if (remote.Envelope.Tombstone)
        {
            _repository.ApplySyncTombstone(new SyncTombstone(
                remote.Envelope.NoteId,
                remote.Envelope.UpdatedAt,
                remote.Envelope.DeviceId));
            tombstones++;
        }
        else
        {
            _repository.ApplySyncNote(SyncEnvelopeCodec.DecryptNote(remote.Envelope, key));
        }
        downloaded++;
    }

    private static SyncEnvelope RekeyEnvelope(SyncEnvelope envelope, DateTimeOffset rekeyedAt)
    {
        // La marca nueva es necesaria para que un dispositivo que conserve el código antiguo no
        // considere idéntico el sobre y vuelva a publicarlo con la clave revocada.
        envelope.UpdatedAt = envelope.UpdatedAt >= rekeyedAt
            ? envelope.UpdatedAt.AddTicks(1)
            : rekeyedAt;
        return envelope;
    }

    private static SyncEnvelope CreateRekeyedNoteEnvelope(
        Note note,
        byte[] key,
        string deviceId,
        DateTimeOffset rekeyedAt)
    {
        var updatedAt = note.UpdatedAt >= rekeyedAt
            ? note.UpdatedAt.AddTicks(1)
            : rekeyedAt;
        var copy = new Note
        {
            Id = note.Id,
            Text = note.Text,
            Color = note.Color,
            CreatedAt = note.CreatedAt,
            UpdatedAt = updatedAt,
            State = note.State,
            ScreenOrigin = note.ScreenOrigin,
            Tags = note.Tags.ToArray(),
            IsProtected = note.IsProtected,
            ProtectedContent = note.ProtectedContent
        };
        return SyncEnvelopeCodec.CreateNote(copy, key, deviceId);
    }

    private static SyncConflictVersion ToConflictVersion(
        SyncEnvelope envelope,
        IReadOnlyDictionary<Guid, Note>? localNotes,
        byte[] key)
    {
        Note? note = null;
        if (!envelope.Tombstone)
        {
            note = localNotes?.GetValueOrDefault(envelope.NoteId)
                ?? SyncEnvelopeCodec.DecryptNote(envelope, key);
        }

        return new SyncConflictVersion(envelope.UpdatedAt, envelope.DeviceId, envelope.Tombstone, note);
    }

    private ISyncTransport CreateTransport() => _settings.SyncTransport switch
    {
        SyncTransportKind.Folder => new FolderSyncTransport(_settings.SyncFolderPath ?? string.Empty),
        SyncTransportKind.Server => new HttpSyncTransport(_settings.SyncServerUrl ?? string.Empty, GetServerToken()),
        SyncTransportKind.WebDav => new WebDavSyncTransport(
            _settings.SyncServerUrl ?? string.Empty,
            _settings.SyncWebDavUsername ?? string.Empty,
            GetWebDavPassword()),
        _ => throw new InvalidOperationException("Unknown sync transport.")
    };

    private byte[] GetOrCreateKey()
    {
        var existing = GetExistingKey();
        if (existing is not null) return existing;

        var key = DatabaseKeyProvider.GenerateKey();
        _settings.WrappedSyncKey = DatabaseKeyProvider.Wrap(key);
        EnsureDeviceId();
        _settingsService.Save(_settings);
        return key;
    }

    private byte[]? GetExistingKey() => _settings.WrappedSyncKey is null
        ? null
        : DatabaseKeyProvider.Unwrap(_settings.WrappedSyncKey);

    private string EnsureDeviceId()
    {
        if (!string.IsNullOrWhiteSpace(_settings.SyncDeviceId)) return _settings.SyncDeviceId;
        _settings.SyncDeviceId = Guid.NewGuid().ToString("N");
        _settingsService.Save(_settings);
        return _settings.SyncDeviceId;
    }

}
