# Recordatorios con notificación de Windows — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a one-shot, per-note reminder that notifies via a native Windows
balloon tip (Action Center) when it's due, with quick presets, a manual
date/time picker, and a small badge on the dock tab.

**Architecture:** A new `NoteReminder` table (one row per note, `NoteId`
primary key) holds the due timestamp. `Aldune.Core.NotesRepository` gets
CRUD methods for it, plus `GetById` (missing until now). A new
`Aldune.Windowing.ReminderScheduler` polls every 30s via a `DispatcherTimer`
and does one catch-up pass at startup, firing `NotifyIcon.ShowBalloonTip` on
the tray icon `TrayIcon` already owns. `NoteWindow` gets a "Recordatorio"
entry in its "⋯" menu with an inline picker (three presets + calendar/time).
`EdgeDockWindow` paints a small clock badge on tabs with a pending
reminder.

**Tech Stack:** C#/.NET 10, WPF, `Microsoft.Data.Sqlite`,
`System.Windows.Forms.NotifyIcon` (already a dependency via
`UseWindowsForms`), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-11-aldune-reminders-design.md`

## Global Constraints

- No migrations: new tables only, via `CREATE TABLE IF NOT EXISTS` in
  `NotesDatabase.Initialize`. Never add a column to `Note`.
- All timestamps stored in the database are ISO-8601 UTC (`DateTimeOffset.
  ToString("O")` after `.ToUniversalTime()`), same convention as `CreatedAt`/
  `UpdatedAt`. UI-facing computation (presets, the picker) works in local
  time (`DateTimeOffset.Now`); conversion to UTC happens once, at the
  repository boundary.
- Reminders are per-note, one active at a time (`PRIMARY KEY NoteId`), and
  one-shot: `ReminderScheduler` clears a reminder's row the moment it fires.
- Core additions are TDD with xUnit, following the existing
  `NotesRepositoryTaskCompletionTests`-style pattern (temp SQLite file per
  test class, `IDisposable` cleanup, all-zero 32-byte test cipher key). WPF
  layer changes (scheduler wiring, `NoteWindow`/`EdgeDockWindow` UI) are not
  unit tested — same pattern as the rest of that layer in this project —
  verified manually per task.
- Run `dotnet build` then `dotnet test` from the repo root after every task;
  do not move to the next task with a red build or a failing test.

---

### Task 1: `NoteReminder` table + core repository methods

**Files:**
- Modify: `src/Aldune.Core/NotesDatabase.cs:41-68` (add table to the
  `CREATE TABLE` block)
- Modify: `src/Aldune.Core/NotesRepository.cs` (add methods; extend
  `Delete` and `PurgeExpiredTrash`)
- Test: `tests/Aldune.Core.Tests/NotesRepositoryReminderTests.cs` (new)

**Interfaces:**
- Produces (used by Task 2 is unrelated; used directly by Task 3 and Task 4):
  - `NotesRepository.SetReminder(Guid noteId, DateTimeOffset dueAt) -> void`
  - `NotesRepository.ClearReminder(Guid noteId) -> void`
  - `NotesRepository.GetReminder(Guid noteId) -> DateTimeOffset?`
  - `NotesRepository.GetDueReminders(DateTimeOffset now) -> IReadOnlyList<(Guid NoteId, DateTimeOffset DueAt)>`
  - `NotesRepository.GetPendingReminders() -> IReadOnlyDictionary<Guid, DateTimeOffset>`
  - `NotesRepository.GetById(Guid id) -> Note?`

- [ ] **Step 1: Write the failing tests**

Create `tests/Aldune.Core.Tests/NotesRepositoryReminderTests.cs`:

```csharp
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryReminderTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-reminder-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryReminderTests()
    {
        var database = new NotesDatabase(_dbPath);
        var cipher = new ContentCipher(new byte[32]); // fixed all-zero test key
        _sut = new NotesRepository(database, cipher);
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);

        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void SetReminder_ThenGetReminder_RoundTrips()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        var dueAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

        _sut.SetReminder(note.Id, dueAt);

        Assert.Equal(dueAt, _sut.GetReminder(note.Id));
    }

    [Fact]
    public void SetReminder_WithLocalOffset_StoresTheSameInstant()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        var dueAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(2)); // e.g. CEST

        _sut.SetReminder(note.Id, dueAt);

        Assert.Equal(dueAt, _sut.GetReminder(note.Id)); // DateTimeOffset equality compares the instant
    }

    [Fact]
    public void SetReminder_CalledTwice_ReplacesInsteadOfAccumulating()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddDays(1));

        var newer = DateTimeOffset.UtcNow.AddDays(2);
        _sut.SetReminder(note.Id, newer);

        Assert.Equal(newer, _sut.GetReminder(note.Id));
        Assert.Single(_sut.GetPendingReminders());
    }

    [Fact]
    public void GetReminder_WithNoneSet_ReturnsNull()
    {
        var note = _sut.Create("sin recordatorio", "#F5E3B3", "primary");
        Assert.Null(_sut.GetReminder(note.Id));
    }

    [Fact]
    public void ClearReminder_RemovesIt()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));

        _sut.ClearReminder(note.Id);

        Assert.Null(_sut.GetReminder(note.Id));
    }

    [Fact]
    public void GetDueReminders_ReturnsOnlyThoseAtOrBeforeNow()
    {
        var overdue = _sut.Create("vencida", "#F5E3B3", "primary");
        var future = _sut.Create("futura", "#F5E3B3", "primary");
        var now = DateTimeOffset.UtcNow;
        _sut.SetReminder(overdue.Id, now.AddMinutes(-5));
        _sut.SetReminder(future.Id, now.AddHours(1));

        var due = _sut.GetDueReminders(now);

        var dueId = Assert.Single(due).NoteId;
        Assert.Equal(overdue.Id, dueId);
    }

    [Fact]
    public void GetPendingReminders_ReturnsAllActiveOnes()
    {
        var a = _sut.Create("a", "#F5E3B3", "primary");
        var b = _sut.Create("b", "#F5E3B3", "primary");
        _sut.SetReminder(a.Id, DateTimeOffset.UtcNow.AddHours(1));
        _sut.SetReminder(b.Id, DateTimeOffset.UtcNow.AddDays(1));

        var pending = _sut.GetPendingReminders();

        Assert.Equal(2, pending.Count);
        Assert.True(pending.ContainsKey(a.Id));
        Assert.True(pending.ContainsKey(b.Id));
    }

    [Fact]
    public void Delete_RemovesTheReminderToo()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));

        _sut.Delete(note.Id);

        Assert.Empty(_sut.GetPendingReminders());
    }

    [Fact]
    public void PurgeExpiredTrash_RemovesTheReminderOfThePurgedNote()
    {
        var note = _sut.Create("vieja", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));
        _sut.SetState(note.Id, NoteState.Trashed);
        BackdateUpdatedAt(note.Id, DateTimeOffset.UtcNow.AddDays(-31));

        _sut.PurgeExpiredTrash(TimeSpan.FromDays(30));

        Assert.Empty(_sut.GetPendingReminders());
    }

    // Mismo patrón que NotesRepositoryTests.BackdateUpdatedAt: PurgeExpiredTrash corta por
    // UpdatedAt, así que hay que poder ponerlo en el pasado sin esperar de verdad.
    private void BackdateUpdatedAt(Guid id, DateTimeOffset updatedAt)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Note SET UpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    [Fact]
    public void GetById_ReturnsTheDecryptedNote()
    {
        var note = _sut.Create("mi nota", "#F5E3B3", "primary");

        var found = _sut.GetById(note.Id);

        Assert.NotNull(found);
        Assert.Equal("mi nota", found!.Text);
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsNull()
    {
        Assert.Null(_sut.GetById(Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~NotesRepositoryReminderTests"`
Expected: FAIL to compile — `SetReminder`, `ClearReminder`, `GetReminder`,
`GetDueReminders`, `GetPendingReminders`, `GetById` don't exist yet.

- [ ] **Step 3: Add the `NoteReminder` table**

In `src/Aldune.Core/NotesDatabase.cs`, inside `Initialize()`'s
`command.CommandText`, right after the `TaskCompletion` table (currently
ends at line 68, just before the closing `"""`), add:

```sql

            -- Recordatorio puntual (no recurrente) de una nota, como mucho uno activo a la vez —
            -- de ahí NoteId como clave primaria en vez de una compuesta o un Id propio: poner uno
            -- nuevo reemplaza cualquiera anterior. Aparte de Note por el mismo motivo que las demás:
            -- esa tabla tiene el contenido real del usuario y no hay migraciones. Se borra la fila
            -- al dispararse (ver Aldune.Windowing.ReminderScheduler) o al cancelarse a mano.
            CREATE TABLE IF NOT EXISTS NoteReminder (
                NoteId TEXT PRIMARY KEY NOT NULL,
                DueAt TEXT NOT NULL
            );
```

- [ ] **Step 4: Add the repository methods**

In `src/Aldune.Core/NotesRepository.cs`, add after `DeletePlacement` (the
method ending at line 308):

```csharp

    /// <summary>
    /// Pone (o reemplaza) el recordatorio puntual de una nota. Se guarda siempre convertido a UTC,
    /// aunque quien llama trabaje en hora local (el selector de la nota, los atajos de
    /// <see cref="ReminderPresets"/>) — misma convención que <c>CreatedAt</c>/<c>UpdatedAt</c>.
    /// </summary>
    public void SetReminder(Guid noteId, DateTimeOffset dueAt)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO NoteReminder (NoteId, DueAt) VALUES ($noteId, $dueAt);
            """;
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$dueAt", dueAt.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>Quita el recordatorio de una nota, si tenía uno. No falla si no tenía ninguno.</summary>
    public void ClearReminder(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM NoteReminder WHERE NoteId = $noteId;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.ExecuteNonQuery();
    }

    /// <summary>El recordatorio activo de una nota, o null si no tiene ninguno — para pintar el
    /// estado actual en el menú "⋯" de la nota abierta.</summary>
    public DateTimeOffset? GetReminder(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DueAt FROM NoteReminder WHERE NoteId = $noteId;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());

        var result = command.ExecuteScalar();
        return result is null
            ? null
            : DateTimeOffset.Parse(
                (string)result,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
    }

    /// <summary>Recordatorios vencidos a <paramref name="now"/> (inclusive) — usado tanto por el
    /// sondeo periódico como por el catch-up al arrancar (ver Aldune.Windowing.ReminderScheduler).
    /// No los borra: quien llama decide cuándo limpiarlos (ClearReminder), después de avisar.</summary>
    public IReadOnlyList<(Guid NoteId, DateTimeOffset DueAt)> GetDueReminders(DateTimeOffset now)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NoteId, DueAt FROM NoteReminder WHERE DueAt <= $now;";
        command.Parameters.AddWithValue("$now", now.ToUniversalTime().ToString("O"));

        var results = new List<(Guid, DateTimeOffset)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                Guid.Parse((string)reader["NoteId"]),
                DateTimeOffset.Parse(
                    (string)reader["DueAt"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return results;
    }

    /// <summary>Todos los recordatorios activos, para pintar el indicador en el dock sin una consulta
    /// por nota (ver EdgeDockWindow.SetNotes).</summary>
    public IReadOnlyDictionary<Guid, DateTimeOffset> GetPendingReminders()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NoteId, DueAt FROM NoteReminder;";

        var results = new Dictionary<Guid, DateTimeOffset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[Guid.Parse((string)reader["NoteId"])] = DateTimeOffset.Parse(
                (string)reader["DueAt"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }
        return results;
    }

    /// <summary>Una nota por Id, descifrada — para cuando solo se tiene el Guid (p. ej.
    /// ReminderScheduler, que solo conoce el NoteId de un recordatorio vencido) y no la lista
    /// completa que ya da GetByState.</summary>
    public Note? GetById(Guid id)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, EncryptedText, Nonce, Tag, Color, CreatedAt, UpdatedAt, State, ScreenOrigin
            FROM Note WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadNote(reader) : null;
    }
```

- [ ] **Step 5: Wire `NoteReminder` into `Delete` and `PurgeExpiredTrash`**

In `src/Aldune.Core/NotesRepository.cs`, `Delete` (currently):

```csharp
        command.CommandText = """
            DELETE FROM Note WHERE Id = $id;
            DELETE FROM NotePlacement WHERE NoteId = $id;
            DELETE FROM TaskCompletion WHERE NoteId = $id;
            """;
```

becomes:

```csharp
        command.CommandText = """
            DELETE FROM Note WHERE Id = $id;
            DELETE FROM NotePlacement WHERE NoteId = $id;
            DELETE FROM TaskCompletion WHERE NoteId = $id;
            DELETE FROM NoteReminder WHERE NoteId = $id;
            """;
```

And `PurgeExpiredTrash` (currently):

```csharp
        command.CommandText = """
            DELETE FROM NotePlacement WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM Note WHERE State = $state AND UpdatedAt < $cutoff;
            """;
```

becomes:

```csharp
        command.CommandText = """
            DELETE FROM NotePlacement WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM NoteReminder WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM Note WHERE State = $state AND UpdatedAt < $cutoff;
            """;
```

(The two `DELETE`s reading from `Note` must run before the final `DELETE FROM Note`, since the subquery needs the row to still exist.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~NotesRepositoryReminderTests"`
Expected: PASS, all 11 tests.

Run: `dotnet test`
Expected: PASS, all tests (no regressions in `NotesRepositoryTests`/
`NotesRepositoryTaskCompletionTests` from the `Delete`/`PurgeExpiredTrash`
changes).

- [ ] **Step 7: Commit**

```bash
git add src/Aldune.Core/NotesDatabase.cs src/Aldune.Core/NotesRepository.cs tests/Aldune.Core.Tests/NotesRepositoryReminderTests.cs
git commit -m "Add NoteReminder table and repository CRUD for note reminders"
```

---

### Task 2: `Aldune.Core.ReminderPresets` (pure)

**Files:**
- Create: `src/Aldune.Core/ReminderPresets.cs`
- Test: `tests/Aldune.Core.Tests/ReminderPresetsTests.cs` (new)

**Interfaces:**
- Consumes: nothing (pure, no dependency on Task 1).
- Produces (used by Task 4):
  - `ReminderPresets.InOneHour(DateTimeOffset now) -> DateTimeOffset`
  - `ReminderPresets.Tonight(DateTimeOffset now) -> DateTimeOffset`
  - `ReminderPresets.TomorrowMorning(DateTimeOffset now) -> DateTimeOffset`

- [ ] **Step 1: Write the failing tests**

Create `tests/Aldune.Core.Tests/ReminderPresetsTests.cs`:

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ReminderPresetsTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Fact]
    public void InOneHour_IsAlwaysNowPlusOneHour()
    {
        var now = new DateTimeOffset(2026, 9, 11, 15, 30, 0, Offset);
        Assert.Equal(now.AddHours(1), ReminderPresets.InOneHour(now));
    }

    [Fact]
    public void Tonight_BeforeEightPm_IsTodayAtEight()
    {
        var now = new DateTimeOffset(2026, 9, 11, 14, 0, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 11, 20, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.Tonight(now));
    }

    [Fact]
    public void Tonight_AtExactlyEightPm_IsTomorrowAtEight()
    {
        var now = new DateTimeOffset(2026, 9, 11, 20, 0, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 12, 20, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.Tonight(now));
    }

    [Fact]
    public void Tonight_AfterEightPm_IsTomorrowAtEight()
    {
        var now = new DateTimeOffset(2026, 9, 11, 23, 45, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 12, 20, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.Tonight(now));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 59)]
    [InlineData(23, 0)]
    public void TomorrowMorning_IsAlwaysTheNextDayAtNine_RegardlessOfCurrentTime(int hour, int minute)
    {
        var now = new DateTimeOffset(2026, 9, 11, hour, minute, 0, Offset);
        var expected = new DateTimeOffset(2026, 9, 12, 9, 0, 0, Offset);
        Assert.Equal(expected, ReminderPresets.TomorrowMorning(now));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ReminderPresetsTests"`
Expected: FAIL to compile — `ReminderPresets` doesn't exist yet.

- [ ] **Step 3: Implement `ReminderPresets`**

Create `src/Aldune.Core/ReminderPresets.cs`:

```csharp
namespace Aldune.Core;

/// <summary>
/// Los tres atajos rápidos del selector de recordatorio de <c>NoteWindow</c>. Puro y parametrizado
/// por <c>now</c> (en vez de leer <c>DateTimeOffset.Now</c> internamente) para poder testear "Esta
/// noche" a los dos lados del límite de las 20:00 sin depender del reloj real — ver
/// docs/superpowers/specs/2026-09-11-aldune-reminders-design.md.
///
/// Todo se calcula en la hora local de <paramref name="now"/> (mismo <c>Offset</c> que trae), no en
/// UTC: quien llama (NoteWindow) trabaja con <c>DateTimeOffset.Now</c>, y la conversión a UTC pasa a
/// ocurrir una sola vez, en <see cref="NotesRepository.SetReminder"/>.
/// </summary>
public static class ReminderPresets
{
    private const int TonightHour = 20;
    private const int TomorrowMorningHour = 9;

    public static DateTimeOffset InOneHour(DateTimeOffset now) => now.AddHours(1);

    /// <summary>Hoy a las 20:00 si aún no han dado las 20:00; si ya son las 20:00 en punto o más
    /// tarde, mañana a las 20:00 — nunca propone una hora que ya pasó hoy.</summary>
    public static DateTimeOffset Tonight(DateTimeOffset now)
    {
        var eightPm = new DateTimeOffset(now.Year, now.Month, now.Day, TonightHour, 0, 0, now.Offset);
        return now < eightPm ? eightPm : eightPm.AddDays(1);
    }

    /// <summary>El día siguiente a las 9:00, sin importar la hora de <paramref name="now"/>.</summary>
    public static DateTimeOffset TomorrowMorning(DateTimeOffset now)
    {
        var tomorrow = now.AddDays(1);
        return new DateTimeOffset(tomorrow.Year, tomorrow.Month, tomorrow.Day, TomorrowMorningHour, 0, 0, now.Offset);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ReminderPresetsTests"`
Expected: PASS, all 7 tests (4 `[Fact]` + 3 `[Theory]` cases).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/ReminderPresets.cs tests/Aldune.Core.Tests/ReminderPresetsTests.cs
git commit -m "Add ReminderPresets for the reminder picker's quick shortcuts"
```

---

### Task 3: `ReminderScheduler` + tray/coordinator wiring

**Files:**
- Modify: `src/Aldune/Windowing/TrayIcon.cs` (expose the `NotifyIcon`)
- Modify: `src/Aldune/Windowing/AppCoordinator.cs` (add `OpenNoteById`)
- Create: `src/Aldune/Windowing/ReminderScheduler.cs`
- Modify: `src/Aldune/Resources/Strings.cs` (two new strings)
- Modify: `src/Aldune/App.xaml.cs` (construct and run the catch-up pass)

**Interfaces:**
- Consumes: `NotesRepository.GetDueReminders`/`ClearReminder`/`GetById`
  (Task 1), `AppCoordinator.OpenOrActivateNote` (existing),
  `NoteTitleHelper.GetTitle` (existing).
- Produces: `AppCoordinator.OpenNoteById(Guid noteId) -> void`,
  `ReminderScheduler.CheckDueReminders() -> void` (used by Task 4? No —
  used only by `App.xaml.cs` for the startup catch-up; Task 4 does not
  depend on this task and can be built independently).

This task is WPF-layer wiring — no automated tests (see Global
Constraints). Verified manually at the end of the task.

- [ ] **Step 1: Expose the tray `NotifyIcon`**

In `src/Aldune/Windowing/TrayIcon.cs`, add this property right after the
`_coordinator` field (line 22):

```csharp
    /// <summary>El NotifyIcon que ya crea esta clase, reutilizado por ReminderScheduler para avisar
    /// de recordatorios — un segundo icono de bandeja sería confuso.</summary>
    internal NotifyIcon Icon => _icon;
```

- [ ] **Step 2: Add `AppCoordinator.OpenNoteById`**

In `src/Aldune/Windowing/AppCoordinator.cs`, add this method right after
`OpenOrActivateNote` (after the closing brace at line 154):

```csharp

    /// <summary>
    /// Abre (o activa) una nota a partir de solo su Id, sin que lo pida un dock concreto — el mismo
    /// patrón que <see cref="CreateAndOpenNote"/>/<see cref="OpenNotesManager"/>, usado por
    /// <see cref="ReminderScheduler"/> cuando un recordatorio se dispara: solo conoce el NoteId, no
    /// una referencia al dock que originó la petición.
    /// </summary>
    public void OpenNoteById(Guid noteId)
    {
        var dock = DockNearCursor();
        if (dock is null) return;

        var note = _repository.GetById(noteId);
        if (note is null) return; // la nota se borró entre que sonó el recordatorio y el clic

        OpenOrActivateNote(note, dock);
    }
```

- [ ] **Step 3: Add the two new strings**

In `src/Aldune/Resources/Strings.cs`, add right after `AppName` (line 27):

```csharp
    public static string ReminderManyDue(int count) => T($"{count} reminders pending", $"{count} recordatorios pendientes");
```

(`ReminderNotificationTitle` is not needed — the balloon tip's title is
just `Strings.AppName`, already defined.)

- [ ] **Step 4: Implement `ReminderScheduler`**

Create `src/Aldune/Windowing/ReminderScheduler.cs`:

```csharp
using System.Windows.Forms;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Dispara el aviso nativo de Windows cuando un recordatorio de nota vence. Sondea cada 30s
/// mientras la app corre (<see cref="PollInterval"/>) y hace una pasada de catch-up al arrancar
/// (<see cref="CheckDueReminders"/>, llamada explícitamente desde App.OnStartup) para que un
/// recordatorio vencido con la app cerrada avise igual, en vez de perderse en silencio — ver
/// docs/superpowers/specs/2026-09-11-aldune-reminders-design.md.
///
/// Usa el NotifyIcon que ya crea TrayIcon (inyectado, no uno nuevo) y ShowBalloonTip en vez de un
/// toast interactivo de verdad: la razón (no depender de un acceso directo con ruta fija, que
/// rompería el uso portable) está documentada en la spec.
/// </summary>
internal sealed class ReminderScheduler
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BalloonTimeoutMs = 10000;

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private readonly NotifyIcon _icon;
    private readonly DispatcherTimer _timer;

    /// <summary>El NoteId del último aviso de UN solo recordatorio mostrado — null si el último
    /// aviso fue el agrupado de varios a la vez. Lo usa OnBalloonTipClicked para decidir si abre esa
    /// nota o el gestor. Como ShowBalloonTip reemplaza cualquier globo anterior por uno nuevo, el
    /// que esté visible en cada momento siempre corresponde al valor guardado más reciente.</summary>
    private Guid? _lastSingleNoteId;

    internal ReminderScheduler(NotesRepository repository, AppCoordinator coordinator, NotifyIcon icon)
    {
        _repository = repository;
        _coordinator = coordinator;
        _icon = icon;
        _icon.BalloonTipClicked += OnBalloonTipClicked;

        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => CheckDueReminders();
        _timer.Start();
    }

    /// <summary>Revisa y avisa de los recordatorios vencidos ahora mismo. Público porque App.xaml.cs
    /// la llama una vez explícitamente al arrancar, además del sondeo periódico normal.</summary>
    internal void CheckDueReminders()
    {
        var due = _repository.GetDueReminders(DateTimeOffset.UtcNow);
        if (due.Count == 0) return;

        foreach (var (noteId, _) in due)
        {
            _repository.ClearReminder(noteId);
        }

        if (due.Count == 1)
        {
            var noteId = due[0].NoteId;
            var note = _repository.GetById(noteId);
            _lastSingleNoteId = noteId;
            var text = note is null ? Strings.AppName : NoteTitleHelper.GetTitle(note.Text);
            _icon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, text, ToolTipIcon.None);
        }
        else
        {
            _lastSingleNoteId = null;
            _icon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, Strings.ReminderManyDue(due.Count), ToolTipIcon.None);
        }
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_lastSingleNoteId is { } noteId)
        {
            _coordinator.OpenNoteById(noteId);
        }
        else
        {
            _coordinator.OpenNotesManager();
        }
    }
}
```

- [ ] **Step 5: Wire it up in `App.xaml.cs`**

Add a field next to the others (near line 284-287):

```csharp
    private ReminderScheduler? _reminderScheduler;
