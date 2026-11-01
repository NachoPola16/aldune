namespace Aldune.Core;

using System.Text.Json;

/// <summary>Transporte sin servidor: sirve para una carpeta local, UNC, NAS o carpeta de Syncthing/Drive.</summary>
public sealed class FolderSyncTransport : ISyncTransport, ILookStore
{
    private readonly string _objectsPath;
    private readonly string _lookPath;

    public FolderSyncTransport(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) throw new ArgumentException("A sync folder is required.", nameof(folderPath));
        _objectsPath = Path.Combine(folderPath, "objects");
        _lookPath = Path.Combine(folderPath, "look.aldune");
        Directory.CreateDirectory(_objectsPath);
    }

    public IReadOnlyList<SyncRemoteObject> ReadAll()
    {
        var result = new List<SyncRemoteObject>();
        foreach (var path in Directory.EnumerateFiles(_objectsPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                result.Add(new SyncRemoteObject(SyncEnvelopeCodec.Deserialize(bytes), bytes));
            }
            catch (FormatException)
            {
                // Un fichero incompleto o de otra versión no debe impedir que se sincronicen los demás.
                // Se conservará para que el usuario pueda inspeccionarlo o recuperarlo manualmente.
            }
            catch (JsonException)
            {
                // Igual que arriba: una escritura interrumpida no invalida todo el almacén.
            }
        }
        return result;
    }

    public void Write(SyncEnvelope envelope)
    {
        var bytes = SyncEnvelopeCodec.Serialize(envelope);
        var finalPath = Path.Combine(_objectsPath, $"{envelope.NoteId:N}.json");
        if (File.Exists(finalPath))
        {
            try
            {
                var current = SyncEnvelopeCodec.Deserialize(File.ReadAllBytes(finalPath));
                if (SyncVersion.Compare(envelope, current) < 0) return;
            }
            catch (FormatException)
            {
                // Sustituir un archivo inválido es la forma de recuperar el objeto sin intervención.
            }
        }
        var tempPath = Path.Combine(_objectsPath, $".{envelope.NoteId:N}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, finalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public void Delete(Guid noteId)
    {
        var path = Path.Combine(_objectsPath, $"{noteId:N}.json");
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>The look document, in the folder's root: <c>ReadAll</c> lists only <c>objects/*.json</c>, so no version ever mistakes it for a note.</summary>
    public byte[]? ReadLook()
    {
        var info = new FileInfo(_lookPath);
        if (!info.Exists || info.Length > ILookStore.MaxBytes) return null;
        return File.ReadAllBytes(_lookPath);
    }

    public void WriteLook(byte[] sealedLook)
    {
        // Same as the notes: a temporary file and a move, so a reader never sees half a document.
        var tempPath = Path.Combine(Path.GetDirectoryName(_lookPath)!, $".look.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(tempPath, sealedLook);
            File.Move(tempPath, _lookPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
