using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// «Fijada arriba» a través de la sincronización real (dos bases independientes y una carpeta compartida): conflictos, desfijar,
/// papelera, archivo y orden manual. <c>PinnedToTopTests</c> cubre el repositorio y el sobre por separado; aquí se comprueba lo que solo
/// aparece al juntarlos. Ver docs/specs/2026-10-31-pinned-notes-design.md.
/// </summary>
public sealed class PinnedToTopSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-pinned-sync-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;
    private readonly Device _deviceA;
    private readonly Device _deviceB;

    public PinnedToTopSyncTests()
    {
        _sharedFolder = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_sharedFolder);
        _deviceA = CreateDevice("device-a");
        _deviceB = CreateDevice("device-b");
    }

    [Fact]
    public void APinMadeOnOneDeviceReachesTheOtherAndTheTextIsUntouched()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        Assert.False(_deviceB.Repository.GetById(note.Id)!.IsPinnedToTop);

        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();

        var onB = _deviceB.Repository.GetById(note.Id)!;
        Assert.True(onB.IsPinnedToTop);
        Assert.Equal("original", onB.Text);
    }

    [Fact]
    public void UnpinningAfterASyncReachesTheOtherDevice()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();
        Assert.True(_deviceB.Repository.GetById(note.Id)!.IsPinnedToTop);

        _deviceB.Repository.SetPinnedToTop(note.Id, false);
        SyncBoth();

        Assert.False(_deviceA.Repository.GetById(note.Id)!.IsPinnedToTop);
        Assert.False(_deviceB.Repository.GetById(note.Id)!.IsPinnedToTop);
    }

    [Fact]
    public void APinWithNothingElseChangedIsASyncedChange_SoTheNextPassIsQuiet()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();

        var again = _deviceA.Sync.Synchronize();

        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(0, again.Uploaded);
        Assert.Equal(0, again.Downloaded);
        Assert.Equal(0, again.ConflictsResolved);
    }

    [Fact]
    public void WhenOneSidePinsAndTheOtherEditsTheTextLater_TheEditWinsAndTheNoteEndsUnpinned()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();

        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        // B no ha visto el pin: edita sobre la versión vieja y, al ser la más reciente, su versión (sin pin) gana.
        _deviceB.Repository.UpdateText(note.Id, "edit from B");
        var uploaded = _deviceB.Sync.Synchronize();
        var downloaded = _deviceA.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        foreach (var device in new[] { _deviceA, _deviceB })
        {
            var winner = device.Repository.GetById(note.Id)!;
            Assert.Equal("edit from B", winner.Text);
            Assert.False(winner.IsPinnedToTop);
        }

        // La versión que pierde (la fijada) queda guardada en el conflicto de B, que fue quien lo vio.
        Assert.Single(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void WhenOneSideEditsTheTextAndTheOtherPinsLater_ThePinWinsWithItsOldText()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();

        _deviceB.Repository.UpdateText(note.Id, "edit from B");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        // No se mezcla campo a campo: gana la versión entera más reciente, con su pin y su texto.
        foreach (var device in new[] { _deviceA, _deviceB })
        {
            var winner = device.Repository.GetById(note.Id)!;
            Assert.Equal("original", winner.Text);
            Assert.True(winner.IsPinnedToTop);
        }

        // Y la edición perdida se puede recuperar desde el conflicto de A.
        var conflict = _deviceA.Sync.GetConflicts().Single();
        Assert.True(_deviceA.Sync.RestoreConflict(conflict.Id));
        Assert.Equal("edit from B", _deviceA.Repository.GetById(note.Id)!.Text);
        Assert.False(_deviceA.Repository.GetById(note.Id)!.IsPinnedToTop);
    }

    [Fact]
    public void WhenBothSidesPinTheSameNote_ThereIsNoConflict()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();

        _deviceB.Repository.SetPinnedToTop(note.Id, true);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();

        Assert.True(_deviceA.Repository.GetById(note.Id)!.IsPinnedToTop);
        Assert.True(_deviceB.Repository.GetById(note.Id)!.IsPinnedToTop);
        // Los dos contenidos son equivalentes: solo cambia la fecha, no hay nada que guardar.
        Assert.Empty(_deviceA.Sync.GetConflicts());
        Assert.Empty(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void APinnedNoteSentToTheTrashStaysTrashedOnTheOtherDevice_AndKeepsItsPinWhenRestored()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();

        _deviceA.Repository.SetState(note.Id, NoteState.Trashed);
        SyncBoth();

        var trashedOnB = _deviceB.Repository.GetById(note.Id)!;
        Assert.Equal(NoteState.Trashed, trashedOnB.State);
        Assert.Empty(_deviceB.Repository.GetByState(NoteState.Active));

        _deviceB.Repository.SetState(note.Id, NoteState.Active);
        SyncBoth();
        Assert.Equal(NoteState.Active, _deviceA.Repository.GetById(note.Id)!.State);
        Assert.True(_deviceA.Repository.GetById(note.Id)!.IsPinnedToTop);
    }

    [Fact]
    public void DeletingAPinnedNoteForGoodRemovesItOnTheOtherDevice()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();

        Assert.True(_deviceA.Repository.Delete(note.Id));
        SyncBoth();

        Assert.Null(_deviceB.Repository.GetById(note.Id));
    }

    [Fact]
    public void AnArchivedPinnedNoteArrivesArchivedAndPinned_AndDoesNotShowInTheActiveDeck()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        _deviceA.Repository.SetState(note.Id, NoteState.Archived);
        SyncBoth();

        var onB = _deviceB.Repository.GetById(note.Id)!;
        Assert.Equal(NoteState.Archived, onB.State);
        Assert.True(onB.IsPinnedToTop);
        Assert.Empty(_deviceB.Repository.GetByState(NoteState.Active));
        Assert.Equal(onB.Id, _deviceB.Repository.GetByState(NoteState.Archived).Single().Id);
    }

    [Fact]
    public void PinAndManualOrderTravelTogether_PinnedFirstAndTheGroupOrderIsKept()
    {
        var a = _deviceA.Repository.Create("a", "#EBD38B", "primary");
        var b = _deviceA.Repository.Create("b", "#EBD38B", "primary");
        var c = _deviceA.Repository.Create("c", "#EBD38B", "primary");
        var d = _deviceA.Repository.Create("d", "#EBD38B", "primary");
        var code = _deviceA.Sync.GetOrCreateSyncCode();
        _deviceB.Sync.ImportSyncCode(code);
        SyncBoth();
        Assert.Equal(4, _deviceB.Repository.GetByState(NoteState.Active).Count);

        // Se fija la «c» y se arrastra la «d» al principio del grupo sin fijar; dentro del grupo fijado no hay nada que mover.
        _deviceA.Repository.SetPinnedToTop(c.Id, true);
        _deviceA.Repository.MoveNote(d.Id, 0, new[] { a.Id, b.Id, d.Id });
        SyncBoth();

        Assert.Equal(new[] { "c", "d", "a", "b" },
            _deviceA.Repository.GetByState(NoteState.Active).Select(n => n.Text));
        Assert.Equal(new[] { "c", "d", "a", "b" },
            _deviceB.Repository.GetByState(NoteState.Active).Select(n => n.Text));
    }

    [Fact]
    public void PinningOnTheReceivingDeviceDoesNotMoveTheNoteInsideItsGroupForTheSender()
    {
        var a = _deviceA.Repository.Create("a", "#EBD38B", "primary");
        var b = _deviceA.Repository.Create("b", "#EBD38B", "primary");
        var c = _deviceA.Repository.Create("c", "#EBD38B", "primary");
        _deviceA.Repository.MoveNote(c.Id, 0, new[] { a.Id, b.Id, c.Id });
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        SyncBoth();

        _deviceB.Repository.SetPinnedToTop(b.Id, true);
        SyncBoth();
        _deviceB.Repository.SetPinnedToTop(b.Id, false);
        SyncBoth();

        // Fijar y desfijar devuelve la nota a su sitio: el pin no es una posición, la posición manual sigue siendo la de antes.
        Assert.Equal(new[] { "c", "a", "b" }, _deviceA.Repository.GetByState(NoteState.Active).Select(n => n.Text));
        Assert.Equal(new[] { "c", "a", "b" }, _deviceB.Repository.GetByState(NoteState.Active).Select(n => n.Text));
    }

    [Fact]
    public void AnOlderDeviceThatEditsAPinnedNoteAndSendsItWithoutThePin_RemovesThePinEverywhere()
    {
        // Pérdida documentada en la spec (tabla «Compatibilidad»): una versión que no conoce el campo lo reenvía ausente = no fijada.
        // Se simula con una versión posterior de la misma nota sin el pin, que es el mismo sobre que escribiría la versión antigua.
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetPinnedToTop(note.Id, true);
        SyncBoth();
        Thread.Sleep(20);

        _deviceB.Repository.UpdateText(note.Id, "edited by an older version");
        _deviceB.Repository.SetPinnedToTop(note.Id, false);
        SyncBoth();

        Assert.False(_deviceA.Repository.GetById(note.Id)!.IsPinnedToTop);
        Assert.Equal("edited by an older version", _deviceA.Repository.GetById(note.Id)!.Text);
    }

    private void ShareKeyAndSynchronize()
    {
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        SyncBoth();
    }

    private void SyncBoth()
    {
        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();
        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        // Una segunda vuelta de A recoge lo que B haya publicado.
        var back = _deviceA.Sync.Synchronize();
        Assert.True(back.Succeeded, back.Error);
    }

    private Device CreateDevice(string deviceId)
    {
        var deviceRoot = Path.Combine(_root, deviceId);
        Directory.CreateDirectory(deviceRoot);
        var settingsService = new SettingsService(Path.Combine(deviceRoot, "settings.json"));
        var settings = new AppSettings
        {
            SyncEnabled = true,
            SyncTransport = SyncTransportKind.Folder,
            SyncFolderPath = _sharedFolder,
            SyncDeviceId = deviceId
        };
        settingsService.Save(settings);

        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, new SyncService(repository, settings, settingsService));
    }

    public void Dispose()
    {
        foreach (var device in new[] { _deviceA, _deviceB })
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = device.DatabasePath }.ToString());
            SqliteConnection.ClearPool(connection);
        }

        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record Device(string DatabasePath, NotesRepository Repository, SyncService Sync);
}