```

In `OnStartup`, right after `_trayIcon = new TrayIcon(coordinator);` (line
221), add:

```csharp
        _reminderScheduler = new ReminderScheduler(repository, coordinator, _trayIcon.Icon);
        _reminderScheduler.CheckDueReminders(); // catch-up: avisa ya de lo vencido con la app cerrada
```

- [ ] **Step 6: Build and run the full test suite**

Run: `dotnet build`
Expected: 0 errors, 0 new warnings.

Run: `dotnet test`
Expected: all tests still pass (this task adds no new automated tests).

- [ ] **Step 7: Manual verification**

1. `dotnet run --project src/Aldune`.
2. Open a note, use the debugger/temporarily lower `PollInterval` if
   needed, or just wait — but for a quick check, skip ahead to Task 4
   first (it's what actually lets you *set* a reminder from the UI). Come
   back to this manual check once Task 4 is done: set a reminder 1-2
   minutes out from `NoteWindow`, wait, confirm the Windows notification
   appears with the note's title and clicking it opens that note.
2. Close Aldune, use a temporary test (or just accept a longer wait) to
   confirm the catch-up path: set a reminder, close the app before it
   fires, reopen — the balloon should appear immediately on next launch.

- [ ] **Step 8: Commit**

```bash
git add src/Aldune/Windowing/TrayIcon.cs src/Aldune/Windowing/AppCoordinator.cs src/Aldune/Windowing/ReminderScheduler.cs src/Aldune/Resources/Strings.cs src/Aldune/App.xaml.cs
git commit -m "Add ReminderScheduler: poll due reminders and notify via the tray icon"
```

---

### Task 4: `NoteWindow` — set / view / clear a reminder

**Files:**
- Modify: `src/Aldune/Windowing/NoteWindow.xaml` (menu entry + inline panel)
- Modify: `src/Aldune/Windowing/NoteWindow.xaml.cs` (event handlers)
- Modify: `src/Aldune/Resources/Strings.cs` (reminder picker strings)

**Interfaces:**
- Consumes: `NotesRepository.SetReminder`/`GetReminder`/`ClearReminder`
  (Task 1), `ReminderPresets.InOneHour`/`Tonight`/`TomorrowMorning`
  (Task 2).
- Produces: nothing consumed by later tasks (Task 5 reads
  `GetPendingReminders` directly, not through `NoteWindow`).

This task is WPF-layer UI — no automated tests (see Global Constraints).
Verified manually.

- [ ] **Step 1: Add the reminder strings**

In `src/Aldune/Resources/Strings.cs`, add inside the "Ventana de nota"
section, right after `ConvertToTask` (and the `ExportToMarkdown`/
`MarkdownFileFilter` pair added earlier in this same section):

```csharp
    public static string ReminderMenuEntry => T("Reminder", "Recordatorio");
    public static string ReminderSet(DateTimeOffset dueAt) =>
        T($"Reminder: {dueAt:dd/MM HH:mm}", $"Recordatorio: {dueAt:dd/MM HH:mm}");
    public static string ReminderInOneHour => T("In 1 hour", "En 1 hora");
    public static string ReminderTonight => T("Tonight", "Esta noche");
    public static string ReminderTomorrowMorning => T("Tomorrow 9:00", "Mañana 9:00");
    public static string ReminderSave => T("Save", "Guardar");
    public static string ReminderClear => T("Remove reminder", "Quitar recordatorio");
