using System.IO.Compression;
using System.Text.Json;
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class LocalBackupRestoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-restore-test-{Guid.NewGuid():N}");
    private readonly string _data;
    private readonly string _out;

    public LocalBackupRestoreTests()
    {
        _data = Path.Combine(_root, "data");
        _out = Path.Combine(_root, "out");
        Directory.CreateDirectory(_data);
        Directory.CreateDirectory(_out);
    }

    public void Dispose()
    {
        // Same reasoning as LocalBackupTests: clear only the pools of this test's databases, since
        // xUnit runs classes in parallel and ClearAllPools would cut another class's connection.
        if (Directory.Exists(_root))
        {
            foreach (var database in Directory.EnumerateFiles(_root, "*.db*", SearchOption.AllDirectories))
                ClearPool(database);
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void ClearPool(string database)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database }.ToString());
        SqliteConnection.ClearPool(connection);
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static void CreateData(string dir, string settings, params string[] texts)
    {
        Directory.CreateDirectory(dir);
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(dir, "notes.db")), new ContentCipher(new byte[32]));
        foreach (var text in texts) repository.Create(text, "#EBD38B", "primary");
        // Release the pooled handle: the restore tests move and delete this file.
        ClearPool(Path.Combine(dir, "notes.db"));
        File.WriteAllText(Path.Combine(dir, "settings.json"), settings);
    }

    private static string[] TextsOf(string dir)
    {
        var path = Path.Combine(dir, "notes.db");
        var texts = new NotesRepository(new NotesDatabase(path), new ContentCipher(new byte[32]))
            .GetByState(NoteState.Active).Select(n => n.Text).OrderBy(t => t, StringComparer.Ordinal).ToArray();
        // Release the pooled handle so the test can move or delete the file afterwards.
        ClearPool(path);
        return texts;
    }

    private string MakeZip(params string[] texts)
    {
        CreateData(_data, "{\"A\":1}", texts);
        var zip = Path.Combine(_out, "b.zip");
        LocalBackup.CreateManual(_data, zip, "9.9.9", Now);
        return zip;
    }

    private void AddLiveNote(string text)
    {
        var path = Path.Combine(_data, "notes.db");
        new NotesRepository(new NotesDatabase(path), new ContentCipher(new byte[32])).Create(text, "#EBD38B", "primary");
        ClearPool(path);
    }

    [Fact]
    public void CreateManual_WritesOneZipWithDatabaseSettingsAndManifest()
    {
        var zip = MakeZip("uno", "dos");

        using (var archive = ZipFile.OpenRead(zip))
            Assert.Equal(new[] { "manifest.json", "notes.db", "settings.json" },
                archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal));
        var check = LocalBackup.Verify(zip);
        Assert.True(check.IsOk);
        Assert.Equal("9.9.9", check.Manifest!.AppVersion);
        Assert.Equal(2, check.Manifest.NoteCount);
        Assert.Equal(Now.UtcDateTime, check.Manifest.CreatedUtc);
    }

    [Fact]
    public void CreateManual_OverwritesAnExistingFileAndLeavesNoTempFiles()
    {
        MakeZip("uno");
        var zip = MakeZip("uno", "dos");

        // CreateData adds to the same database, so the second call leaves three notes.
        Assert.Equal(3, LocalBackup.Verify(zip).Manifest!.NoteCount);
        Assert.Equal(new[] { "b.zip" }, Directory.GetFiles(_out).Select(Path.GetFileName));
    }

    [Fact]
    public void Verify_RejectsEachKindOfBadBackup()
    {
        var notZip = Path.Combine(_out, "x.zip");
        File.WriteAllText(notZip, "nope");
        Assert.Equal(BackupProblem.Unreadable, LocalBackup.Verify(notZip).Problem);
        Assert.Equal(BackupProblem.Unreadable, LocalBackup.Verify(Path.Combine(_out, "missing.zip")).Problem);

        var zip = MakeZip("uno");
        foreach (var (entry, problem) in new[]
        {
            ("manifest.json", BackupProblem.MissingManifest),
            ("notes.db", BackupProblem.MissingDatabase),
            ("settings.json", BackupProblem.MissingSettings),
        })
        {
            var copy = Path.Combine(_out, "without-" + entry + ".zip");
            File.Copy(zip, copy);
            using (var archive = ZipFile.Open(copy, ZipArchiveMode.Update)) archive.GetEntry(entry)!.Delete();
            Assert.Equal(problem, LocalBackup.Verify(copy).Problem);
        }

        var garbage = Path.Combine(_out, "garbage.zip");
        File.Copy(zip, garbage);
        ReplaceEntry(garbage, "notes.db", new string('x', 5000));
        Assert.Equal(BackupProblem.DatabaseDamaged, LocalBackup.Verify(garbage).Problem);
    }

    private static void ReplaceEntry(string zip, string name, string content)
    {
        using var archive = ZipFile.Open(zip, ZipArchiveMode.Update);
        archive.GetEntry(name)!.Delete();
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    [Fact]
    public void Verify_RefusesABackupFromANewerFormat()
    {
        var zip = MakeZip("uno");
        ReplaceEntry(zip, "manifest.json", JsonSerializer.Serialize(new
        {
            Format = LocalBackup.ManifestFormat + 1, AppVersion = "99.0.0", CreatedUtc = Now.UtcDateTime, NoteCount = 1,
        }));

        Assert.Equal(BackupProblem.TooNew, LocalBackup.Verify(zip).Problem);
    }

    [Fact]
    public void Verify_RefusesADatabaseWithANewerSchemaStamp()
    {
        var zip = MakeZip("uno");
        var db = Path.Combine(_out, "future.db");
        using (var archive = ZipFile.OpenRead(zip)) archive.GetEntry("notes.db")!.ExtractToFile(db);
        using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {LocalBackup.SupportedSchemaVersion + 1};";
            command.ExecuteNonQuery();
        }
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            archive.GetEntry("notes.db")!.Delete();
            archive.CreateEntryFromFile(db, "notes.db");
        }

        Assert.Equal(BackupProblem.TooNew, LocalBackup.Verify(zip).Problem);
    }

    [Fact]
    public void Verify_AcceptsADailyFolderWithoutManifest()
    {
        CreateData(_data, "{}", "uno");
        var folder = LocalBackup.CreateDaily(_data, Now)!;

        var check = LocalBackup.Verify(folder);

        Assert.True(check.IsOk);
        Assert.Equal(1, check.Manifest!.NoteCount);
    }

    [Fact]
    public void StageRestore_DoesNotTouchLiveDataUntilApplied()
    {
        var zip = MakeZip("viejo");
        File.WriteAllText(Path.Combine(_data, "settings.json"), "{\"A\":2}");
        AddLiveNote("nuevo");

        var staged = LocalBackup.StageRestore(_data, zip);

        Assert.True(staged.IsOk);
        Assert.True(LocalBackup.HasPendingRestore(_data));
        Assert.Equal(new[] { "nuevo", "viejo" }, TextsOf(_data));
        Assert.Equal("{\"A\":2}", File.ReadAllText(Path.Combine(_data, "settings.json")));
    }

    [Fact]
    public void StageRestore_OfABadBackupStagesNothing()
    {
        var bad = Path.Combine(_out, "bad.zip");
        File.WriteAllText(bad, "nope");

        var staged = LocalBackup.StageRestore(_data, bad);

        Assert.False(staged.IsOk);
        Assert.False(LocalBackup.HasPendingRestore(_data));
    }

    [Fact]
    public void ApplyPendingRestore_SwapsFilesAndKeepsThePreviousOnesAsBeforeRestore()
    {
        var zip = MakeZip("viejo");
        File.WriteAllText(Path.Combine(_data, "settings.json"), "{\"A\":2}");
        AddLiveNote("nuevo");
        LocalBackup.StageRestore(_data, zip);

        Assert.True(LocalBackup.ApplyPendingRestore(_data));

        Assert.False(LocalBackup.HasPendingRestore(_data));
        Assert.Equal(new[] { "viejo" }, TextsOf(_data));
        Assert.Equal("{\"A\":1}", File.ReadAllText(Path.Combine(_data, "settings.json")));
        Assert.Equal("{\"A\":2}", File.ReadAllText(Path.Combine(_data, "settings.json.before-restore")));
        // The replaced database is kept whole and readable on its own.
        var kept = Path.Combine(_root, "kept");
        Directory.CreateDirectory(kept);
        File.Copy(Path.Combine(_data, "notes.db.before-restore"), Path.Combine(kept, "notes.db"));
        Assert.Equal(new[] { "nuevo", "viejo" }, TextsOf(kept));
    }

    [Fact]
    public void ApplyPendingRestore_NeverOverwritesAnEarlierBeforeRestoreCopy()
    {
        var zip = MakeZip("a");
        File.WriteAllText(Path.Combine(_data, "settings.json.before-restore"), "first");
        LocalBackup.StageRestore(_data, zip);

        Assert.True(LocalBackup.ApplyPendingRestore(_data));

        Assert.Equal("first", File.ReadAllText(Path.Combine(_data, "settings.json.before-restore")));
        Assert.Contains(Directory.GetFiles(_data).Select(Path.GetFileName),
            name => name!.StartsWith("settings.json.before-restore", StringComparison.Ordinal) && name != "settings.json.before-restore");
    }

    [Fact]
    public void ApplyPendingRestore_WithAStagedDatabaseThatWentBadLeavesLiveDataAlone()
    {
        var zip = MakeZip("viejo");
        LocalBackup.StageRestore(_data, zip);
        File.WriteAllText(Path.Combine(_data, "restore-pending", "notes.db"), new string('x', 5000));

        Assert.False(LocalBackup.ApplyPendingRestore(_data));

        Assert.Equal(new[] { "viejo" }, TextsOf(_data));
        Assert.False(LocalBackup.HasPendingRestore(_data));
    }

    [Fact]
    public void ApplyPendingRestore_WithoutAnyLiveFilesJustInstallsTheBackup()
    {
        var zip = MakeZip("viejo");
        LocalBackup.StageRestore(_data, zip);
        File.Delete(Path.Combine(_data, "notes.db"));
        File.Delete(Path.Combine(_data, "settings.json"));

        Assert.True(LocalBackup.ApplyPendingRestore(_data));

        Assert.Equal(new[] { "viejo" }, TextsOf(_data));
    }

    [Fact]
    public void StageRestore_FromADailyFolderWorks()
    {
        CreateData(_data, "{\"A\":1}", "viejo");
        var folder = LocalBackup.CreateDaily(_data, Now)!;
        AddLiveNote("nuevo");

        Assert.True(LocalBackup.StageRestore(_data, folder).IsOk);
        Assert.True(LocalBackup.ApplyPendingRestore(_data));

        Assert.Equal(new[] { "viejo" }, TextsOf(_data));
        Assert.True(File.Exists(Path.Combine(_data, "notes.db.before-restore")));
    }

    [Fact]
    public void ApplyPendingRestore_WithNothingStagedDoesNothing()
    {
        Assert.False(LocalBackup.ApplyPendingRestore(_data));
    }

    [Fact]
    public void ADatabaseWhoseNoteTableLacksTheExpectedColumns_IsRejected()
    {
        var folder = Path.Combine(_out, "odd");
        Directory.CreateDirectory(folder);
        var db = Path.Combine(folder, "notes.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Note (Foo TEXT);";
            command.ExecuteNonQuery();
        }
        ClearPool(db);
        File.WriteAllText(Path.Combine(folder, "settings.json"), "{}");

        Assert.Equal(BackupProblem.DatabaseDamaged, LocalBackup.Verify(folder).Problem);
    }

    [Fact]
    public void SettingsThatAreNotJson_AreRejectedAtVerifyAndNeverSwapped()
    {
        CreateData(_data, "{\"A\":1}", "keep me");
        var good = MakeZipFrom("other");
        var broken = Path.Combine(_out, "broken.zip");
        File.Copy(good, broken);
        using (var archive = ZipFile.Open(broken, ZipArchiveMode.Update))
        {
            archive.GetEntry("settings.json")!.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("settings.json").Open());
            writer.Write("not json {{{");
        }

        Assert.Equal(BackupProblem.MissingSettings, LocalBackup.Verify(broken).Problem);
        Assert.Equal(new[] { "keep me" }, TextsOf(_data));
    }

    [Fact]
    public void WhenTheSwapFailsHalfWay_TheLiveFilesComeBackAndTheStagedCopyStays()
    {
        var zip = MakeZipFrom("from backup");
        var live = Path.Combine(_root, "live");
        CreateData(live, "{\"Live\":true}", "live note");
        Assert.True(LocalBackup.StageRestore(live, zip).IsOk);

        // A program holding settings.json open (an antivirus, say) makes the second file of the swap fail after the first one moved.
        using (new FileStream(Path.Combine(live, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<IOException>(() => LocalBackup.ApplyPendingRestore(live));
        }

        Assert.Equal(new[] { "live note" }, TextsOf(live));
        Assert.Contains("Live", File.ReadAllText(Path.Combine(live, "settings.json")));
        Assert.True(LocalBackup.HasPendingRestore(live));

        // Once the file is free the same staged copy applies.
        Assert.True(LocalBackup.ApplyPendingRestore(live));
        Assert.Equal(new[] { "from backup" }, TextsOf(live));
    }

    private string MakeZipFrom(params string[] texts)
    {
        var source = Path.Combine(_root, "src-" + Guid.NewGuid().ToString("N"));
        CreateData(source, "{\"FromBackup\":1}", texts);
        var zip = Path.Combine(_out, Guid.NewGuid().ToString("N") + ".zip");
        LocalBackup.CreateManual(source, zip, "9.9.9", Now);
        return zip;
    }
}
