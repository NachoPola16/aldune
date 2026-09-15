# Aldune Phase 2: Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Phase 1's three hardcoded fake in-memory notes with real, encrypted, persisted notes: SQLite storage, envelope encryption (AES-GCM content key wrapped via Windows DPAPI), CRUD, archive/trash state, and autosave with debounce + flush-on-exit.

**Architecture:** All persistence logic (`Note`/`NoteState`, `ContentCipher`, `DatabaseKeyProvider`, `SettingsService`, `NotesDatabase`, `NotesRepository`) lives in `Aldune.Core` — none of it depends on WPF, only on SQLite (via `Microsoft.Data.Sqlite`) and Windows' DPAPI (via `System.Security.Cryptography.ProtectedData`, which works from any TFM but only functions at runtime on Windows — acceptable since Aldune is Windows-only per the spec). This keeps the whole persistence layer unit-testable against real temp SQLite files and real DPAPI round-trips, with zero UI thread involved. The `Aldune` WPF project only wires this layer into `App.xaml.cs` (bootstrap), `EdgeDockWindow` (load real notes, add a "new note" affordance), and `NoteWindow` (autosave, archive/trash actions).

**Tech Stack:** C# / .NET 10, `Microsoft.Data.Sqlite` (SQLite access), `System.Security.Cryptography.AesGcm` (built into .NET, no package needed — content encryption), `System.Security.Cryptography.ProtectedData` (DPAPI key wrapping), `System.Text.Json` (settings file), xUnit.

**Spec:** `docs/superpowers/specs/2026-08-30-aldune-v1-design.md` (sections: Modelo de datos, Almacenamiento, Manejo de errores)

## Global Constraints

- All persistence code lives in `Aldune.Core` (`net10.0`, no `System.Windows.*` references) — same rule as Phase 1, extended to the new domain.
- Content is encrypted with AES-256-GCM using a per-database key; that key (not each note individually) is wrapped with DPAPI (`DataProtectionScope.CurrentUser`) and stored in the local settings file, per the spec's envelope-encryption design.
- Note state is a single enum (`Active` / `Archived` / `Trashed`) — archiving and trashing are the same mechanism with a different value, not two systems (per spec).
- Autosave has no visible "Save" button; edits persist via a debounced write, and any pending debounced write is flushed immediately on app/window close (per spec).
- No placeholders: every task ends in a concretely testable/verifiable state.

---

### Task 1: Add dependencies; replace the Phase 1 placeholder `NoteModel` with the real `Note`/`NoteState` shape