```

(Numeric `dd/MM HH:mm` on purpose, not a month name: the app has no
established pattern for localizing `DateTimeOffset` display, and a numeric
format sidesteps needing one for a first version.)

- [ ] **Step 2: Add the menu entry + inline panel to the XAML**

In `src/Aldune/Windowing/NoteWindow.xaml`, inside `ActionsPopup`'s
`StackPanel`, the current content (after the Export changes made earlier
this session) reads:

```xml
                        <Button x:Name="ExportButton" Click="OnExportClick"
                                Content="{x:Static res:Strings.ExportToMarkdown}"
                                Style="{StaticResource NoteMenuItemStyle}" />
                        <Border Background="#3C3730" Height="1" Margin="0,4,0,4" />

                        <Button x:Name="ArchiveButton" Click="OnArchiveClick"
```

Insert the reminder entry and panel right before `ExportButton`, so the
order is Task/Pin/**Reminder**/Export/divider/Archive-Restore-Trash:

```xml
                        <Button x:Name="ReminderButton" Click="OnReminderMenuClick"
                                Style="{StaticResource NoteMenuItemStyle}" />

                        <!-- Colapsado hasta que se pulsa ReminderButton. Mismo panel sirve para
                             poner un recordatorio nuevo y para "posponer": al abrir la nota desde
                             el aviso, el recordatorio que sonó ya se limpió solo, así que este
                             mismo flujo sin más UI aparte pone uno nuevo. -->
                        <StackPanel x:Name="ReminderPanel" Visibility="Collapsed" Margin="9,0,9,8">
                            <Button x:Name="ReminderInOneHourButton" Click="OnReminderPresetClick"
                                    Content="{x:Static res:Strings.ReminderInOneHour}"
                                    Style="{StaticResource NoteMenuItemStyle}" />
                            <Button x:Name="ReminderTonightButton" Click="OnReminderPresetClick"
                                    Content="{x:Static res:Strings.ReminderTonight}"
                                    Style="{StaticResource NoteMenuItemStyle}" />
                            <Button x:Name="ReminderTomorrowButton" Click="OnReminderPresetClick"
                                    Content="{x:Static res:Strings.ReminderTomorrowMorning}"
                                    Style="{StaticResource NoteMenuItemStyle}" />

                            <Calendar x:Name="ReminderCalendar" Margin="0,6,0,4" HorizontalAlignment="Center" />

                            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,8">
                                <TextBox x:Name="ReminderHourBox" Width="34" Text="09" TextAlignment="Center" />
                                <TextBlock Text=":" VerticalAlignment="Center" Margin="4,0" Foreground="#EDE7DC" />
                                <TextBox x:Name="ReminderMinuteBox" Width="34" Text="00" TextAlignment="Center" />
                            </StackPanel>

                            <Button x:Name="ReminderSaveButton" Click="OnReminderSaveClick"
                                    Content="{x:Static res:Strings.ReminderSave}"
                                    Style="{StaticResource NoteMenuItemStyle}" />
                            <Button x:Name="ReminderClearButton" Click="OnReminderClearClick"
                                    Content="{x:Static res:Strings.ReminderClear}"
                                    Visibility="Collapsed"
                                    Style="{StaticResource NoteMenuDangerStyle}" />
                        </StackPanel>
                        <Border Background="#3C3730" Height="1" Margin="0,4,0,4" />

                        <Button x:Name="ExportButton" Click="OnExportClick"
