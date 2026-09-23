using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Reproduces the first real multi-device scenarios against two independent databases and one
/// shared folder transport. These tests deliberately sync in both directions instead of testing
/// only SyncVersion.Compare, because the dangerous bugs live at the boundary between tombstones,
/// encrypted envelopes and repository application.
/// </summary>
public sealed class SyncConflictTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-conflicts-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;
    private readonly Device _deviceA;
    private readonly Device _deviceB;

    public SyncConflictTests()
    {
        _sharedFolder = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_sharedFolder);
        _deviceA = CreateDevice("device-a");
        _deviceB = CreateDevice("device-b");
    }

    [Fact]
    public void ConcurrentEdits_UseTheNewestVersionAndConverge()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        // B publishes first; A edits second, so A is the deterministic winner. B must converge
        // to A on its next sync instead of keeping the stale local text.
        _deviceB.Repository.UpdateText(note.Id, "edit from B");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, "edit from A");

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(1, uploaded.ConflictsResolved);
        Assert.Equal(1, downloaded.ConflictsResolved);
        Assert.Equal("edit from A", _deviceB.Repository.GetAllForSync().Single().Text);
        Assert.Single(_deviceB.Sync.GetConflicts());

        // The losing version remains recoverable locally and can be promoted deliberately. The
        // restore gets a fresh timestamp so the next sync can publish the user's decision.
        var conflict = _deviceB.Sync.GetConflicts().Single();
        Assert.True(_deviceB.Sync.RestoreConflict(conflict.Id));
        Assert.Equal("edit from B", _deviceB.Repository.GetAllForSync().Single().Text);
        Assert.Empty(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void DismissAllConflicts_ClearsTheQueueWithoutTouchingTheNotes()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        // Two divergent edits leave one conflict on each side. Dismissing all has to empty the
        // queue without applying anything and without touching the notes themselves.
        _deviceB.Repository.UpdateText(note.Id, "edit from B");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, "edit from A");
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        var conflictsBefore = _deviceB.Sync.GetConflicts().Count;
        Assert.True(conflictsBefore > 0);
        var textBefore = _deviceB.Repository.GetAllForSync().Single().Text;

        var dismissed = _deviceB.Sync.DismissAllConflicts();

        Assert.Equal(conflictsBefore, dismissed);
        Assert.Empty(_deviceB.Sync.GetConflicts());
        Assert.Equal(textBefore, _deviceB.Repository.GetAllForSync().Single().Text);
        Assert.Equal(0, _deviceB.Sync.DismissAllConflicts());
    }

    [Fact]
    public void NewerDelete_BeatsAnOlderEditAndDoesNotResurrect()
    {
        var note = _deviceA.Repository.Create("original", "#AAE6B1", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        _deviceB.Repository.UpdateText(note.Id, "stale edit");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        Assert.True(_deviceA.Repository.Delete(note.Id));

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(1, uploaded.ConflictsResolved);
        Assert.Equal(1, downloaded.ConflictsResolved);
        Assert.Empty(_deviceB.Repository.GetAllForSync());
        Assert.Equal("device-a", _deviceB.Repository.GetSyncTombstones().Single().DeviceId);
    }

    [Fact]
    public void TombstoneEnvelope_UsesPublishingDeviceForTieBreaks()
    {
        var noteId = Guid.NewGuid();
        var deletedAt = DateTimeOffset.UtcNow;
        var tombstone = new SyncTombstone(noteId, deletedAt, "database-owner");

        var envelope = SyncEnvelopeCodec.CreateTombstone(tombstone, new byte[32], "publishing-device");

        Assert.Equal("publishing-device", envelope.DeviceId);
        Assert.Equal(deletedAt, envelope.UpdatedAt);
    }

    [Fact]
    public void TombstoneEnvelope_IsAuthenticatedWithTheSyncKey()
    {
        var key = new byte[32];
        var envelope = SyncEnvelopeCodec.CreateTombstone(
            new SyncTombstone(Guid.NewGuid(), DateTimeOffset.UtcNow, "a"), key, "a");

        Assert.True(SyncEnvelopeCodec.IsAuthenticTombstone(envelope, key));

        var otherKey = new byte[32];
        otherKey[0] = 1;
        Assert.False(SyncEnvelopeCodec.IsAuthenticTombstone(envelope, otherKey));

        // Cambiar la fecha visible (para ganar a cualquier edición) rompe la autenticación.
        envelope.UpdatedAt = envelope.UpdatedAt.AddYears(10);
        Assert.False(SyncEnvelopeCodec.IsAuthenticTombstone(envelope, key));
    }

    [Fact]
    public void ForgedTombstone_IsIgnoredAndTheNoteStaysAsItWas()
    {
        // Quien puede escribir en el almacén (el servidor, el NAS, la cuenta de Nextcloud) pero no
        // tiene la clave no puede fabricar un borrado: antes bastaba un sobre sin cifrar con el id
        // y una fecha futura para borrar la nota para siempre en todos los dispositivos.
        var note = _deviceA.Repository.Create("importante", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        new FolderSyncTransport(_sharedFolder).Write(new SyncEnvelope
        {
            NoteId = note.Id,
            Tombstone = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddYears(1),
            DeviceId = "attacker",
        });

        var result = _deviceB.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        var survivor = Assert.Single(_deviceB.Repository.GetAllForSync());
        Assert.Equal("importante", survivor.Text);
        Assert.Equal(NoteState.Active, survivor.State);
        Assert.Empty(_deviceB.Repository.GetSyncTombstones());
        Assert.Empty(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void ForgedTombstone_ForANoteThisDeviceNeverHad_IsIgnored()
    {
        _deviceA.Sync.ImportSyncCode(_deviceB.Sync.GetOrCreateSyncCode());
        var unknown = Guid.NewGuid();
        new FolderSyncTransport(_sharedFolder).Write(new SyncEnvelope
        {
            NoteId = unknown,
            Tombstone = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            DeviceId = "attacker",
        });

        var result = _deviceA.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(_deviceA.Repository.GetSyncTombstones());
    }

    [Fact]
    public void SelectedScope_ExchangesOnlyTheChosenNote()
    {
        var selected = _deviceA.Repository.Create("shared", "#EBD38B", "primary");
        _deviceA.Repository.Create("private", "#AAE6B1", "primary");
        _deviceA.Settings.SyncScope = SyncScopeKind.SelectedNotes;
        _deviceA.Settings.SyncNoteIds = new List<Guid> { selected.Id };

        var code = _deviceA.Sync.GetOrCreateSyncCode();
        _deviceB.Sync.ImportSyncCode(code);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.Single(_deviceB.Repository.GetAllForSync());

        _deviceB.Repository.Create("created on B", "#83E7F2", "primary");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);

        Assert.DoesNotContain(_deviceA.Repository.GetAllForSync(), note => note.Text == "created on B");
    }

    [Fact]
    public void SharedProfileCodeCarriesTheSelectionToANewDevice()
    {
        var selected = _deviceA.Repository.Create("shared with another user", "#EBD38B", "primary");
        _deviceA.Repository.Create("private", "#AAE6B1", "primary");
        _deviceA.Settings.SyncScope = SyncScopeKind.SelectedNotes;
        _deviceA.Settings.SyncNoteIds = new List<Guid> { selected.Id };

        var code = _deviceA.Sync.GetOrCreateShareCode();
        _deviceB.Sync.ImportSyncCode(code);

        Assert.Equal(SyncScopeKind.SelectedNotes, _deviceB.Settings.SyncScope);
        Assert.Equal(new[] { selected.Id }, _deviceB.Settings.SyncNoteIds);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.Equal("shared with another user", _deviceB.Repository.GetAllForSync().Single().Text);
    }

    [Fact]
    public void RevokingSharedAccessRequiresANewInvitation()
    {
        var note = _deviceA.Repository.Create("shared", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        Assert.True(_deviceA.Sync.RevokeSharedAccess(out var error), error);

        // El código antiguo ya no puede descifrar la nueva versión del sobre.
        var oldCodeResult = _deviceB.Sync.Synchronize();
        Assert.False(oldCodeResult.Succeeded);

        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        var updated = _deviceB.Sync.Synchronize();
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("shared", _deviceB.Repository.GetAllForSync().Single().Text);
    }

    [Fact]
    public void DeletionsStayAuthenticAfterRevokingSharedAccess()
    {
        // Al rotar la clave los borrados se vuelven a firmar con la nueva. Si la fecha del sobre se
        // cambiara después de firmar, el otro dispositivo los tomaría por falsos y no borraría.
        var kept = _deviceA.Repository.Create("se queda", "#EBD38B", "primary");
        var deleted = _deviceA.Repository.Create("se borra", "#AAE6B1", "primary");
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.Equal(2, _deviceB.Repository.GetAllForSync().Count);

        Assert.True(_deviceA.Repository.Delete(deleted.Id));
        Assert.True(_deviceA.Sync.RevokeSharedAccess(out var error), error);
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        var result = _deviceB.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(kept.Id, Assert.Single(_deviceB.Repository.GetAllForSync()).Id);
    }

    [Fact]
    public void ForgedFutureTombstone_DoesNotFreezeTheNote()
    {
        // Un borrado sin firmar con fecha futura ganaba siempre la comparación de versiones: el
        // almacén se negaba a guardar encima cualquier edición y la nota dejaba de sincronizarse
        // sin ningún error. Ahora se quita del almacén y la nota se vuelve a publicar.
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);
        new FolderSyncTransport(_sharedFolder).Write(new SyncEnvelope
        {
            NoteId = note.Id,
            Tombstone = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddYears(50),
            DeviceId = "attacker",
        });

        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        _deviceA.Repository.UpdateText(note.Id, "editada en A");
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        Assert.Equal("editada en A", Assert.Single(_deviceB.Repository.GetAllForSync()).Text);
    }

    [Fact]
    public void UnsignedTombstone_FromAnOlderVersion_MovesTheNoteToTheTrash()
    {
        // Un borrado hecho desde una versión anterior a la 1.0 no va firmado. No se aplica como
        // borrado definitivo (podría ser falso), pero tampoco se ignora: la nota va a la papelera,
        // que es lo que el usuario quería y se puede deshacer.
        var note = _deviceA.Repository.Create("borrada en un equipo antiguo", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);
        new FolderSyncTransport(_sharedFolder).Write(new SyncEnvelope
        {
            NoteId = note.Id,
            Tombstone = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            DeviceId = "old-device",
        });

        var result = _deviceB.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(NoteState.Trashed, Assert.Single(_deviceB.Repository.GetAllForSync()).State);
        Assert.Empty(_deviceB.Repository.GetSyncTombstones());

        // Y la papelera llega al resto: la nota en papelera es más reciente que el borrado sin firmar.
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.Equal(NoteState.Trashed, Assert.Single(_deviceA.Repository.GetAllForSync()).State);
    }

    [Fact]
    public void AnUnreadableObject_IsSkippedAndTheRestStillSyncs()
    {
        // Un solo objeto que no se puede descifrar paraba toda la sincronización de todos los
        // dispositivos hasta borrarlo a mano.
        var note = _deviceA.Repository.Create("buena", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);
        new FolderSyncTransport(_sharedFolder).Write(new SyncEnvelope
        {
            NoteId = Guid.NewGuid(),
            UpdatedAt = DateTimeOffset.UtcNow,
            DeviceId = "other",
            CipherText = Convert.ToBase64String(new byte[32]),
            Nonce = Convert.ToBase64String(new byte[12]),
            Tag = Convert.ToBase64String(new byte[16]),
        });
        _deviceA.Repository.UpdateText(note.Id, "sigue sincronizando");

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal("sigue sincronizando", Assert.Single(_deviceB.Repository.GetAllForSync()).Text);
    }

    [Fact]
    public void ADeviceWithARevokedCode_FailsBeforeUploadingAnything()
    {
        // Con el código revocado no puede leer nada del almacén. Antes subía primero sus notas nuevas,
        // cifradas con la clave vieja, y el resto de dispositivos ya no podía sincronizar.
        var note = _deviceA.Repository.Create("compartida", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);
        Assert.True(_deviceA.Sync.RevokeSharedAccess(out var error), error);
        var stale = _deviceB.Repository.Create("nueva en B con el código viejo", "#AAE6B1", "primary");

        var result = _deviceB.Sync.Synchronize();

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(new FolderSyncTransport(_sharedFolder).ReadAll(), item => item.Envelope.NoteId == stale.Id);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
    }

    [Fact]
    public void ChangingOnlyTheTags_Syncs()
    {
        var note = _deviceA.Repository.Create("con etiquetas", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);
        Thread.Sleep(20);

        _deviceA.Repository.SetTags(note.Id, new[] { "Trabajo" });
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        Assert.Equal(new[] { "Trabajo" }, Assert.Single(_deviceB.Repository.GetAllForSync()).Tags);
    }

    [Fact]
    public void DeletingATag_Syncs()
    {
        var note = _deviceA.Repository.Create("con etiquetas", "#EBD38B", "primary");
        _deviceA.Repository.SetTags(note.Id, new[] { "Trabajo" });
        ShareKeyAndSynchronizeInitialNote(note.Id);
        Thread.Sleep(20);

        _deviceA.Repository.DeleteTag("Trabajo");
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        Assert.Empty(Assert.Single(_deviceB.Repository.GetAllForSync()).Tags);
    }

    private void ShareKeyAndSynchronizeInitialNote(Guid noteId)
    {
        var code = _deviceA.Sync.GetOrCreateSyncCode();
        _deviceB.Sync.ImportSyncCode(code);

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(1, downloaded.Downloaded);
        Assert.Equal(noteId, _deviceB.Repository.GetAllForSync().Single().Id);
    }

    [Fact]
    public void DockOrderTravelsInsideTheEncryptedEnvelope()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        // El orden viaja solo si tiene momento: arrastrar pone fecha a la nota para que el sobre se
        // vuelva a publicar. Sin eso el segundo dispositivo conservaría el orden antiguo.
        var first = _deviceA.Repository.Create("first", "#EBD38B", "primary");
        var second = _deviceA.Repository.Create("second", "#EBD38B", "primary");
        var ids = _deviceA.Repository.GetByState(NoteState.Active).Select(n => n.Id).ToList();
        var before = _deviceA.Repository.GetById(first.Id)!.UpdatedAt;
        _deviceA.Repository.MoveNote(second.Id, targetIndex: 0, ids);

        Assert.True(_deviceA.Repository.GetById(second.Id)!.UpdatedAt > before);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        Assert.Equal("second", _deviceB.Repository.GetByState(NoteState.Active).First().Text);
        Assert.NotNull(_deviceB.Repository.GetById(second.Id)!.DockPosition);
    }

    private Device CreateDevice(string deviceId)
    {
        var deviceRoot = Path.Combine(_root, deviceId);
        Directory.CreateDirectory(deviceRoot);
        var settingsPath = Path.Combine(deviceRoot, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var settings = new AppSettings
        {
            SyncEnabled = true,
            SyncTransport = SyncTransportKind.Folder,
            SyncFolderPath = _sharedFolder,
            SyncDeviceId = deviceId
        };
        settingsService.Save(settings);

        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(
            new NotesDatabase(databasePath),
            new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, settings, new SyncService(repository, settings, settingsService));
    }

    public void Dispose()
    {
        foreach (var device in new[] { _deviceA, _deviceB })
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = device.DatabasePath }.ToString());
            SqliteConnection.ClearPool(connection);
        }

        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record Device(string DatabasePath, NotesRepository Repository, AppSettings Settings, SyncService Sync);
}
