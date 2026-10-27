using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Un objeto remoto que el transporte no pudo leer (WebDAV por encima del tope de respuesta) no es un objeto que no exista: subir encima
/// la copia local pisaría lo que otro dispositivo escribió, sin conflicto, y repetiría la subida en cada pasada.
/// </summary>
public sealed class SyncUnreadableRemoteTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-unreadable-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));

    public void Dispose()
    {
        _cipher.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private sealed class FakeTransport(IReadOnlyCollection<Guid> unreadable) : ISyncTransport
    {
        public List<Guid> Written { get; } = new();
        public IReadOnlyCollection<Guid> UnreadableIds => unreadable;
        public IReadOnlyList<SyncRemoteObject> ReadAll() => Array.Empty<SyncRemoteObject>();
        public void Write(SyncEnvelope envelope) => Written.Add(envelope.NoteId);
        public void Delete(Guid noteId) { }
    }

    [Fact]
    public void ANoteWhoseRemoteObjectCouldNotBeRead_IsNeitherUploadedNorSettled()
    {
        Directory.CreateDirectory(_dir);
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "notes.db")), _cipher);
        var settings = new AppSettings { SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = _dir };
        var service = new SyncService(repository, settings, new SettingsService(Path.Combine(_dir, "settings.json")));
        service.GetOrCreateSyncCode();
        var blocked = repository.Create("nota enorme en el almacén", "#EBD38B", "primary");
        var normal = repository.Create("nota normal", "#EBD38B", "primary");
        var transport = new FakeTransport([blocked.Id]);
        service.TransportFactoryOverride = () => transport;

        var result = service.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new[] { normal.Id }, transport.Written);
        Assert.False(repository.GetSyncBases().ContainsKey(blocked.Id));
    }
}