```

- [ ] **Step 3: Add the code-behind**

In `src/Aldune/Windowing/NoteWindow.xaml.cs`, add these methods right
before `OnExportClick` (added earlier this session, currently just above
`OnArchiveClick`):

```csharp
    private void OnReminderMenuClick(object sender, RoutedEventArgs e)
    {
        ReminderPanel.Visibility = ReminderPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnReminderPresetClick(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        var dueAt = sender == ReminderInOneHourButton ? ReminderPresets.InOneHour(now)
            : sender == ReminderTonightButton ? ReminderPresets.Tonight(now)
            : ReminderPresets.TomorrowMorning(now);

        SaveReminder(dueAt);
    }

    private void OnReminderSaveClick(object sender, RoutedEventArgs e)
    {
        if (ReminderCalendar.SelectedDate is not { } date) return;
        if (!int.TryParse(ReminderHourBox.Text, out int hour) || hour is < 0 or > 23) return;
        if (!int.TryParse(ReminderMinuteBox.Text, out int minute) || minute is < 0 or > 59) return;

        var local = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeOffset.Now.Offset);
        SaveReminder(local);
    }

    private void SaveReminder(DateTimeOffset dueAt)
    {
        _repository.SetReminder(_note.Id, dueAt);
        ReminderPanel.Visibility = Visibility.Collapsed;
        UpdateReminderButton();
        _coordinator.RefreshAll(); // para que el badge del dock (Task 5) se actualice ya
    }

    private void OnReminderClearClick(object sender, RoutedEventArgs e)
    {
        _repository.ClearReminder(_note.Id);
        ReminderPanel.Visibility = Visibility.Collapsed;
        UpdateReminderButton();
        _coordinator.RefreshAll();
    }

    private void UpdateReminderButton()
    {
        var dueAt = _repository.GetReminder(_note.Id);
        ReminderButton.Content = dueAt is { } due ? Strings.ReminderSet(due.ToLocalTime()) : Strings.ReminderMenuEntry;
        ReminderClearButton.Visibility = dueAt is null ? Visibility.Collapsed : Visibility.Visible;
    }