**Files:**
- Modify: `src/Aldune.Core/Aldune.Core.csproj` (add package references)
- Delete: `src/Aldune.Core/NoteModel.cs`
- Create: `src/Aldune.Core/Note.cs`
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (update `SetNotes`/`OnTabClick` to use `Note` instead of `NoteModel`)
- Modify: `src/Aldune/Windowing/NoteWindow.xaml.cs` (update constructor parameter type)
- Modify: `src/Aldune/App.xaml.cs` (update the 3 fake notes' construction to the new shape — temporary, replaced for real in Task 8)

**Interfaces:**
- Produces: `NoteState` enum (`Active`, `Archived`, `Trashed`); `Note` class with `Guid Id`, `string Text` (mutable), `string Color` (mutable), `DateTimeOffset CreatedAt`, `DateTimeOffset UpdatedAt` (mutable), `NoteState State` (mutable), `string ScreenOrigin` (mutable) — this is the full shape from the spec's Modelo de datos section. `ScreenOrigin` is unused until Phase 3 (multi-monitor) but included now since it's a schema-shape decision, cheaper to make once than to migrate later.
- Consumes (from Phase 1, unchanged): nothing new in this task beyond the rename.

- [ ] **Step 1: Add package references**

```bash
dotnet add src/Aldune.Core/Aldune.Core.csproj package Microsoft.Data.Sqlite
dotnet add src/Aldune.Core/Aldune.Core.csproj package System.Security.Cryptography.ProtectedData
```

- [ ] **Step 2: Delete the Phase 1 placeholder and create the real `Note` shape**

Delete `src/Aldune.Core/NoteModel.cs`.

```csharp
// src/Aldune.Core/Note.cs
namespace Aldune.Core;

public enum NoteState
{
    Active,
    Archived,
    Trashed
}

public sealed class Note
{
    public required Guid Id { get; init; }
    public required string Text { get; set; }
    public required string Color { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public required NoteState State { get; set; }
    public required string ScreenOrigin { get; set; }
}
```

- [ ] **Step 3: Update `EdgeDockWindow.xaml.cs`**

Replace every `NoteModel` reference with `Note`:

```csharp
// src/Aldune/Windowing/EdgeDockWindow.xaml.cs — SetNotes and OnTabClick
public void SetNotes(IReadOnlyList<Note> notes)
{
    TabsList.ItemsSource = notes;
}

private void OnTabClick(object sender, RoutedEventArgs e)
{
    if (sender is FrameworkElement { Tag: Note note })
    {
        var noteWindow = new NoteWindow(note);
        noteWindow.Show();
        NativeMethods.ForceActivate(noteWindow);
    }
}
```

(The XAML's `DataTemplate` binds by property name, not by type, so `EdgeDockWindow.xaml` itself needs no change.)

- [ ] **Step 4: Update `NoteWindow.xaml.cs`**

```csharp
// src/Aldune/Windowing/NoteWindow.xaml.cs
public partial class NoteWindow : Window
{
    public NoteWindow(Note note)
    {
        InitializeComponent();
        TextBody.Text = note.Text;
        Loaded += (_, _) => TextBody.Focus();
    }
}
```

- [ ] **Step 5: Update the 3 fake notes in `App.xaml.cs` to the new shape (temporary — Task 8 replaces this block entirely with real persistence)**

```csharp
// src/Aldune/App.xaml.cs — inside OnStartup, replace the SetNotes(new[] { ... }) call
var now = DateTimeOffset.UtcNow;
dock.SetNotes(new[]
{
    new Note { Id = Guid.NewGuid(), Text = "Primera nota de prueba", Color = "#F5E3B3", CreatedAt = now, UpdatedAt = now, State = NoteState.Active, ScreenOrigin = "primary" },
    new Note { Id = Guid.NewGuid(), Text = "Segunda nota", Color = "#C9E4DE", CreatedAt = now, UpdatedAt = now, State = NoteState.Active, ScreenOrigin = "primary" },
    new Note { Id = Guid.NewGuid(), Text = "Tercera nota con más texto para probar el ajuste", Color = "#F2C6DE", CreatedAt = now, UpdatedAt = now, State = NoteState.Active, ScreenOrigin = "primary" },
});
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build`
Expected: Build succeeds, 0 errors. (Existing `Aldune.Core.Tests` still pass unchanged — this task touches no tested logic.)

Run: `dotnet run --project src/Aldune`, confirm the app still launches and shows the 3 fake notes exactly as in Phase 1 (this task is a pure rename/reshape, behavior must be identical). Terminate the process after confirming.

- [ ] **Step 7: Commit**

```bash
git add src/Aldune.Core/Aldune.Core.csproj src/Aldune.Core/Note.cs src/Aldune/Windowing/EdgeDockWindow.xaml.cs src/Aldune/Windowing/NoteWindow.xaml.cs src/Aldune/App.xaml.cs
git rm src/Aldune.Core/NoteModel.cs
git commit -m "Replace placeholder NoteModel with the real Note/NoteState shape"
```

---

### Task 2: `ContentCipher` — AES-GCM encryption for note text

**Files:**
- Create: `src/Aldune.Core/ContentCipher.cs`
- Test: `tests/Aldune.Core.Tests/ContentCipherTests.cs`

**Interfaces:**
- Produces: `readonly record struct EncryptedContent(byte[] CipherText, byte[] Nonce, byte[] Tag)`; `ContentCipher` class with constructor `ContentCipher(byte[] key)` (32-byte AES-256 key), methods `EncryptedContent Encrypt(string plainText)` and `string Decrypt(EncryptedContent encrypted)`.
- Consumes: nothing (pure crypto given a raw key — key generation/wrapping is Task 3's job, deliberately separated so this class is testable with a fixed, known key with no DPAPI dependency).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/ContentCipherTests.cs
using System.Security.Cryptography;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ContentCipherTests
{
    private static readonly byte[] TestKey = new byte[32]; // all-zero key, fine for a fixed test fixture

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("hola, esto es una nota");
        var decrypted = sut.Decrypt(encrypted);
        Assert.Equal("hola, esto es una nota", decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCipherTextForSamePlainText()
    {
        var sut = new ContentCipher(TestKey);
        var first = sut.Encrypt("misma nota");
        var second = sut.Encrypt("misma nota");
        Assert.NotEqual(Convert.ToBase64String(first.CipherText), Convert.ToBase64String(second.CipherText));
        Assert.NotEqual(Convert.ToBase64String(first.Nonce), Convert.ToBase64String(second.Nonce));
    }

    [Fact]
    public void Decrypt_WithTamperedCipherText_Throws()
    {
        var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("texto original");
        encrypted.CipherText[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted));
    }

    [Fact]
    public void Constructor_WithWrongKeyLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ContentCipher(new byte[16]));
    }

    [Fact]
    public void EncryptThenDecrypt_HandlesEmptyString()
    {
        var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("");
        Assert.Equal("", sut.Decrypt(encrypted));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `ContentCipher`/`EncryptedContent` don't exist yet.

- [ ] **Step 3: Implement `ContentCipher`**

```csharp
// src/Aldune.Core/ContentCipher.cs
using System.Security.Cryptography;
using System.Text;

namespace Aldune.Core;

public readonly record struct EncryptedContent(byte[] CipherText, byte[] Nonce, byte[] Tag);

public sealed class ContentCipher
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBytes = 32;

    private readonly byte[] _key;

    public ContentCipher(byte[] key)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes (AES-256).", nameof(key));
        _key = key;
    }

    public EncryptedContent Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return new EncryptedContent(cipherBytes, nonce, tag);
    }

    public string Decrypt(EncryptedContent encrypted)
    {
        var plainBytes = new byte[encrypted.CipherText.Length];
        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(encrypted.Nonce, encrypted.CipherText, encrypted.Tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 5 new tests pass, plus all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/ContentCipher.cs tests/Aldune.Core.Tests/ContentCipherTests.cs
git commit -m "Add ContentCipher: AES-GCM encryption for note text"
```

---

### Task 3: `DatabaseKeyProvider` — DPAPI key wrapping

**Files:**
- Create: `src/Aldune.Core/DatabaseKeyProvider.cs`
- Test: `tests/Aldune.Core.Tests/DatabaseKeyProviderTests.cs`

**Interfaces:**
- Produces: `static class DatabaseKeyProvider` with `static byte[] GenerateKey()`, `static byte[] Wrap(byte[] rawKey)`, `static byte[] Unwrap(byte[] wrappedKey)`.
- Consumes: nothing.

This runs real DPAPI on the current Windows user session — no mocking, since the dev/CI machine is Windows per the spec's Windows-only constraint, exactly like Phase 1 tested real `WS_EX_NOACTIVATE` interop behavior without mocking Win32.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/DatabaseKeyProviderTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DatabaseKeyProviderTests
{
    [Fact]
    public void GenerateKey_Produces32Bytes()
    {
        var key = DatabaseKeyProvider.GenerateKey();
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void GenerateKey_ProducesDifferentKeysEachCall()
    {
        var first = DatabaseKeyProvider.GenerateKey();
        var second = DatabaseKeyProvider.GenerateKey();
        Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
    }

    [Fact]
    public void WrapThenUnwrap_RoundTrips()
    {
        var rawKey = DatabaseKeyProvider.GenerateKey();
        var wrapped = DatabaseKeyProvider.Wrap(rawKey);
        var unwrapped = DatabaseKeyProvider.Unwrap(wrapped);
        Assert.Equal(Convert.ToBase64String(rawKey), Convert.ToBase64String(unwrapped));
    }

    [Fact]
    public void Wrap_ProducesDifferentBytesThanRawKey()
    {
        var rawKey = DatabaseKeyProvider.GenerateKey();
        var wrapped = DatabaseKeyProvider.Wrap(rawKey);
        Assert.NotEqual(Convert.ToBase64String(rawKey), Convert.ToBase64String(wrapped));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `DatabaseKeyProvider` doesn't exist yet.

- [ ] **Step 3: Implement `DatabaseKeyProvider`**

```csharp
// src/Aldune.Core/DatabaseKeyProvider.cs
using System.Security.Cryptography;

namespace Aldune.Core;

public static class DatabaseKeyProvider
{
    private const int KeySizeBytes = 32;

    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeySizeBytes);

    public static byte[] Wrap(byte[] rawKey) =>
        ProtectedData.Protect(rawKey, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public static byte[] Unwrap(byte[] wrappedKey) =>
        ProtectedData.Unprotect(wrappedKey, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 4 new tests pass, plus all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/DatabaseKeyProvider.cs tests/Aldune.Core.Tests/DatabaseKeyProviderTests.cs
git commit -m "Add DatabaseKeyProvider: DPAPI wrapping for the database content key"
```

---

### Task 4: `SettingsService` — local JSON settings file holding the wrapped key

**Files:**
- Create: `src/Aldune.Core/AppSettings.cs`
- Create: `src/Aldune.Core/SettingsService.cs`
- Test: `tests/Aldune.Core.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `AppSettings` class with `byte[]? WrappedDatabaseKey { get; set; }`; `SettingsService` class with constructor `SettingsService(string settingsPath)`, methods `AppSettings Load()` (returns a fresh `AppSettings` if the file doesn't exist yet) and `void Save(AppSettings settings)`.
- Consumes: nothing.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/SettingsServiceTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"aldune-settings-test-{Guid.NewGuid()}");
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsFreshSettings()
    {
        var sut = new SettingsService(_settingsPath);
        var settings = sut.Load();
        Assert.Null(settings.WrappedDatabaseKey);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var sut = new SettingsService(_settingsPath);
        var key = new byte[] { 1, 2, 3, 4, 5 };
        sut.Save(new AppSettings { WrappedDatabaseKey = key });

        var loaded = sut.Load();
        Assert.Equal(key, loaded.WrappedDatabaseKey);
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var sut = new SettingsService(_settingsPath);
        sut.Save(new AppSettings { WrappedDatabaseKey = new byte[] { 9 } });
        Assert.True(File.Exists(_settingsPath));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `AppSettings`/`SettingsService` don't exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/Aldune.Core/AppSettings.cs
namespace Aldune.Core;

public sealed class AppSettings
{
    public byte[]? WrappedDatabaseKey { get; set; }
}
```

```csharp
// src/Aldune.Core/SettingsService.cs
using System.Text.Json;

namespace Aldune.Core;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(_settingsPath, json);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 3 new tests pass, plus all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/AppSettings.cs src/Aldune.Core/SettingsService.cs tests/Aldune.Core.Tests/SettingsServiceTests.cs
git commit -m "Add SettingsService: local JSON settings file for the wrapped database key"
```

---

### Task 5: `NotesDatabase` — SQLite schema and connection

**Files:**
- Create: `src/Aldune.Core/NotesDatabase.cs`
- Test: `tests/Aldune.Core.Tests/NotesDatabaseTests.cs`

**Interfaces:**
- Produces: `NotesDatabase` class with constructor `NotesDatabase(string databasePath)` (creates the `Note` table if it doesn't exist), method `SqliteConnection OpenConnection()` (returns a new, already-open connection — callers are responsible for disposing it).
- Consumes: `Microsoft.Data.Sqlite`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/NotesDatabaseTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesDatabaseTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-test-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Constructor_CreatesNoteTable()
    {
        var sut = new NotesDatabase(_dbPath);

        using var connection = sut.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Note';";
        var tableName = command.ExecuteScalar() as string;

        Assert.Equal("Note", tableName);
    }

    [Fact]
    public void Constructor_IsIdempotent_DoesNotThrowIfCalledTwiceOnSamePath()
    {
        _ = new NotesDatabase(_dbPath);
        var second = new NotesDatabase(_dbPath); // must not throw "table already exists"

        using var connection = second.OpenConnection();
        Assert.NotNull(connection);
    }

    [Fact]
    public void OpenConnection_ReturnsAlreadyOpenConnection()
    {
        var sut = new NotesDatabase(_dbPath);
        using var connection = sut.OpenConnection();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `NotesDatabase` doesn't exist yet.

- [ ] **Step 3: Implement `NotesDatabase`**

```csharp
// src/Aldune.Core/NotesDatabase.cs
using Microsoft.Data.Sqlite;

namespace Aldune.Core;

public sealed class NotesDatabase
{
    private readonly string _connectionString;

    public NotesDatabase(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        Initialize();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Note (
                Id TEXT PRIMARY KEY NOT NULL,
                EncryptedText BLOB NOT NULL,
                Nonce BLOB NOT NULL,
                Tag BLOB NOT NULL,
                Color TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                State TEXT NOT NULL,
                ScreenOrigin TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 3 new tests pass, plus all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/NotesDatabase.cs tests/Aldune.Core.Tests/NotesDatabaseTests.cs
git commit -m "Add NotesDatabase: SQLite schema and connection management"
```

---

### Task 6: `NotesRepository` — CRUD, wired to database + cipher

**Files:**
- Create: `src/Aldune.Core/NotesRepository.cs`
- Test: `tests/Aldune.Core.Tests/NotesRepositoryTests.cs`

**Interfaces:**
- Consumes: `NotesDatabase` (Task 5), `ContentCipher` (Task 2), `Note`/`NoteState` (Task 1).
- Produces: `NotesRepository` class with constructor `NotesRepository(NotesDatabase database, ContentCipher cipher)`, methods: `Note Create(string initialText, string color, string screenOrigin)`, `IReadOnlyList<Note> GetByState(NoteState state)`, `void UpdateText(Guid id, string newText)`, `void SetState(Guid id, NoteState state)`. These four methods are the complete CRUD surface Task 8/9 need: create, read (filtered by state — Active/Archived/Trashed are three separate queries the UI can call), update text (autosave), and change state (archive/trash/restore, all just `SetState` with a different target).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/NotesRepositoryTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-repo-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryTests()
    {
        var database = new NotesDatabase(_dbPath);
        var cipher = new ContentCipher(new byte[32]); // fixed all-zero test key
        _sut = new NotesRepository(database, cipher);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Create_ThenGetByState_ReturnsTheNoteActive()
    {
        var created = _sut.Create("hola nota", "#F5E3B3", "primary");

        var active = _sut.GetByState(NoteState.Active);

        Assert.Single(active);
        Assert.Equal(created.Id, active[0].Id);
        Assert.Equal("hola nota", active[0].Text);
        Assert.Equal("#F5E3B3", active[0].Color);
        Assert.Equal(NoteState.Active, active[0].State);
        Assert.Equal("primary", active[0].ScreenOrigin);
    }

    [Fact]
    public void Create_EncryptsTextAtRest()
    {
        _sut.Create("texto secreto", "#FFFFFF", "primary");

        var database = new NotesDatabase(_dbPath);
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EncryptedText FROM Note;";
        var storedBytes = (byte[])command.ExecuteScalar()!;
        var storedAsText = System.Text.Encoding.UTF8.GetString(storedBytes);

        Assert.DoesNotContain("texto secreto", storedAsText);
    }

    [Fact]
    public void UpdateText_ChangesTextAndUpdatedAt()
    {
        var created = _sut.Create("texto original", "#FFFFFF", "primary");
        var originalUpdatedAt = created.UpdatedAt;

        _sut.UpdateText(created.Id, "texto editado");

        var reloaded = _sut.GetByState(NoteState.Active)[0];
        Assert.Equal("texto editado", reloaded.Text);
        Assert.True(reloaded.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void SetState_MovesNoteBetweenStateQueries()
    {
        var created = _sut.Create("nota a archivar", "#FFFFFF", "primary");

        _sut.SetState(created.Id, NoteState.Archived);

        Assert.Empty(_sut.GetByState(NoteState.Active));
        var archived = _sut.GetByState(NoteState.Archived);
        Assert.Single(archived);
        Assert.Equal(created.Id, archived[0].Id);
    }

    [Fact]
    public void SetState_ToTrashed_ThenBackToActive_RestoresIt()
    {
        var created = _sut.Create("nota a borrar y restaurar", "#FFFFFF", "primary");

        _sut.SetState(created.Id, NoteState.Trashed);
        Assert.Single(_sut.GetByState(NoteState.Trashed));

        _sut.SetState(created.Id, NoteState.Active);
        Assert.Single(_sut.GetByState(NoteState.Active));
        Assert.Empty(_sut.GetByState(NoteState.Trashed));
    }

    [Fact]
    public void GetByState_OrdersByCreatedAt()
    {
        var first = _sut.Create("primera", "#FFFFFF", "primary");
        System.Threading.Thread.Sleep(10); // ensure a distinct timestamp
        var second = _sut.Create("segunda", "#FFFFFF", "primary");

        var active = _sut.GetByState(NoteState.Active);

        Assert.Equal(first.Id, active[0].Id);
        Assert.Equal(second.Id, active[1].Id);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `NotesRepository` doesn't exist yet.

- [ ] **Step 3: Implement `NotesRepository`**

```csharp
// src/Aldune.Core/NotesRepository.cs
using Microsoft.Data.Sqlite;

namespace Aldune.Core;

public sealed class NotesRepository
{
    private readonly NotesDatabase _database;
    private readonly ContentCipher _cipher;

    public NotesRepository(NotesDatabase database, ContentCipher cipher)
    {
        _database = database;
        _cipher = cipher;
    }

    public Note Create(string initialText, string color, string screenOrigin)
    {
        var now = DateTimeOffset.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Text = initialText,
            Color = color,
            CreatedAt = now,
            UpdatedAt = now,
            State = NoteState.Active,
            ScreenOrigin = screenOrigin
        };

        var encrypted = _cipher.Encrypt(note.Text);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Note (Id, EncryptedText, Nonce, Tag, Color, CreatedAt, UpdatedAt, State, ScreenOrigin)
            VALUES ($id, $text, $nonce, $tag, $color, $createdAt, $updatedAt, $state, $screenOrigin);
            """;
        command.Parameters.AddWithValue("$id", note.Id.ToString());
        command.Parameters.AddWithValue("$text", encrypted.CipherText);
        command.Parameters.AddWithValue("$nonce", encrypted.Nonce);
        command.Parameters.AddWithValue("$tag", encrypted.Tag);
        command.Parameters.AddWithValue("$color", note.Color);
        command.Parameters.AddWithValue("$createdAt", note.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", note.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$state", note.State.ToString());
        command.Parameters.AddWithValue("$screenOrigin", note.ScreenOrigin);
        command.ExecuteNonQuery();

        return note;
    }

    public IReadOnlyList<Note> GetByState(NoteState state)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, EncryptedText, Nonce, Tag, Color, CreatedAt, UpdatedAt, State, ScreenOrigin
            FROM Note WHERE State = $state ORDER BY CreatedAt;
            """;
        command.Parameters.AddWithValue("$state", state.ToString());

        var results = new List<Note>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadNote(reader));
        }
        return results;
    }

    public void UpdateText(Guid id, string newText)
    {
        var encrypted = _cipher.Encrypt(newText);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Note SET EncryptedText = $text, Nonce = $nonce, Tag = $tag, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$text", encrypted.CipherText);
        command.Parameters.AddWithValue("$nonce", encrypted.Nonce);
        command.Parameters.AddWithValue("$tag", encrypted.Tag);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    public void SetState(Guid id, NoteState state)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Note SET State = $state, UpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private Note ReadNote(SqliteDataReader reader)
    {
        var cipherText = (byte[])reader["EncryptedText"];
        var nonce = (byte[])reader["Nonce"];
        var tag = (byte[])reader["Tag"];
        var text = _cipher.Decrypt(new EncryptedContent(cipherText, nonce, tag));

        return new Note
        {
            Id = Guid.Parse((string)reader["Id"]),
            Text = text,
            Color = (string)reader["Color"],
            CreatedAt = DateTimeOffset.Parse((string)reader["CreatedAt"]),
            UpdatedAt = DateTimeOffset.Parse((string)reader["UpdatedAt"]),
            State = Enum.Parse<NoteState>((string)reader["State"]),
            ScreenOrigin = (string)reader["ScreenOrigin"]
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 6 new tests pass, plus all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/NotesRepository.cs tests/Aldune.Core.Tests/NotesRepositoryTests.cs
git commit -m "Add NotesRepository: encrypted CRUD over SQLite"
```

---

### Task 7: Startup error handling — corrupt database and unavailable DPAPI key

**Files:**
- Create: `src/Aldune.Core/DatabaseCorruptionGuard.cs`
- Test: `tests/Aldune.Core.Tests/DatabaseCorruptionGuardTests.cs`

**Interfaces:**
- Produces: `static class DatabaseCorruptionGuard` with `static bool IsValidSqliteFile(string path)` — checks the first 16 bytes of a file against SQLite's fixed magic header (`"SQLite format 3\0"`), returning `false` for a missing, empty, or non-SQLite file. `static void BackupAndRemove(string path)` — copies a corrupt file to `<path>.corrupt-<timestamp>` and deletes the original, so `NotesDatabase`'s next construction creates a fresh empty database instead of failing.
- Consumes: nothing beyond the filesystem.

This task implements the spec's "base de datos corrupta/ilegible al arrancar: se copia el archivo dañado antes de tocar nada, y se ofrece crear una base nueva vacía" requirement. The DPAPI-unavailable half of the spec's error-handling section (distinguishing a damaged profile from a password reset) is a user-facing message, not new logic — it's wired into `App.xaml.cs` in Task 8 by catching the `CryptographicException` that `DatabaseKeyProvider.Unwrap` throws when DPAPI can't decrypt, and is not independently unit-testable (it requires an actually-undecryptable DPAPI blob, which isn't reproducible without a second Windows user profile) — Task 8's manual verification checklist covers it instead.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Aldune.Core.Tests/DatabaseCorruptionGuardTests.cs
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DatabaseCorruptionGuardTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aldune-corrupt-test-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_path)}*"))
            File.Delete(file);
    }

    [Fact]
    public void IsValidSqliteFile_WhenFileDoesNotExist_ReturnsFalse()
    {
        Assert.False(DatabaseCorruptionGuard.IsValidSqliteFile(_path));
    }

    [Fact]
    public void IsValidSqliteFile_WhenFileIsGarbage_ReturnsFalse()
    {
        File.WriteAllText(_path, "esto no es una base de datos SQLite");
        Assert.False(DatabaseCorruptionGuard.IsValidSqliteFile(_path));
    }

    [Fact]
    public void IsValidSqliteFile_WhenFileIsRealSqliteDatabase_ReturnsTrue()
    {
        _ = new NotesDatabase(_path); // creates a real, valid SQLite file
        Assert.True(DatabaseCorruptionGuard.IsValidSqliteFile(_path));
    }

    [Fact]
    public void BackupAndRemove_MovesTheOriginalFileAside()
    {
        File.WriteAllText(_path, "datos corruptos");

        DatabaseCorruptionGuard.BackupAndRemove(_path);

        Assert.False(File.Exists(_path));
        var backups = Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_path)}.corrupt-*");
        Assert.Single(backups);
        Assert.Equal("datos corruptos", File.ReadAllText(backups[0]));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: Fails to compile — `DatabaseCorruptionGuard` doesn't exist yet.

- [ ] **Step 3: Implement `DatabaseCorruptionGuard`**

```csharp
// src/Aldune.Core/DatabaseCorruptionGuard.cs
namespace Aldune.Core;

public static class DatabaseCorruptionGuard
{
    private static readonly byte[] SqliteMagicHeader = "SQLite format 3\0"u8.ToArray();

    public static bool IsValidSqliteFile(string path)
    {
        if (!File.Exists(path)) return false;

        using var stream = File.OpenRead(path);
        if (stream.Length < SqliteMagicHeader.Length) return false;

        var header = new byte[SqliteMagicHeader.Length];
        var bytesRead = stream.Read(header, 0, header.Length);
        return bytesRead == header.Length && header.SequenceEqual(SqliteMagicHeader);
    }

    public static void BackupAndRemove(string path)
    {
        var backupPath = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        File.Copy(path, backupPath);
        File.Delete(path);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Aldune.Core.Tests`
Expected: All 4 new tests pass, plus all pre-existing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/DatabaseCorruptionGuard.cs tests/Aldune.Core.Tests/DatabaseCorruptionGuardTests.cs
git commit -m "Add DatabaseCorruptionGuard: detect and quarantine corrupt SQLite files"
```

---

### Task 8: Wire real persistence into the app; add a way to create a note

**Files:**
- Modify: `src/Aldune/App.xaml.cs` (bootstrap: settings → key → cipher → database → repository, replacing the 3 fake notes with a real repository-backed load)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml` (add a "+" button to the tabs list for creating a new note)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (wire the "+" button to `NotesRepository.Create`, refresh `SetNotes` afterward)

**Interfaces:**
- Consumes: `SettingsService`/`AppSettings` (Task 4), `DatabaseKeyProvider` (Task 3), `ContentCipher` (Task 2), `NotesDatabase`/`NotesRepository` (Tasks 5-6), `DatabaseCorruptionGuard` (Task 7).
- Produces: a real, working app where notes persist across restarts. `EdgeDockWindow` gains a `NotesRepository` reference so its "+" button and (Task 9's) archive/trash actions can call it directly, and a `Refresh()` method that reloads `SetNotes(repository.GetByState(NoteState.Active))`.

- [ ] **Step 1: Rewrite `App.xaml.cs`'s bootstrap**

```csharp
// src/Aldune/App.xaml.cs
using System.IO;
using System.Windows;
using Aldune.Core;
using Aldune.Windowing;

namespace Aldune;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aldune");
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        var databasePath = Path.Combine(appDataDir, "notes.db");

        if (DatabaseCorruptionGuard.IsValidSqliteFile(databasePath) is false && File.Exists(databasePath))
        {
            DatabaseCorruptionGuard.BackupAndRemove(databasePath);
        }

        var settingsService = new SettingsService(settingsPath);
        var settings = settingsService.Load();

        byte[] rawKey;
        if (settings.WrappedDatabaseKey is null)
        {
            rawKey = DatabaseKeyProvider.GenerateKey();
            settings.WrappedDatabaseKey = DatabaseKeyProvider.Wrap(rawKey);
            settingsService.Save(settings);
        }
        else
        {
            rawKey = DatabaseKeyProvider.Unwrap(settings.WrappedDatabaseKey);
        }

        var cipher = new ContentCipher(rawKey);
        var database = new NotesDatabase(databasePath);
        var repository = new NotesRepository(database, cipher);

        var area = SystemParameters.WorkArea;
        var workingArea = new WorkingArea(area.Left, area.Top, area.Width, area.Height);

        var dock = new EdgeDockWindow(EdgePosition.Right, workingArea, repository);
        dock.Refresh();
        dock.Show();
    }
}
```

- [ ] **Step 2: Add a "+" tab to `EdgeDockWindow.xaml`**

```xml
<!-- src/Aldune/Windowing/EdgeDockWindow.xaml -->
<Window x:Class="Aldune.Windowing.EdgeDockWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="False"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="NoResize"
        Background="#3A3A3A">
    <StackPanel>
        <ItemsControl x:Name="TabsList">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Button Content="{Binding Text}"
                            Background="{Binding Color}"
                            Margin="4"
                            Padding="6"
                            Click="OnTabClick"
                            Tag="{Binding}" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <Button Content="+ Nueva nota" Margin="4" Padding="6" Click="OnNewNoteClick" />
    </StackPanel>
</Window>
```

- [ ] **Step 3: Wire the repository into `EdgeDockWindow.xaml.cs`**

```csharp
// src/Aldune/Windowing/EdgeDockWindow.xaml.cs
public partial class EdgeDockWindow : Window
{
    private readonly FanStateMachine _fanState = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly EdgePosition _edge;
    private readonly WorkingArea _workingArea;
    private readonly NotesRepository _repository;

    public EdgeDockWindow(EdgePosition edge, WorkingArea workingArea, NotesRepository repository)
    {
        InitializeComponent();
        _edge = edge;
        _workingArea = workingArea;
        _repository = repository;

        // ... existing constructor body (timer, fan state wiring, MouseEnter/MouseLeave,
        // SourceInitialized, ApplyGeometry()) is unchanged — only the new _repository field
        // and the parameter above are added.
    }

    public void Refresh()
    {
        SetNotes(_repository.GetByState(NoteState.Active));
    }

    public void SetNotes(IReadOnlyList<Note> notes)
    {
        TabsList.ItemsSource = notes;
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
        {
            var noteWindow = new NoteWindow(note, _repository, this);
            noteWindow.Show();
            NativeMethods.ForceActivate(noteWindow);
        }
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        _repository.Create(string.Empty, "#F5E3B3", screenOrigin: "primary");
        Refresh();
    }
}
```

(`NoteWindow`'s constructor gains `NotesRepository` and a reference back to the `EdgeDockWindow` in Task 9, for autosave and archive/trash — this task only adds the repository/`Refresh()`/"+" plumbing; Task 9 extends `NoteWindow`'s constructor signature, so this step's `OnTabClick` snippet above anticipates that signature to avoid a second edit to this method in Task 9.)

- [ ] **Step 4: Build and manual verification**

Run: `dotnet build`
Expected: Build fails at this point specifically because `NoteWindow`'s constructor doesn't yet accept `(Note, NotesRepository, EdgeDockWindow)` — Task 9 completes it. **Do not attempt to make this task's build green by guessing Task 9's `NoteWindow` changes** — instead, temporarily stub `NoteWindow`'s constructor to accept and ignore the two extra parameters (`public NoteWindow(Note note, NotesRepository repository, EdgeDockWindow owner)`, storing nothing new yet) so this task's own build passes cleanly, and let Task 9 give the real implementation using those fields. Confirm `dotnet build` succeeds with this stub in place.

Run: `dotnet run --project src/Aldune`. Click "+ Nueva nota" a couple of times, confirm new (empty) tabs appear. Close the app, delete nothing, run it again — confirm the notes created are still there (real persistence, not the old fake in-memory list). Terminate cleanly.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune/App.xaml.cs src/Aldune/Windowing/EdgeDockWindow.xaml src/Aldune/Windowing/EdgeDockWindow.xaml.cs src/Aldune/Windowing/NoteWindow.xaml.cs
git commit -m "Wire real persistence into the app; add note creation"
```

---

### Task 9: Autosave and archive/trash actions on `NoteWindow`

**Files:**
- Modify: `src/Aldune/Windowing/NoteWindow.xaml` (add "Archivar" and "Papelera" buttons)
- Modify: `src/Aldune/Windowing/NoteWindow.xaml.cs` (debounced autosave, flush-on-exit, archive/trash actions)

**Interfaces:**
- Consumes: `NotesRepository` (Task 6), `EdgeDockWindow.Refresh()` (Task 8, to update the dock's tab list after an archive/trash action removes a note from the Active view).
- Produces: `NoteWindow(Note note, NotesRepository repository, EdgeDockWindow owner)` — the real, final constructor signature (completing Task 8's stub).

- [ ] **Step 1: Add the action buttons to `NoteWindow.xaml`**

```xml
<!-- src/Aldune/Windowing/NoteWindow.xaml -->
<Window x:Class="Aldune.Windowing.NoteWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Aldune"
        Width="260" Height="300"
        WindowStyle="ToolWindow"
        Topmost="True">
    <DockPanel>
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="4">
            <Button Content="Archivar" Click="OnArchiveClick" Margin="0,0,4,0" Padding="6,2" />
            <Button Content="Papelera" Click="OnTrashClick" Padding="6,2" />
        </StackPanel>
        <TextBox x:Name="TextBody"
                 AcceptsReturn="True"
                 TextWrapping="Wrap"
                 Padding="8"
                 BorderThickness="0" />
    </DockPanel>
</Window>
```

- [ ] **Step 2: Implement debounced autosave, flush-on-exit, and archive/trash**

```csharp
// src/Aldune/Windowing/NoteWindow.xaml.cs
using System.Windows;
using System.Windows.Threading;
using Aldune.Core;

namespace Aldune.Windowing;

public partial class NoteWindow : Window
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly Note _note;
    private readonly NotesRepository _repository;
    private readonly EdgeDockWindow _owner;
    private readonly DispatcherTimer _autosaveTimer;
    private bool _hasPendingEdit;

    public NoteWindow(Note note, NotesRepository repository, EdgeDockWindow owner)
    {
        InitializeComponent();
        _note = note;
        _repository = repository;
        _owner = owner;

        TextBody.Text = note.Text;
        Loaded += (_, _) => TextBody.Focus();

        _autosaveTimer = new DispatcherTimer { Interval = AutosaveDelay };
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer.Stop();
            Flush();
        };

        TextBody.TextChanged += (_, _) =>
        {
            _hasPendingEdit = true;
            _autosaveTimer.Stop();
            _autosaveTimer.Start();
        };

        Closing += (_, _) => Flush();
    }

    private void Flush()
    {
        if (!_hasPendingEdit) return;
        _hasPendingEdit = false;
        _repository.UpdateText(_note.Id, TextBody.Text);
    }

    private void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        Flush();
        _repository.SetState(_note.Id, NoteState.Archived);
        _owner.Refresh();
        Close();
    }

    private void OnTrashClick(object sender, RoutedEventArgs e)
    {
        _hasPendingEdit = false; // discard any pending edit — the note is being trashed, not saved
        _repository.SetState(_note.Id, NoteState.Trashed);
        _owner.Refresh();
        Close();
    }
}
```

- [ ] **Step 3: Build and manual verification**

Run: `dotnet build`
Expected: Build succeeds — this completes the `NoteWindow` constructor Task 8 stubbed out.

Run: `dotnet run --project src/Aldune`. Full manual checklist:
1. Open a note, type something, close the window (via the × button) without clicking Archivar/Papelera — reopen the app (restart it) and confirm the edit was saved (flush-on-exit works).
2. Open a note, type something, wait ~1 second without closing, then check (e.g. by looking at the app's data file timestamp, or by force-closing and reopening) that the edit was already saved before you closed anything (debounced autosave works, not just flush-on-exit).
3. Click "Archivar" on a note — it disappears from the dock's tab list (Active view). (There's no "show archived" UI yet — that's a later phase; for now, confirming it *leaves* the active list is enough.)
4. Click "Papelera" on a note — same check, it disappears from the dock's tab list.
5. Click "+ Nueva nota" a few times, confirm each gets its own independent tab and can be edited independently.

- [ ] **Step 4: Commit**

```bash
git add src/Aldune/Windowing/NoteWindow.xaml src/Aldune/Windowing/NoteWindow.xaml.cs
git commit -m "Add debounced autosave, flush-on-exit, and archive/trash actions"
```

---

## Self-Review Notes

- **Spec coverage** (Phase 2 slice, per "Orden de implementación sugerido"): SQLite storage ✓, envelope encryption (AES-GCM content key wrapped via DPAPI, key stored in settings not per-note) ✓, CRUD ✓, Active/Archived/Trashed as one state field ✓, autosave with debounce ✓, flush-on-exit ✓, corrupt-database detection and quarantine ✓. Deliberately out of this plan (later phases per the spec): import/export, multi-monitor, tray/hotkey/autostart, checkbox rendering, a "view archived/trashed notes" browser UI (Archivar/Papelera buttons only *remove* a note from the Active view for now — restoring or browsing those states is not yet exposed in the UI, since no phase has scoped that screen yet; `NotesRepository.SetState` already supports restoring by calling it with `NoteState.Active`, so the data-layer capability exists even though Phase 2 exposes no button for it).
- **Placeholder scan:** no TBD/TODO; every step has real, complete code. Task 8's intentionally-temporary `NoteWindow` constructor stub is explicitly flagged as such, with Task 9 completing it — this is a deliberate two-task split of one interface change (same pattern Phase 1 used for multi-task file edits), not an unresolved placeholder.
- **Type consistency:** `Note`/`NoteState` (Task 1) match their use in `ContentCipher`-adjacent code, `NotesRepository` (Task 6), and the WPF layer (Tasks 8-9). `NotesRepository`'s 4 methods (Task 6) match exactly what Tasks 8-9 call (`Create`, `GetByState`, `UpdateText`, `SetState`) — no method invented in a later task that an earlier task didn't produce. `NoteWindow`'s final constructor signature (Task 9) matches the stub Task 8 introduces and the call site Task 8's `OnTabClick` already uses.
