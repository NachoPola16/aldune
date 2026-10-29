using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aldune.Core.Tests;

/// <summary>
/// Vectores de compatibilidad del formato de sync 4 (<c>tests/vectors/sync-format4.json</c>). Son la fuente de verdad
/// para cualquier cliente que no use <c>Aldune.Core</c> (Kotlin en Android, Swift en iOS): si su implementación lee
/// y escribe lo mismo que estos ficheros, entiende el formato. Aquí se comprueba, siempre, que Core sigue leyendo
/// exactamente lo que dice el fichero (un cambio de formato rompe este test antes que a un cliente). Para
/// regenerarlo a propósito: <c>ALDUNE_REGEN_VECTORS=1 dotnet test --filter SyncVectorsTests</c>; es un cambio de
/// formato y hay que pensar antes en las versiones ya instaladas (ver docs/SYNC.md).
/// </summary>
public sealed class SyncVectorsTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private const string DeviceA = "device-a";

    private static string VectorsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aldune.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Aldune.slnx not found"),
            "tests", "vectors", "sync-format4.json");
    }

    private static DateTimeOffset T(string iso) =>
        DateTimeOffset.Parse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static Note MakeNote(string id, string text, NoteState state, string[] tags, double? dock,
        bool isProtected = false, ProtectedNoteContent? content = null, string? protectedTitle = null) => new()
    {
        Id = Guid.Parse(id), Text = text, Color = "#EBD38B",
        CreatedAt = T("2026-10-01T08:00:00.1234567+00:00"), UpdatedAt = T("2026-10-17T10:30:15.5000000+00:00"),
        State = state, ScreenOrigin = "primary", Tags = tags, DockPosition = dock,
        IsProtected = isProtected, ProtectedContent = content, ProtectedTitle = protectedTitle
    };

    private static JsonObject Expected(Note n) => new()
    {
        ["id"] = n.Id.ToString(), ["text"] = n.Text, ["color"] = n.Color,
        ["createdAt"] = n.CreatedAt.ToString("O"), ["updatedAt"] = n.UpdatedAt.ToString("O"),
        ["state"] = (int)n.State, ["screenOrigin"] = n.ScreenOrigin,
        ["tags"] = new JsonArray(n.Tags.Select(t => (JsonNode?)t).ToArray()),
        ["isProtected"] = n.IsProtected, ["dockPosition"] = n.DockPosition, ["protectedTitle"] = n.ProtectedTitle
    };

    // El contenido cifrado del sobre es AES-GCM del TEXTO BASE64 del JSON (no del JSON directamente): ver el README.
    private static string PayloadJson(SyncEnvelope envelope)
    {
        using var cipher = new ContentCipher(Key);
        var base64 = cipher.Decrypt(new EncryptedContent(
            Convert.FromBase64String(envelope.CipherText!), Convert.FromBase64String(envelope.Nonce!),
            Convert.FromBase64String(envelope.Tag!)));
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private static JsonObject NoteVector(string name, Note note)
    {
        var envelope = SyncEnvelopeCodec.CreateNote(note, Key, DeviceA);
        return new JsonObject
        {
            ["name"] = name, ["deviceId"] = DeviceA,
            ["envelopeJson"] = Encoding.UTF8.GetString(SyncEnvelopeCodec.Serialize(envelope)),
            ["payloadJson"] = PayloadJson(envelope), ["expected"] = Expected(note)
        };
    }

    private static JsonObject TombstoneVector(string name, SyncEnvelope envelope, bool authentic) => new()
    {
        ["name"] = name, ["envelopeJson"] = Encoding.UTF8.GetString(SyncEnvelopeCodec.Serialize(envelope)),
        ["authentic"] = authentic
    };

    private static JsonObject Order(string aAt, string aDev, string bAt, string bDev, int expected) => new()
    {
        ["a"] = new JsonObject { ["updatedAt"] = aAt, ["deviceId"] = aDev },
        ["b"] = new JsonObject { ["updatedAt"] = bAt, ["deviceId"] = bDev },
        ["expectedSign"] = expected
    };

    private static JsonObject Share(string code, int scope, string[] noteIds, string? name, int? transport, string? url, string? tag) => new()
    {
        ["code"] = code,
        ["expected"] = new JsonObject
        {
            ["scope"] = scope, ["noteIds"] = new JsonArray(noteIds.Select(i => (JsonNode?)i).ToArray()),
            ["profileName"] = name, ["transport"] = transport, ["serverUrl"] = url, ["tag"] = tag
        }
    };

    private static JsonObject Build()
    {
        var protectedContent = ProtectedNoteContent.Protect("secreto: línea 1\nlínea 2 ☐", "contraseña-de-prueba");
        var tombstoneId = Guid.Parse("3b9f1c2e-0a44-4c1b-8f3d-5d7e9a1b2c3d");
        var deletedAt = T("2026-10-17T11:00:00.0000000+00:00");
        var authentic = SyncEnvelopeCodec.CreateTombstone(new SyncTombstone(tombstoneId, deletedAt, DeviceA), Key, DeviceA);
        // Un borrado "sin firmar": la cabecera dice una fecha distinta de la que va cifrada dentro.
        var forged = SyncEnvelopeCodec.CreateTombstone(new SyncTombstone(tombstoneId, deletedAt, DeviceA), Key, DeviceA);
        forged.UpdatedAt = deletedAt.AddDays(30);

        var shareId = Guid.Parse("5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f");
        const string davUrl = "https://cloud.example.com/remote.php/dav/files/ana/aldune/";

        return new JsonObject
        {
            ["format"] = SyncCompatibility.CurrentFormat,
            ["notes"] = "Vectores del formato de sync 4. Ver tests/vectors/README.md. Clave = bytes 0x00..0x1f.",
            ["syncKeyHex"] = Convert.ToHexString(Key).ToLowerInvariant(),
            ["syncKeyEncoded"] = SyncKeyFormat.Encode(Key),
            ["envelopes"] = new JsonArray(
                NoteVector("nota simple", MakeNote("0d3c5b8e-1f2a-4b6c-9d7e-8a9b0c1d2e3f", "Título\ncuerpo", NoteState.Active, [], null)),
                NoteVector("unicode, casillas y etiquetas", MakeNote("1e4d6c9f-2a3b-4c7d-8e9f-0b1c2d3e4f5a",
                    "Compra 🛒\n☐ pan\n☒ leche\n→ viñeta con acentos: áéíóú ñ ü ß", NoteState.Archived, ["casa", "urgente"], 2.5)),
                NoteVector("papelera con posición", MakeNote("2f5e7d0a-3b4c-4d8e-9f0a-1c2d3e4f5a6b", "", NoteState.Trashed, [], -3.25)),
                NoteVector("protegida", MakeNote("3a6f8e1b-4c5d-4e9f-8a1b-2d3e4f5a6b7c", "", NoteState.Active, [], null, true, protectedContent)),
                NoteVector("protegida con nombre visible", MakeNote("4b7a9f2c-5d6e-4fa0-9b2c-3e4f5a6b7c8d", "", NoteState.Active, [], null, true, protectedContent, "Banco ☐"))),
            ["tombstones"] = new JsonArray(
                TombstoneVector("borrado firmado", authentic, true),
                TombstoneVector("borrado con fecha falsificada", forged, false)),
            ["protectedNotes"] = new JsonArray(new JsonObject
            {
                ["password"] = "contraseña-de-prueba", ["plaintext"] = "secreto: línea 1\nlínea 2 ☐", ["pbkdf2Iterations"] = 600_000,
                ["content"] = JsonSerializer.SerializeToNode(protectedContent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            }),
            ["shareCodes"] = new JsonArray(
                Share(SyncShareCodeCodec.Encode(Key, SyncScopeKind.AllNotes, [], "Casa", SyncTransportKind.Server, "https://sync.example.com/", null),
                    0, [], "Casa", 1, "https://sync.example.com/", null),
                Share(SyncShareCodeCodec.Encode(Key, SyncScopeKind.SelectedNotes, [shareId], null, SyncTransportKind.WebDav, davUrl, null),
                    1, [shareId.ToString()], null, 2, davUrl, null),
                Share(SyncShareCodeCodec.Encode(Key, SyncScopeKind.Tag, [], "Trabajo", null, null, "trabajo"),
                    2, [], "Trabajo", null, null, "trabajo")),
            ["versionOrder"] = new JsonArray(
                Order("2026-10-17T10:00:00.0000000+00:00", "a", "2026-10-17T10:00:01.0000000+00:00", "a", -1),
                Order("2026-10-17T10:00:01.0000000+00:00", "a", "2026-10-17T10:00:00.0000000+00:00", "z", 1),
                Order("2026-10-17T10:00:00.0000000+00:00", "a", "2026-10-17T10:00:00.0000000+00:00", "b", -1),
                Order("2026-10-17T10:00:00.0000000+00:00", "b", "2026-10-17T10:00:00.0000000+00:00", "a", 1),
                Order("2026-10-17T10:00:00.0000000+00:00", "a", "2026-10-17T10:00:00.0000000+00:00", "a", 0),
                Order("2026-10-17T12:00:00.0000000+02:00", "a", "2026-10-17T10:00:00.0000000+00:00", "a", 0))
        };
    }

    [Fact]
    public void RegenerateVectors_OnlyWhenAsked()
    {
        if (Environment.GetEnvironmentVariable("ALDUNE_REGEN_VECTORS") != "1") return;
        var options = new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        File.WriteAllText(VectorsPath(), Build().ToJsonString(options) + "\n", new UTF8Encoding(false));
    }

    /// <summary>
    /// Los dos ficheros: el que genera Core y el que escribe el cliente Kotlin (<c>android/core</c>). Core tiene que leer los dos
    /// igual: es la comprobación de compatibilidad en sentido inverso (lo que escribe Kotlin lo lee C#).
    /// </summary>
    public static IEnumerable<object[]> VectorFiles() => [["sync-format4.json"], ["kotlin-output.json"]];

    private static JsonObject Load(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(VectorsPath())!, file)))!.AsObject();

    // Las fechas se comparan como instantes: Core las escribe en "expected" con 7 decimales y Kotlin recortadas
    // ("...15.5+00:00"), y un cliente que lee tiene que dar lo mismo para las dos.
    private static void AssertSameNote(string name, JsonObject expected, Note note)
    {
        Assert.True(expected["id"]!.GetValue<string>() == note.Id.ToString(), $"{name}: id");
        Assert.True(expected["text"]!.GetValue<string>() == note.Text, $"{name}: text");
        Assert.True(expected["color"]!.GetValue<string>() == note.Color, $"{name}: color");
        Assert.True(T(expected["createdAt"]!.GetValue<string>()) == note.CreatedAt, $"{name}: createdAt");
        Assert.True(T(expected["updatedAt"]!.GetValue<string>()) == note.UpdatedAt, $"{name}: updatedAt");
        Assert.True(expected["state"]!.GetValue<int>() == (int)note.State, $"{name}: state");
        Assert.True(expected["screenOrigin"]!.GetValue<string>() == note.ScreenOrigin, $"{name}: screenOrigin");
        Assert.True(expected["tags"]!.AsArray().Select(t => t!.GetValue<string>()).SequenceEqual(note.Tags), $"{name}: tags");
        Assert.True(expected["isProtected"]!.GetValue<bool>() == note.IsProtected, $"{name}: isProtected");
        Assert.True(expected["dockPosition"]?.GetValue<double>() == note.DockPosition, $"{name}: dockPosition");
        // Un fichero anterior al nombre visible no trae la clave: equivale a «sin nombre».
        Assert.True(expected["protectedTitle"]?.GetValue<string>() == note.ProtectedTitle, $"{name}: protectedTitle");
    }

    [Theory]
    [MemberData(nameof(VectorFiles))]
    public void Envelopes_AreReadByCoreExactlyAsTheFileSays(string file)
    {
        foreach (var vector in Load(file)["envelopes"]!.AsArray().Select(n => n!.AsObject()))
        {
            var name = $"{file}: {vector["name"]!.GetValue<string>()}";
            var envelope = SyncEnvelopeCodec.Deserialize(Encoding.UTF8.GetBytes(vector["envelopeJson"]!.GetValue<string>()));

            Assert.True(PayloadJson(envelope) == vector["payloadJson"]!.GetValue<string>(), $"{name}: el contenido descifrado cambió");
            var note = SyncEnvelopeCodec.DecryptNote(envelope, Key);
            AssertSameNote(name, vector["expected"]!.AsObject(), note);

            // Una nota protegida que trae su contraseña de prueba: Core tiene que poder abrirla.
            if (vector["protectedPassword"] is { } password)
            {
                Assert.True(note.ProtectedContent!.TryUnprotect(password.GetValue<string>(), out var plain), $"{name}: no abre");
                Assert.Equal(vector["protectedPlaintext"]!.GetValue<string>(), plain);
            }
        }
    }

    [Theory]
    [MemberData(nameof(VectorFiles))]
    public void Tombstones_AuthenticityMatchesTheFile(string file)
    {
        foreach (var vector in Load(file)["tombstones"]!.AsArray().Select(n => n!.AsObject()))
        {
            var envelope = SyncEnvelopeCodec.Deserialize(Encoding.UTF8.GetBytes(vector["envelopeJson"]!.GetValue<string>()));
            Assert.True(envelope.Tombstone);
            Assert.True(vector["authentic"]!.GetValue<bool>() == SyncEnvelopeCodec.IsAuthenticTombstone(envelope, Key),
                $"{file}: {vector["name"]!.GetValue<string>()}");
        }
    }

    [Theory]
    [MemberData(nameof(VectorFiles))]
    public void ProtectedNotes_UnlockWithTheFilePasswordAndNotWithAnother(string file)
    {
        foreach (var vector in Load(file)["protectedNotes"]!.AsArray().Select(n => n!.AsObject()))
        {
            var content = vector["content"]!.Deserialize<ProtectedNoteContent>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(600_000, vector["pbkdf2Iterations"]!.GetValue<int>());
            Assert.True(content.TryUnprotect(vector["password"]!.GetValue<string>(), out var plain), file);
            Assert.Equal(vector["plaintext"]!.GetValue<string>(), plain);
            Assert.False(content.TryUnprotect("otra-contraseña", out _));
        }
    }

    [Theory]
    [MemberData(nameof(VectorFiles))]
    public void ShareCodes_DecodeToWhatTheFileSays(string file)
    {
        foreach (var vector in Load(file)["shareCodes"]!.AsArray().Select(n => n!.AsObject()))
        {
            var data = SyncShareCodeCodec.Decode(vector["code"]!.GetValue<string>());
            var expected = vector["expected"]!.AsObject();
            Assert.Equal(Key, data.Key);
            Assert.Equal(expected["scope"]!.GetValue<int>(), (int)data.Scope);
            Assert.Equal(expected["noteIds"]!.AsArray().Select(n => n!.GetValue<string>()), data.NoteIds.Select(i => i.ToString()));
            Assert.Equal(expected["profileName"]?.GetValue<string>(), data.ProfileName);
            Assert.Equal(expected["transport"]?.GetValue<int>(), data.Transport is { } t ? (int)t : null);
            Assert.Equal(expected["serverUrl"]?.GetValue<string>(), data.ServerUrl);
            Assert.Equal(expected["tag"]?.GetValue<string>(), data.Tag);
        }
    }

    [Fact]
    public void VersionOrder_MatchesTheFile()
    {
        SyncEnvelope Of(JsonNode side) => new()
        {
            NoteId = Guid.NewGuid(), UpdatedAt = T(side["updatedAt"]!.GetValue<string>()), DeviceId = side["deviceId"]!.GetValue<string>()
        };

        foreach (var vector in Load("sync-format4.json")["versionOrder"]!.AsArray().Select(n => n!.AsObject()))
            Assert.Equal(vector["expectedSign"]!.GetValue<int>(), Math.Sign(SyncVersion.Compare(Of(vector["a"]!), Of(vector["b"]!))));
    }

    [Theory]
    [MemberData(nameof(VectorFiles))]
    public void SyncKey_EncodingMatchesTheFile(string file)
    {
        var data = Load(file);
        Assert.Equal(data["syncKeyHex"]!.GetValue<string>(), Convert.ToHexString(Key).ToLowerInvariant());
        Assert.Equal(data["syncKeyEncoded"]!.GetValue<string>(), SyncKeyFormat.Encode(Key));
        Assert.Equal(Key, SyncKeyFormat.Decode(data["syncKeyEncoded"]!.GetValue<string>()));
    }
}