```

Then, in the constructor, right after `UpdatePinButton();` (line 45), add:

```csharp
        UpdateReminderButton();
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: 0 errors, 0 new warnings.

- [ ] **Step 5: Manual verification**

1. `dotnet run --project src/Aldune`, open a note.
2. Click "⋯" → "Recordatorio" — the panel expands with three presets, a
   calendar, and hour/minute boxes.
3. Click "En 1 hora" — the panel collapses, the menu entry now reads
   "Recordatorio: <fecha> <hora>" the next time you open "⋯".
4. Reopen "⋯" → "Recordatorio" — a "Quitar recordatorio" button now
   appears below Save. Click it — the entry goes back to plain
   "Recordatorio", no due date shown.
5. Set a reminder via the calendar + hour/minute boxes directly (not a
   preset) — confirm it saves and the menu entry reflects it.
6. Now go back and do Task 3's manual verification (set a reminder 1-2
   minutes out, confirm the native notification fires and clicking it
   opens the right note).

- [ ] **Step 6: Commit**

```bash
git add src/Aldune/Windowing/NoteWindow.xaml src/Aldune/Windowing/NoteWindow.xaml.cs src/Aldune/Resources/Strings.cs
git commit -m "Let NoteWindow set, view, and clear a note's reminder"
```

