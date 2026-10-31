using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Ajuste «tareas hechas» por nota (Default / mantener en su sitio / bajar al final). Viaja con la nota como campo opcional del
/// contenido cifrado; las dos últimas políticas nunca borran. Ver docs/SYNC.md.
/// </summary>
public sealed class CompletedTasksPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-ctp-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;
    private readonly Device _a;
    private readonly Device _b;

    public CompletedTasksPolicyTests()
    {
        _sharedFolder = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_sharedFolder);
        _a = CreateDevice("device-a");
        _b = CreateDevice("device-b");
    }

    [Fact]
    public void ANewNoteFollowsTheDeviceSettings()
    {
        var note = _a.Repository.Create("x", "#EBD38B", "primary");
        Assert.Equal(CompletedTasksPolicy.Default, _a.Repository.GetById(note.Id)!.CompletedTasks);
    }

    [Theory]
    [InlineData(CompletedTasksPolicy.KeepInPlace)]
    [InlineData(CompletedTasksPolicy.MoveToEnd)]
    public void ThePolicyIsStoredAndCountsAsAnEdit(CompletedTasksPolicy policy)
    {
        var note = _a.Repository.Create("x", "#EBD38B", "primary");
        var before = _a.Repository.GetById(note.Id)!.UpdatedAt;
        Thread.Sleep(15);

        _a.Repository.SetCompletedTasksPolicy(note.Id, policy);

        var read = _a.Repository.GetById(note.Id)!;
        Assert.Equal(policy, read.CompletedTasks);
        Assert.True(read.UpdatedAt > before);
        Assert.Equal(policy, _a.Repository.GetByState(NoteState.Active).Single().CompletedTasks);
    }

    [Fact]
    public void ThePolicyCyclesDefaultKeepEnd()
    {
        Assert.Equal(CompletedTasksPolicy.KeepInPlace, CompletedTasksPolicyRules.Next(CompletedTasksPolicy.Default));
        Assert.Equal(CompletedTasksPolicy.MoveToEnd, CompletedTasksPolicyRules.Next(CompletedTasksPolicy.KeepInPlace));
        Assert.Equal(CompletedTasksPolicy.Default, CompletedTasksPolicyRules.Next(CompletedTasksPolicy.MoveToEnd));
    }

    [Fact]
    public void OnlyDefaultMayDeleteAndOnlyWhenTheDeviceAsksForIt()
    {
        var deleting = new AppSettings { AutoHideCompletedTasks = true };
        var plain = new AppSettings { AutoHideCompletedTasks = false };

        Assert.True(CompletedTasksPolicyRules.MayDelete(CompletedTasksPolicy.Default, deleting));
        Assert.False(CompletedTasksPolicyRules.MayDelete(CompletedTasksPolicy.Default, plain));
        Assert.False(CompletedTasksPolicyRules.MayDelete(CompletedTasksPolicy.KeepInPlace, deleting));
        Assert.False(CompletedTasksPolicyRules.MayDelete(CompletedTasksPolicy.MoveToEnd, deleting));
    }

    [Fact]
    public void TheMoveToEndDecisionOverridesTheDeviceSettingBothWays()
    {
        var moving = new AppSettings { MoveCompletedTasksToEnd = true };
        var staying = new AppSettings { MoveCompletedTasksToEnd = false };

        Assert.True(CompletedTasksPolicyRules.ShouldMoveToEnd(CompletedTasksPolicy.Default, moving));
        Assert.False(CompletedTasksPolicyRules.ShouldMoveToEnd(CompletedTasksPolicy.Default, staying));
        Assert.False(CompletedTasksPolicyRules.ShouldMoveToEnd(CompletedTasksPolicy.KeepInPlace, moving));
        Assert.True(CompletedTasksPolicyRules.ShouldMoveToEnd(CompletedTasksPolicy.MoveToEnd, staying));
    }

    [Fact]
    public void ThePruneNeverTouchesANoteThatKeepsOrMovesItsTasks()
    {
        var keep = _a.Repository.Create("[x] done", "#EBD38B", "primary");
        var move = _a.Repository.Create("[x] done", "#EBD38B", "primary");
        var plain = _a.Repository.Create("[x] done", "#EBD38B", "primary");
        _a.Repository.SetCompletedTasksPolicy(keep.Id, CompletedTasksPolicy.KeepInPlace);
        _a.Repository.SetCompletedTasksPolicy(move.Id, CompletedTasksPolicy.MoveToEnd);

        var skipped = _a.Repository.GetByState(NoteState.Active)
            .Where(n => n.CompletedTasks != CompletedTasksPolicy.Default).Select(n => n.Id).ToHashSet();

        Assert.Equal(new[] { keep.Id, move.Id }.OrderBy(i => i), skipped.OrderBy(i => i));
        Assert.DoesNotContain(plain.Id, skipped);
    }

    [Fact]
    public void ThePolicyReachesTheOtherDevice()
    {
        var note = _a.Repository.Create("[ ] milk", "#EBD38B", "primary");
        ShareKeyAndSync();

        _a.Repository.SetCompletedTasksPolicy(note.Id, CompletedTasksPolicy.KeepInPlace);
        SyncBoth();

        Assert.Equal(CompletedTasksPolicy.KeepInPlace, _b.Repository.GetById(note.Id)!.CompletedTasks);
        Assert.Equal("[ ] milk", _b.Repository.GetById(note.Id)!.Text);
    }

    [Fact]
    public void GoingBackToDefaultTravelsToo_SoTheSettingCanBeUndone()
    {
        var note = _a.Repository.Create("x", "#EBD38B", "primary");
        ShareKeyAndSync();
        _a.Repository.SetCompletedTasksPolicy(note.Id, CompletedTasksPolicy.MoveToEnd);
        SyncBoth();
        Assert.Equal(CompletedTasksPolicy.MoveToEnd, _b.Repository.GetById(note.Id)!.CompletedTasks);

        _b.Repository.SetCompletedTasksPolicy(note.Id, CompletedTasksPolicy.Default);
        SyncBoth();

        Assert.Equal(CompletedTasksPolicy.Default, _a.Repository.GetById(note.Id)!.CompletedTasks);
    }

    [Fact]
    public void ThePolicyOnItsOwnIsASyncedChange_SoTheNextPassIsQuiet()
    {
        var note = _a.Repository.Create("x", "#EBD38B", "primary");
        ShareKeyAndSync();
        _a.Repository.SetCompletedTasksPolicy(note.Id, CompletedTasksPolicy.KeepInPlace);
        SyncBoth();

        var again = _a.Sync.Synchronize();

        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(0, again.Uploaded);
        Assert.Equal(0, again.Downloaded);
    }

    [Fact]
    public void ARekeyedEnvelopeKeepsThePolicy()
    {
        var key = new byte[32];
        var note = new Note
        {
            Id = Guid.NewGuid(), Text = "t", Color = "#EBD38B", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            State = NoteState.Active, ScreenOrigin = "primary", CompletedTasks = CompletedTasksPolicy.MoveToEnd
        };

        var envelope = SyncService.CreateRekeyedNoteEnvelope(note, key, "d", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(CompletedTasksPolicy.MoveToEnd, SyncEnvelopeCodec.DecryptNote(envelope, key).CompletedTasks);
    }

    [Fact]
    public void ADefaultNoteWritesNoNewFieldSoItsPayloadIsTheOneOfAnOlderVersion()
    {
        var key = new byte[32];
        var note = new Note
        {
            Id = Guid.NewGuid(), Text = "t", Color = "#EBD38B", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            State = NoteState.Active, ScreenOrigin = "primary"
        };

        Assert.False(PayloadOf(note, key).TryGetProperty("completedTasks", out _));
        note.CompletedTasks = CompletedTasksPolicy.KeepInPlace;
        Assert.Equal(1, PayloadOf(note, key).GetProperty("completedTasks").GetInt32());
        note.CompletedTasks = CompletedTasksPolicy.MoveToEnd;
        Assert.Equal(2, PayloadOf(note, key).GetProperty("completedTasks").GetInt32());
    }

    [Fact]
    public void AnEnvelopeWithAnUnknownPolicyNumberReadsAsDefaultInsteadOfFailing()
    {
        var key = new byte[32];
        var note = new Note
        {
            Id = Guid.NewGuid(), Text = "t", Color = "#EBD38B", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            State = NoteState.Active, ScreenOrigin = "primary", CompletedTasks = (CompletedTasksPolicy)7
        };

        var read = SyncEnvelopeCodec.DecryptNote(SyncEnvelopeCodec.CreateNote(note, key, "d"), key);

        Assert.Equal(CompletedTasksPolicy.Default, read.CompletedTasks);
    }

    [Fact]
    public void ThePolicyCountsInTheContentComparison()
    {
        var one = new Note
        {
            Id = Guid.NewGuid(), Text = "t", Color = "#EBD38B", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            State = NoteState.Active, ScreenOrigin = "primary"
        };
        var two = new Note
        {
            Id = one.Id, Text = "t", Color = "#EBD38B", CreatedAt = one.CreatedAt, UpdatedAt = one.UpdatedAt,
            State = NoteState.Active, ScreenOrigin = "primary", CompletedTasks = CompletedTasksPolicy.KeepInPlace
        };

        Assert.False(NoteEquivalence.SameContent(one, two));
    }

    private static JsonElement PayloadOf(Note note, byte[] key)
    {
        var envelope = SyncEnvelopeCodec.CreateNote(note, key, "d");
        using var cipher = new ContentCipher(key);
        var plain = cipher.Decrypt(new EncryptedContent(
            Convert.FromBase64String(envelope.CipherText!), Convert.FromBase64String(envelope.Nonce!), Convert.FromBase64String(envelope.Tag!)));
        return JsonDocument.Parse(Convert.FromBase64String(plain)).RootElement.Clone();
    }

    private void ShareKeyAndSync()
    {
        _b.Sync.ImportSyncCode(_a.Sync.GetOrCreateSyncCode());
        SyncBoth();
    }

    private void SyncBoth()
    {
        Assert.True(_a.Sync.Synchronize().Succeeded);
        Assert.True(_b.Sync.Synchronize().Succeeded);
        Assert.True(_a.Sync.Synchronize().Succeeded);
    }

    private Device CreateDevice(string deviceId)
    {
        var deviceRoot = Path.Combine(_root, deviceId);
        Directory.CreateDirectory(deviceRoot);
        var settingsService = new SettingsService(Path.Combine(deviceRoot, "settings.json"));
        var settings = new AppSettings
        {
            SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = _sharedFolder, SyncDeviceId = deviceId
        };
        settingsService.Save(settings);
        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, new SyncService(repository, settings, settingsService));
    }

    public void Dispose()
    {
        foreach (var device in new[] { _a, _b })
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = device.DatabasePath }.ToString());
            SqliteConnection.ClearPool(connection);
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record Device(string DatabasePath, NotesRepository Repository, SyncService Sync);
}
