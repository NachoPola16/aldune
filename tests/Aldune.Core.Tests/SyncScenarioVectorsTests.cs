using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Vectores de ESCENARIO del motor de sync (<c>tests/vectors/sync-scenarios.json</c>): estado local + estado del almacén
/// → lo que hace <see cref="SyncService.Synchronize"/>. Complementan a <c>sync-format4.json</c> (formato de los datos): estos
/// fijan el algoritmo, y son el contrato del motor del cliente Kotlin (<c>android/sync</c>). Ver la spec de Android, §6.
///
/// Dos fases con el mismo ejecutor (<see cref="Run"/>): al regenerar se construyen las ENTRADAS en C# (con nonces y sales
/// aleatorios, por eso se guardan los sobres ya cifrados) y se anota lo observado; en cada ejecución normal se leen las
/// entradas del fichero, se ejecuta el motor real y se compara con lo anotado. Así el fichero no cambia salvo que se
/// regenere a propósito: <c>ALDUNE_REGEN_VECTORS=1 dotnet test --filter SyncScenarioVectorsTests</c>.
///
/// Lo que depende de «ahora» (<c>LastSyncAt</c>, la fecha en que se anotó un conflicto) no se observa. Se comparan
/// instantes y contenidos descifrados, nunca los bytes de un sobre (cada cifrado lleva un nonce nuevo).
/// </summary>
public sealed class SyncScenarioVectorsTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] OtherKey = Enumerable.Range(0, 32).Select(i => (byte)(0xA0 + i)).ToArray();
    private const string Local = "android-pc-a";
    private const string Peer = "device-b";
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2024-05-01T10:00:00+00:00");

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string VectorsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aldune.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Aldune.slnx not found"),
            "tests", "vectors", "sync-scenarios.json");
    }

    [Fact]
    public void Scenarios_MatchTheRecordedBehaviour()
    {
        var file = JsonNode.Parse(File.ReadAllText(VectorsPath()))!.AsObject();
        var scenarios = file["scenarios"]!.AsArray();
        Assert.True(scenarios.Count >= 25, "Faltan escenarios en sync-scenarios.json.");
        foreach (var scenario in scenarios)
        {
            var name = scenario!["name"]!.GetValue<string>();
            var observed = Run(scenario.AsObject());
            var expected = scenario["expected"]!;
            Assert.True(JsonNode.DeepEquals(expected, observed),
                $"Escenario «{name}»:\n--- esperado ---\n{expected.ToJsonString(Pretty)}\n--- observado ---\n{observed.ToJsonString(Pretty)}");
        }
    }

    [Fact]
    public void RegenerateScenarioVectors_OnlyWhenAsked()
    {
        if (Environment.GetEnvironmentVariable("ALDUNE_REGEN_VECTORS") != "1") return;
        var scenarios = new JsonArray();
        foreach (var input in Build())
        {
            input["expected"] = Run(input);
            scenarios.Add(input);
        }
        var file = new JsonObject
        {
            ["notes"] = "Vectores de escenario del motor de sync. Ver tests/vectors/README.md. Clave = bytes 0x00..0x1f; " +
                        "«other» = bytes 0xa0..0xbf. Fechas: comparar como instantes.",
            ["syncKeyHex"] = Convert.ToHexString(Key).ToLowerInvariant(),
            ["otherKeyHex"] = Convert.ToHexString(OtherKey).ToLowerInvariant(),
            ["localDeviceId"] = Local,
            ["scenarios"] = scenarios
        };
        File.WriteAllText(VectorsPath(), file.ToJsonString(Pretty) + "\n", new UTF8Encoding(false));
    }

    // ───────────────────────── ejecutor ─────────────────────────

    private static JsonObject Run(JsonObject scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), $"aldune-scenario-{Guid.NewGuid():N}");
        var shared = Path.Combine(root, "shared");
        var objects = Path.Combine(shared, "objects");
        Directory.CreateDirectory(objects);
        var databasePath = Path.Combine(root, "notes.db");
        try
        {
            var cfg = scenario["settings"]!.AsObject();
            var settingsService = new SettingsService(Path.Combine(root, "settings.json"));
            var settings = new AppSettings
            {
                SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = shared,
                SyncDeviceId = Local, SyncScope = (SyncScopeKind)cfg["scope"]!.GetValue<int>(),
                SyncTag = cfg["tag"]?.GetValue<string>(),
                SyncNoteIds = cfg["noteIds"]!.AsArray().Select(n => Guid.Parse(n!.GetValue<string>())).ToList()
            };
            settingsService.Save(settings);
            var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
            var sync = new SyncService(repository, settings, settingsService);
            sync.ImportSyncCode(SyncKeyFormat.Encode(Key));

            var local = scenario["local"]!.AsObject();
            foreach (var n in local["notes"]!.AsArray())
                repository.ApplySyncNote(ReadNote(n!.AsObject()), n["authorDeviceId"]?.GetValue<string>());
            foreach (var t in local["tombstones"]!.AsArray())
                repository.ApplySyncTombstone(new SyncTombstone(
                    Guid.Parse(t!["noteId"]!.GetValue<string>()), Instant(t["deletedAt"]!), t["deviceId"]!.GetValue<string>()));
            repository.SetSyncBases(local["bases"]!.AsArray().Select(b => KeyValuePair.Create(
                Guid.Parse(b!["noteId"]!.GetValue<string>()),
                new SyncBaseVersion(Instant(b["updatedAt"]!), b["deviceId"]!.GetValue<string>()))));

            foreach (var r in scenario["remote"]!.AsArray())
                File.WriteAllBytes(Path.Combine(objects, $"{Guid.Parse(r!["noteId"]!.GetValue<string>()):N}.json"),
                    Encoding.UTF8.GetBytes(r["envelopeJson"]!.GetValue<string>()));
            var before = Snapshot(objects);

            var result = sync.Synchronize();

            var after = Snapshot(objects);
            var remoteAfter = new JsonArray();
            foreach (var (file, bytes) in after.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                SyncEnvelope envelope;
                try { envelope = SyncEnvelopeCodec.Deserialize(bytes); }
                catch (FormatException)
                {
                    // Formato desconocido: el motor lo salta y debe dejarlo intacto en el almacén.
                    remoteAfter.Add(new JsonObject
                    {
                        ["file"] = file, ["unreadable"] = true,
                        ["rewritten"] = !before.TryGetValue(file, out var kept) || !kept.SequenceEqual(bytes)
                    });
                    continue;
                }
                var entry = new JsonObject
                {
                    ["noteId"] = envelope.NoteId.ToString(), ["tombstone"] = envelope.Tombstone,
                    ["updatedAt"] = Iso(envelope.UpdatedAt), ["deviceId"] = envelope.DeviceId,
                    ["rewritten"] = !before.TryGetValue(file, out var old) || !old.SequenceEqual(bytes)
                };
                if (envelope.Tombstone) entry["authentic"] = SyncEnvelopeCodec.IsAuthenticTombstone(envelope, Key);
                else entry["note"] = TryDecrypt(envelope);
                remoteAfter.Add(entry);
            }

            var localAfter = new JsonArray();
            var authors = repository.GetSyncAuthors();
            foreach (var note in repository.GetAllForSync().OrderBy(n => n.Id.ToString(), StringComparer.Ordinal))
            {
                var json = WriteNote(note);
                json["authorDeviceId"] = authors.GetValueOrDefault(note.Id);
                localAfter.Add(json);
            }

            var tombstonesAfter = new JsonArray();
            foreach (var t in repository.GetSyncTombstones().OrderBy(t => t.NoteId.ToString(), StringComparer.Ordinal))
                tombstonesAfter.Add(new JsonObject
                {
                    ["noteId"] = t.NoteId.ToString(), ["deletedAt"] = Iso(t.DeletedAt), ["deviceId"] = t.DeviceId
                });

            var basesAfter = new JsonArray();
            foreach (var (id, b) in repository.GetSyncBases().OrderBy(p => p.Key.ToString(), StringComparer.Ordinal))
                basesAfter.Add(new JsonObject
                {
                    ["noteId"] = id.ToString(), ["updatedAt"] = Iso(b.UpdatedAt), ["deviceId"] = b.DeviceId
                });

            var conflicts = new JsonArray();
            foreach (var c in sync.GetConflicts().OrderBy(c => c.NoteId.ToString(), StringComparer.Ordinal))
                conflicts.Add(new JsonObject
                {
                    ["noteId"] = c.NoteId.ToString(), ["winner"] = Version(c.Winner), ["losing"] = Version(c.Losing)
                });

            return new JsonObject
            {
                ["error"] = result.Error, ["uploaded"] = result.Uploaded, ["downloaded"] = result.Downloaded,
                ["conflictsResolved"] = result.ConflictsResolved, ["tombstonesApplied"] = result.TombstonesApplied,
                ["remoteAfter"] = remoteAfter, ["localAfter"] = localAfter, ["localTombstonesAfter"] = tombstonesAfter,
                ["basesAfter"] = basesAfter, ["conflicts"] = conflicts
            };
        }
        finally
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
            SqliteConnection.ClearPool(connection);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static Dictionary<string, byte[]> Snapshot(string objects) =>
        Directory.GetFiles(objects, "*.json").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllBytes);

    private static JsonNode? TryDecrypt(SyncEnvelope envelope)
    {
        try { return WriteNote(SyncEnvelopeCodec.DecryptNote(envelope, Key)); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException or JsonException)
        {
            return null; // no descifra con la clave del perfil (otra clave)
        }
    }

    private static JsonObject Version(SyncConflictVersion v) => new()
    {
        ["updatedAt"] = Iso(v.UpdatedAt), ["deviceId"] = v.DeviceId, ["tombstone"] = v.Tombstone,
        ["text"] = v.Note?.Text
    };

    // Algunas rutas del motor ponen «ahora» en UpdatedAt (p. ej. mandar a la papelera un borrado sin firma). Esa fecha no
    // es reproducible: se anota como «<now>» y el cliente solo debe comprobar que es reciente. Las fechas de los
    // escenarios están en 2024 (y 2099), lejos de cualquier «ahora» posible.
    private static string Iso(DateTimeOffset value) =>
        Math.Abs((value - DateTimeOffset.UtcNow).TotalDays) < 1 ? "<now>" : value.ToUniversalTime().ToString("O");

    private static DateTimeOffset Instant(JsonNode node) =>
        DateTimeOffset.Parse(node.GetValue<string>(), null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static Note ReadNote(JsonObject n) => new()
    {
        Id = Guid.Parse(n["id"]!.GetValue<string>()), Text = n["text"]!.GetValue<string>(), Color = n["color"]!.GetValue<string>(),
        CreatedAt = Instant(n["createdAt"]!), UpdatedAt = Instant(n["updatedAt"]!),
        State = (NoteState)n["state"]!.GetValue<int>(), ScreenOrigin = n["screenOrigin"]!.GetValue<string>(),
        Tags = n["tags"]!.AsArray().Select(t => t!.GetValue<string>()).ToList(),
        DockPosition = n["dockPosition"]?.GetValue<double>(), IsProtected = n["isProtected"]!.GetValue<bool>(),
        ProtectedContent = n["protectedContent"] is JsonObject p
            ? new ProtectedNoteContent
            {
                Version = p["version"]!.GetValue<int>(), Salt = p["salt"]!.GetValue<string>(),
                CipherText = p["cipherText"]!.GetValue<string>(), Nonce = p["nonce"]!.GetValue<string>(),
                Tag = p["tag"]!.GetValue<string>()
            }
            : null
    };

    private static JsonObject WriteNote(Note n) => new()
    {
        ["id"] = n.Id.ToString(), ["text"] = n.Text, ["color"] = n.Color, ["createdAt"] = Iso(n.CreatedAt),
        ["updatedAt"] = Iso(n.UpdatedAt), ["state"] = (int)n.State, ["screenOrigin"] = n.ScreenOrigin,
        ["tags"] = new JsonArray(n.Tags.Select(t => (JsonNode?)t).ToArray()), ["isProtected"] = n.IsProtected,
        ["protectedContent"] = n.ProtectedContent is { } p
            ? new JsonObject
            {
                ["version"] = p.Version, ["salt"] = p.Salt, ["cipherText"] = p.CipherText, ["nonce"] = p.Nonce, ["tag"] = p.Tag
            }
            : null,
        ["dockPosition"] = n.DockPosition
    };

    // ───────────────────────── construcción de las entradas ─────────────────────────

    private static Guid Id(int n) => Guid.Parse($"{n:x8}-0000-4000-8000-000000000000");
    private static DateTimeOffset At(double minutes) => T0.AddMinutes(minutes);

    private static Note N(int id, string text, double minutes, NoteState state = NoteState.Active, string[]? tags = null,
        double? dock = null, string screen = "primary", ProtectedNoteContent? content = null) => new()
    {
        Id = Id(id), Text = content is null ? text : string.Empty, Color = "#EBD38B", CreatedAt = At(-60),
        UpdatedAt = At(minutes), State = state, ScreenOrigin = screen, Tags = (tags ?? []).ToList(), DockPosition = dock,
        IsProtected = content is not null, ProtectedContent = content
    };

    private sealed class Scn
    {
        public required string Name { get; init; }
        public required string Covers { get; init; }
        public SyncScopeKind Scope { get; init; } = SyncScopeKind.AllNotes;
        public string? Tag { get; init; }
        public int[] NoteIds { get; init; } = [];
        public List<(Note Note, string? Author)> Notes { get; } = [];
        public List<(Guid Id, DateTimeOffset At, string Device)> Tombstones { get; } = [];
        public List<(Guid Id, DateTimeOffset At, string Device)> Bases { get; } = [];
        public List<JsonObject> Remote { get; } = [];

        public Scn Has(Note note, string? author = null) { Notes.Add((note, author)); return this; }
        public Scn Gone(int id, double minutes, string device = "local") { Tombstones.Add((Id(id), At(minutes), device)); return this; }
        public Scn Base(int id, double minutes, string device = Peer) { Bases.Add((Id(id), At(minutes), device)); return this; }

        public Scn Store(Note note, string device = Peer, byte[]? key = null, int? format = null)
        {
            var envelope = SyncEnvelopeCodec.CreateNote(note, key ?? Key, device);
            if (format is { } f) envelope.Format = f;
            Remote.Add(Entry(envelope, note.Id, "note", key, WriteNote(note)));
            return this;
        }

        /// <param name="signed">false = lo cifra otra clave (no se puede comprobar: «sin firma»).</param>
        public Scn StoreGone(int id, double minutes, string device = Peer, bool signed = true, double? headerMinutes = null)
        {
            var key = signed ? Key : OtherKey;
            var envelope = SyncEnvelopeCodec.CreateTombstone(new SyncTombstone(Id(id), At(minutes), device), key, device);
            if (headerMinutes is { } h) envelope.UpdatedAt = At(h); // fecha de la cabecera distinta de la firmada
            Remote.Add(Entry(envelope, Id(id), "tombstone", signed ? null : OtherKey, null));
            return this;
        }

        private static JsonObject Entry(SyncEnvelope envelope, Guid id, string kind, byte[]? key, JsonObject? plain) => new()
        {
            ["noteId"] = id.ToString(), ["kind"] = kind, ["key"] = key is null ? "vectors" : "other",
            ["envelopeJson"] = Encoding.UTF8.GetString(SyncEnvelopeCodec.Serialize(envelope)), ["plain"] = plain
        };

        public JsonObject ToJson() => new()
        {
            ["name"] = Name, ["covers"] = Covers,
            ["settings"] = new JsonObject
            {
                ["deviceId"] = Local, ["scope"] = (int)Scope, ["tag"] = Tag,
                ["noteIds"] = new JsonArray(NoteIds.Select(i => (JsonNode?)Id(i).ToString()).ToArray())
            },
            ["local"] = new JsonObject
            {
                ["notes"] = new JsonArray(Notes.Select(n =>
                {
                    var j = WriteNote(n.Note);
                    j["authorDeviceId"] = n.Author;
                    return (JsonNode?)j;
                }).ToArray()),
                ["bases"] = new JsonArray(Bases.Select(b => (JsonNode?)new JsonObject
                    { ["noteId"] = b.Id.ToString(), ["updatedAt"] = Iso(b.At), ["deviceId"] = b.Device }).ToArray()),
                ["tombstones"] = new JsonArray(Tombstones.Select(t => (JsonNode?)new JsonObject
                    { ["noteId"] = t.Id.ToString(), ["deletedAt"] = Iso(t.At), ["deviceId"] = t.Device }).ToArray())
            },
            ["remote"] = new JsonArray(Remote.Select(r => (JsonNode?)r.DeepClone()).ToArray())
        };
    }

    private static IEnumerable<JsonObject> Build()
    {
        var secret = ProtectedNoteContent.Protect("secreto ☐", "clave-de-prueba");
        var s = new List<Scn>();
        Scn Add(string name, string covers, SyncScopeKind scope = SyncScopeKind.AllNotes, string? tag = null, int[]? ids = null)
        {
            var scn = new Scn { Name = name, Covers = covers, Scope = scope, Tag = tag, NoteIds = ids ?? [] };
            s.Add(scn);
            return scn;
        }

        Add("E01 solo local: se sube", "SyncService.cs:402-404").Has(N(1, "local", 10));
        Add("E02 solo remoto: se aplica", "SyncService.cs:395").Store(N(1, "remoto", 10));
        Add("E03 iguales: no se hace nada", "SyncService.cs:408-434")
            .Has(N(1, "igual", 10), Peer).Base(1, 10).Store(N(1, "igual", 10));
        Add("E04 cambió lo local, lo remoto es la base: se sube sin conflicto", "SyncService.cs:408,424-428")
            .Has(N(1, "nuevo", 20)).Base(1, 10).Store(N(1, "viejo", 10));
        Add("E05 cambió lo remoto, lo local es la base: se aplica sin conflicto", "SyncService.cs:408,429-432")
            .Has(N(1, "viejo", 10), Peer).Base(1, 10).Store(N(1, "nuevo", 20));
        Add("E06a los dos cambiaron: conflicto, gana lo remoto (más reciente)", "SyncService.cs:415-420")
            .Has(N(1, "local", 20)).Base(1, 10).Store(N(1, "remoto", 30));
        Add("E06b los dos cambiaron: conflicto, gana lo local (más reciente)", "SyncService.cs:415-420")
            .Has(N(1, "local", 30)).Base(1, 10).Store(N(1, "remoto", 20));
        Add("E07 los dos cambiaron al mismo contenido: sin conflicto", "SyncService.cs:415 NoteEquivalence.SameContent")
            .Has(N(1, "igual", 20)).Base(1, 10).Store(N(1, "igual", 30));
        Add("E08 sin base y distintos: conflicto por prudencia", "SyncService.cs:460-466")
            .Has(N(1, "local", 20)).Store(N(1, "remoto", 30));
        Add("E09a misma fecha, contenido distinto: gana el deviceId mayor (remoto), sin conflicto", "SyncService.cs:462")
            .Has(N(1, "local", 20)).Base(1, 10).Store(N(1, "remoto", 20));
        Add("E09b misma fecha, contenido distinto: gana el deviceId mayor (local), sin conflicto", "SyncService.cs:462")
            .Has(N(1, "local", 20)).Base(1, 10).Store(N(1, "remoto", 20), "aaa-device");
        Add("E10 versión = base + 1 ms (migración de temas): sin conflicto", "SyncService.cs:472-473")
            .Has(N(1, "local", 10.0 + 1.0 / 60000)).Base(1, 10).Store(N(1, "remoto", 30));
        Add("E11 borrado remoto firmado: se aplica", "SyncService.cs:429-432; NotesRepository.cs:734")
            .Has(N(1, "x", 10), Peer).Base(1, 10).StoreGone(1, 20);
        Add("E12a borrado remoto sin firma: se ignora y la nota se vuelve a publicar", "SyncService.cs:596-621")
            .Has(N(1, "x", 10), Peer).Base(1, 10).StoreGone(1, 20, signed: false);
        Add("E12b borrado sin firma de una nota que no existe aquí: nada", "SyncService.cs:618-620")
            .StoreGone(1, 20, signed: false);
        Add("E12c borrado sin firma de una nota ya en la papelera: no toca UpdatedAt", "SyncService.cs:618-620")
            .Has(N(1, "x", 10, NoteState.Trashed), Peer).Base(1, 10).StoreGone(1, 20, signed: false);
        Add("E13 borrado sin firma con fecha futura: se borra del almacén", "SyncService.cs:612-616")
            .Has(N(1, "x", 10), Peer).Base(1, 10)
            .StoreGone(1, 20, signed: false, headerMinutes: (new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero) - T0).TotalMinutes);
        Add("E15 borrado local: se sube un borrado firmado", "SyncService.cs:641-642")
            .Gone(1, 20).Base(1, 10).Store(N(1, "x", 10));
        Add("E16 la papelera viaja como estado", "NotesRepository.cs:87-88")
            .Has(N(2, "en papelera", 20, NoteState.Trashed));
        Add("E17 alcance por etiqueta: solo suben las etiquetadas", "SyncScopeFilter.cs:15-16", SyncScopeKind.Tag, "Work")
            .Has(N(1, "sin etiqueta", 10)).Has(N(2, "con etiqueta", 10, tags: ["work"]));
        Add("E18a salió de la etiqueta aquí y es más reciente: no se aplica la remota", "SyncService.cs:364-367,393",
                SyncScopeKind.Tag, "work")
            .Has(N(1, "ya sin etiqueta", 30)).Base(1, 10).Store(N(1, "con etiqueta", 20, tags: ["work"]));
        Add("E18b salió de la etiqueta aquí pero la remota es más reciente: se aplica", "SyncService.cs:393-395",
                SyncScopeKind.Tag, "work")
            .Has(N(1, "ya sin etiqueta", 20)).Base(1, 10).Store(N(1, "con etiqueta", 30, tags: ["work"]));
        Add("E19 con etiqueta, un borrado remoto pasa igual", "SyncService.cs:337-339", SyncScopeKind.Tag, "work")
            .Has(N(1, "x", 10, tags: ["work"]), Peer).Base(1, 10).StoreGone(1, 20);
        Add("E20 alcance «notas elegidas»", "SyncService.cs:342-344", SyncScopeKind.SelectedNotes, ids: [1])
            .Has(N(1, "elegida", 10)).Has(N(2, "no elegida", 10)).Store(N(3, "remota no elegida", 10));
        Add("E22a nota protegida: sube con su contenido protegido", "SyncModels.cs:452-453")
            .Has(N(1, "", 20, content: secret));
        Add("E22b la misma protegida en los dos lados: sin conflicto", "NoteEquivalence.cs:30-33")
            .Has(N(1, "", 20, content: secret)).Base(1, 10).Store(N(1, "", 30, content: secret));
        Add("E23a ninguna nota descifra (otra clave): error y cero escrituras", "SyncService.cs:316-323")
            .Has(N(2, "local", 10)).Store(N(1, "ajena", 10), key: OtherKey);
        Add("E23b el almacén solo tiene borrados de otra clave: no aborta", "SyncService.cs:560-584")
            .Has(N(1, "x", 10), Peer).Base(1, 10).StoreGone(1, 20, signed: false);
        Add("E24 objeto con format 5: la carpeta lo salta", "FolderSyncTransport.cs:27-35")
            .Has(N(2, "local", 10)).Store(N(1, "futura", 10), format: 5);
        Add("E25 solo cambia dockPosition: se sube", "SyncModels.cs:335-350")
            .Has(N(1, "igual", 20, dock: 3)).Base(1, 10).Store(N(1, "igual", 10, dock: 1));
        Add("E26 borrado local con alcance por etiqueta: asimetría actual", "SyncService.cs:368-370", SyncScopeKind.Tag, "work")
            .Gone(1, 20).Base(1, 10).Store(N(1, "x", 10, tags: ["work"]));
        Add("E27a edición local frente a borrado remoto más reciente: gana el borrado y queda el conflicto", "SyncService.cs:409-432")
            .Has(N(1, "editada aquí", 20)).Base(1, 10).StoreGone(1, 30);
        Add("E27b edición local más reciente que el borrado remoto: gana la edición", "SyncService.cs:409-428")
            .Has(N(1, "editada aquí", 30)).Base(1, 10).StoreGone(1, 20);
        Add("E28 ScreenOrigin y etiquetas con mayúsculas distintas viajan sin cambios", "NoteEquivalence.cs:13-24")
            .Has(N(1, "igual", 10, screen: "secondary", tags: ["Zeta", "alpha"]), Peer).Base(1, 10)
            .Store(N(1, "igual", 10, screen: "secondary", tags: ["Zeta", "alpha"]));

        return s.Select(x => x.ToJson());
    }
}