---

### Task 5: Dock tab reminder badge

**Files:**
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml` (badge element in the
  tab template)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (populate + apply)

**Interfaces:**
- Consumes: `NotesRepository.GetPendingReminders` (Task 1).
- Produces: nothing consumed elsewhere.

This task is WPF-layer UI — no automated tests. Verified manually.

- [ ] **Step 1: Add the badge element to the tab template**

In `src/Aldune/Windowing/EdgeDockWindow.xaml`, inside `NoteTabButtonStyle`'s
`ControlTemplate`, the current content (in the `Grid`) is:

```xml
                            <StackPanel x:Name="LabelStack" VerticalAlignment="Top" Margin="16,9,20,0">
                                ...
                            </StackPanel>

                            <Border x:Name="HoverOverlay" Background="#1E1A14" Opacity="0" CornerRadius="10,0,0,10" />
```

Insert the badge between them:

```xml
                            <StackPanel x:Name="LabelStack" VerticalAlignment="Top" Margin="16,9,20,0">
                                ...
                            </StackPanel>

                            <!-- Solo el glifo importa aquí, no si hay hueco para más: cuando las
                                 pestañas se apilan solo se ve la franja superior de cada una (ver
                                 el comentario de LabelStack), así que el badge va en esa misma
                                 franja, arriba a la derecha. Glifo elegido sin verificación visual
                                 todavía — mismo aviso que el resto de iconos de esta sesión, ver
                                 docs/STATUS.md. -->
                            <TextBlock x:Name="ReminderBadge"
                                       Text="&#xE917;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                       FontSize="11"
                                       Foreground="{Binding Color, Converter={StaticResource NoteLabelColorConverter}}"
                                       HorizontalAlignment="Right" VerticalAlignment="Top"
                                       Margin="0,8,10,0"
                                       Visibility="Collapsed" />

                            <Border x:Name="HoverOverlay" Background="#1E1A14" Opacity="0" CornerRadius="10,0,0,10" />
```

- [ ] **Step 2: Populate and apply it in code-behind**

In `src/Aldune/Windowing/EdgeDockWindow.xaml.cs`, add a field near
`_tabButtons` (search for `private readonly Dictionary<int, Button> _tabButtons`):

```csharp
    private IReadOnlyDictionary<Guid, DateTimeOffset> _pendingReminders = new Dictionary<Guid, DateTimeOffset>();
```

In `SetNotes`, add this line near the top, right after `_knownNoteIds =
notes.Select(n => n.Id).ToHashSet();`:

```csharp
        _pendingReminders = _repository.GetPendingReminders();
```

Add a new method right after `ApplyPreviewVisibility`:

```csharp
    /// <summary>Muestra u oculta el glifo de reloj de una pestaña según si su nota tiene un
    /// recordatorio pendiente — ver ApplyPreviewVisibility para el mismo patrón de FindName.</summary>
    private void ApplyReminderBadge(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("ReminderBadge", button) is not TextBlock badge) return;

        bool hasReminder = button.Tag is Note note && _pendingReminders.ContainsKey(note.Id);
        badge.Visibility = hasReminder ? Visibility.Visible : Visibility.Collapsed;
    }
```

In `OnTabLoaded`, right after the existing `ApplyPreviewVisibility(button);`
call, add:

```csharp
        ApplyReminderBadge(button);
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: 0 errors, 0 new warnings.

- [ ] **Step 4: Manual verification**

1. `dotnet run --project src/Aldune`.
2. Set a reminder on a note (Task 4's UI).
3. Hover the dock to expand it — the note's tab shows the clock badge in
   its top-right corner.
4. Clear the reminder — hover again, badge is gone.
5. Note whether the glyph actually renders as a recognizable clock (it
   was picked without visual verification) — if it renders as a blank box
   or the wrong shape, note it in `docs/STATUS.md` for a follow-up, same
   as other icons picked this way in this project.

- [ ] **Step 5: Run the full test suite one more time**

Run: `dotnet build && dotnet test`
Expected: 0 errors, all tests pass — this is the final task, confirming
nothing broke across the whole feature.

- [ ] **Step 6: Commit**

```bash
git add src/Aldune/Windowing/EdgeDockWindow.xaml src/Aldune/Windowing/EdgeDockWindow.xaml.cs
git commit -m "Show a clock badge on dock tabs with a pending reminder"
```

---

## After all tasks: update the continuity docs

Per the project's usual workflow (see `docs/STATUS.md`/`docs/ROADMAP.md`),
once all 5 tasks are verified:

- [ ] Write a summary of what was built into `docs/STATUS.md` (same style
  as the rest of the file — what, why, tests count, any bug found along
  the way).
- [ ] Mark "Recordatorios con notificación de Windows" as done in
  `docs/ROADMAP.md` (strike through the header, note it's done, point to
  `STATUS.md`).
- [ ] Republish the portable build and relaunch it (per the batched
  cadence: end of this session's work, not per task —
  `dotnet publish src/Aldune/Aldune.csproj -p:PublishProfile=portable`,
  clean up stray `*_wpftmp.csproj`, relaunch `./publish/portable/aldune.exe`).
