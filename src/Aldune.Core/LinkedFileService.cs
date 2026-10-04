using System.Text.RegularExpressions;

namespace Aldune.Core;

public enum LinkOutcome { Linked, AlreadyLinked, UnsupportedExtension, TooLarge, UnsupportedEncoding, Unreadable }

public sealed record LinkResult(LinkOutcome Outcome, Guid? NoteId = null);

public enum ReconcileOutcome { Unchanged, Written, Reloaded, Conflict, Unavailable, NotLinked }

public sealed record ReconcileResult(ReconcileOutcome Outcome, Guid? ConflictNoteId = null);

/// <summary>
/// Pone de acuerdo una nota vinculada y su archivo (spec, decisiones 3 a 5). Todo lo que toca el disco pasa
/// por aquí, para probarlo con carpetas temporales: la interfaz solo decide cuándo llamar. Puede tardar (una
/// unidad de red), así que la interfaz lo llama desde un hilo de fondo, de uno en uno.
/// </summary>
public sealed partial class LinkedFileService
{
    private const int StableReadAttempts = 5;
    private static readonly TimeSpan StableReadDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan OrphanAge = TimeSpan.FromMinutes(1);

    private readonly NotesRepository _repository;
    private readonly Func<string> _nextColor;
    private readonly Func<string> _conflictPrefix;
    private readonly Action<TimeSpan> _sleep;

    public LinkedFileService(NotesRepository repository, Func<string> nextColor, Func<string> conflictPrefix,
        Action<TimeSpan>? sleep = null)
    {
        _repository = repository;
        _nextColor = nextColor;
        _conflictPrefix = conflictPrefix;
        _sleep = sleep ?? Thread.Sleep;
    }

    // Lo único que Aldune borra en una carpeta del usuario: sus propios temporales, con este nombre exacto.
    [GeneratedRegex("^\\.~aldune-[0-9a-f]{32}\\.tmp$")]
    private static partial Regex OwnTemporary();

    public LinkResult Open(string path)
    {
        var (validated, rejection) = Validate(path);
        if (validated is null) return new LinkResult(rejection);
        if (_repository.FindNoteByLinkedPath(validated.Value.Path) is { } existing)
            return new LinkResult(LinkOutcome.AlreadyLinked, existing);

        var (full, bytes, content) = validated.Value;
        var text = MarkdownLink.ToNoteText(content.Text);
        var note = _repository.Create(text, _nextColor(), "primary");
        _repository.SaveFileLink(new NoteFileLink(note.Id, full, SyncEnabled: false,
            LinkedFileFormat.Hash(bytes), WriteTime(full), LinkedFileFormat.HashText(text)));
        return new LinkResult(LinkOutcome.Linked, note.Id);
    }

    /// <summary>"Buscar…": otra ruta para la misma nota. La huella conocida se descarta, así que el siguiente
    /// <see cref="Reconcile"/> trata el archivo como cambiado: sin cambios pendientes manda él; con ellos, conflicto.</summary>
    public LinkResult Relink(Guid noteId, string newPath)
    {
        var link = _repository.GetFileLink(noteId);
        if (link is null) return new LinkResult(LinkOutcome.Unreadable);
        var (validated, rejection) = Validate(newPath);
        if (validated is null) return new LinkResult(rejection);
        if (_repository.FindNoteByLinkedPath(validated.Value.Path) is { } other && other != noteId)
            return new LinkResult(LinkOutcome.AlreadyLinked, other);

        _repository.SaveFileLink(link with { Path = validated.Value.Path, KnownHash = null, KnownWriteTime = null });
        return new LinkResult(LinkOutcome.Linked, noteId);
    }

    /// <summary>"Convertir en nota normal": la nota se queda con su texto; el archivo, donde estaba.</summary>
    public void Unlink(Guid noteId) => _repository.DeleteFileLink(noteId);

    /// <summary>"Guardar como archivo vinculado…": crea el archivo (UTF-8 sin BOM, CRLF) y vincula la nota.
    /// Nunca sobre un archivo que ya existe: eso sería reemplazar algo del usuario con una nota.</summary>
    public LinkResult SaveAs(Guid noteId, string path)
    {
        if (!LinkedFileFormat.IsSupportedExtension(path)) return new LinkResult(LinkOutcome.UnsupportedExtension);
        if (_repository.GetById(noteId) is not { IsProtected: false } note) return new LinkResult(LinkOutcome.Unreadable);
        try
        {
            var full = Path.GetFullPath(path);
            var bytes = LinkedFileFormat.Encode(MarkdownLink.ToFileText(note.Text, ""), LinkedEncoding.Utf8);
            using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write))
                stream.Write(bytes);
            _repository.SaveFileLink(new NoteFileLink(noteId, full, SyncEnabled: false,
                LinkedFileFormat.Hash(bytes), WriteTime(full), LinkedFileFormat.HashText(note.Text)));
            return new LinkResult(LinkOutcome.Linked, noteId);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return new LinkResult(LinkOutcome.Unreadable);
        }
    }

    public void SetSync(Guid noteId, bool enabled)
    {
        if (_repository.GetFileLink(noteId) is { } link) _repository.SaveFileLink(link with { SyncEnabled = enabled });
    }

    public ReconcileResult Reconcile(Guid noteId)
    {
        if (_repository.GetFileLink(noteId) is not { } first) return new ReconcileResult(ReconcileOutcome.NotLinked);

        var bytes = TryReadStable(first.Path);
        // Vínculo y nota se leen DESPUÉS de la espera a que el archivo se asiente (≥ 300 ms, más en red): lo que
        // se tecleó o se cambió en ese tiempo cuenta como pendiente. Decidir con una foto anterior hacía que
        // Reload pisara lo tecleado, o que lo recargado se sobrescribiera después (revisión final, C1).
        var link = _repository.GetFileLink(noteId);
        var note = _repository.GetById(noteId);
        if (link is null || note is null) return new ReconcileResult(ReconcileOutcome.NotLinked);
        // Un archivo que pasa a ser ilegible (otra codificación, demasiado grande) se trata como no disponible:
        // escribir encima lo estropearía y recargarlo no se puede.
        var content = bytes is null ? null : LinkedFileFormat.Decode(bytes).Content;
        string? currentHash = content is null ? null : LinkedFileFormat.Hash(bytes!);
        bool pending = LinkedFileFormat.HashText(note.Text) != link.KnownTextHash;

        switch (LinkedFileDecision.Decide(link.KnownHash, currentHash, pending))
        {
            case LinkedFileAction.None:
                return new ReconcileResult(ReconcileOutcome.Unchanged);
            case LinkedFileAction.Unavailable:
                return new ReconcileResult(ReconcileOutcome.Unavailable);
            case LinkedFileAction.Write:
                return Write(note, link, bytes!, content!);
            case LinkedFileAction.Reload:
                Reload(note, link, bytes!, content!);
                return new ReconcileResult(ReconcileOutcome.Reloaded);
            default:
                // Conflicto: el archivo no se toca. Lo de Aldune va a una nota normal con las mismas etiquetas,
                // para que siga en la misma vista del dock, y la vinculada toma lo del disco.
                var copy = _repository.Create(_conflictPrefix() + note.Text, note.Color, note.ScreenOrigin);
                if (note.Tags.Count > 0) _repository.SetTags(copy.Id, note.Tags);
                Reload(note, link, bytes!, content!);
                return new ReconcileResult(ReconcileOutcome.Conflict, copy.Id);
        }
    }

    /// <summary>Comprobación barata para el sondeo: ¿ha cambiado la fecha de escritura, o el archivo ya no está?
    /// Si dice que sí, se llama a <see cref="Reconcile"/>, que es quien decide de verdad.</summary>
    public static bool LooksChanged(NoteFileLink link)
    {
        try
        {
            var info = new FileInfo(link.Path);
            return !info.Exists || link.KnownWriteTime is not { } known || info.LastWriteTimeUtc != known.UtcDateTime;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return true;
        }
    }

    private ReconcileResult Write(Note note, NoteFileLink link, byte[] current, LinkedFileContent content)
    {
        var bytes = LinkedFileFormat.Encode(MarkdownLink.ToFileText(note.Text, content.Text), content.Encoding);
        var textHash = LinkedFileFormat.HashText(note.Text);

        // Mismos bytes (p. ej. solo cambió cómo une el editor título y cuerpo): no se reescribe el archivo,
        // que cambiaría su fecha y despertaría a otros programas que lo vigilan.
        if (bytes.AsSpan().SequenceEqual(current))
        {
            _repository.UpdateFileLinkKnownTextHash(note.Id, textHash);
            return new ReconcileResult(ReconcileOutcome.Unchanged);
        }

        try
        {
            AtomicWrite(link.Path, bytes, expectedHash: LinkedFileFormat.Hash(current));
        }
        catch (FileChangedException)
        {
            // Lo cambió otro programa justo ahora: su aviso traerá la siguiente pasada, que dará conflicto.
            return new ReconcileResult(ReconcileOutcome.Unchanged);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return new ReconcileResult(ReconcileOutcome.Unavailable);
        }

        _repository.UpdateFileLinkKnownState(note.Id, LinkedFileFormat.Hash(bytes), WriteTime(link.Path), textHash);
        return new ReconcileResult(ReconcileOutcome.Written);
    }

    private void Reload(Note note, NoteFileLink link, byte[] bytes, LinkedFileContent content)
    {
        var text = MarkdownLink.ToNoteText(content.Text);
        if (text != note.Text) _repository.UpdateText(note.Id, text);
        _repository.UpdateFileLinkKnownState(note.Id, LinkedFileFormat.Hash(bytes), WriteTime(link.Path),
            LinkedFileFormat.HashText(text));
    }

    private static ((string Path, byte[] Bytes, LinkedFileContent Content)? Value, LinkOutcome Rejection) Validate(string path)
    {
        if (!LinkedFileFormat.IsSupportedExtension(path)) return (null, LinkOutcome.UnsupportedExtension);
        try
        {
            var full = Path.GetFullPath(path);
            // Se mira el tamaño antes de leer: un archivo enorme no se carga entero para rechazarlo después.
            if (new FileInfo(full).Length > LinkedFileFormat.MaxBytes) return (null, LinkOutcome.TooLarge);
            var bytes = File.ReadAllBytes(full);
            var (content, rejection) = LinkedFileFormat.Decode(bytes);
            if (content is null)
                return (null, rejection == LinkedFileRejection.TooLarge ? LinkOutcome.TooLarge : LinkOutcome.UnsupportedEncoding);
            return ((full, bytes, content), LinkOutcome.Linked);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return (null, LinkOutcome.Unreadable);
        }
    }

    // Los editores guardan en varios pasos (vaciar, escribir, renombrar): se lee hasta que dos lecturas
    // seguidas coinciden, para no decidir sobre un archivo a medio guardar.
    private byte[]? TryReadStable(string path)
    {
        try
        {
            var previous = File.ReadAllBytes(path);
            for (int attempt = 1; attempt < StableReadAttempts; attempt++)
            {
                _sleep(StableReadDelay);
                var next = File.ReadAllBytes(path);
                if (next.AsSpan().SequenceEqual(previous)) return next;
                previous = next;
            }
            return previous;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            return null;
        }
    }

    // Temporal en la misma carpeta y File.Replace: un corte a mitad no deja el archivo a medias. Si la unidad
    // no admite Replace (algunas de red), se escribe en el sitio; el texto sigue a salvo en la base de datos.
    private static void AtomicWrite(string path, byte[] bytes, string expectedHash)
    {
        var directory = Path.GetDirectoryName(path)!;
        CleanOrphans(directory);
        var temporary = Path.Combine(directory, $".~aldune-{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(temporary, bytes);
        try
        {
            // La última comprobación, justo antes de sustituir: nunca sobre una huella desconocida.
            if (LinkedFileFormat.Hash(File.ReadAllBytes(path)) != expectedHash) throw new FileChangedException();
            try
            {
                File.Replace(temporary, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // Sin el archivo no se escribe: el respaldo no debe volver a crear algo que el usuario ha borrado.
                if (!File.Exists(path)) throw new FileNotFoundException(null, path);
                File.WriteAllBytes(path, bytes);
            }
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void CleanOrphans(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, ".~aldune-*.tmp"))
            {
                if (OwnTemporary().IsMatch(Path.GetFileName(file)) &&
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > OrphanAge)
                    TryDelete(file);
            }
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // Limpiar es un extra: si la carpeta no se deja listar, se sigue.
        }
    }

    private static void TryDelete(string file)
    {
        try { if (File.Exists(file)) File.Delete(file); }
        catch (Exception ex) when (IsFileError(ex)) { }
    }

    private static DateTimeOffset? WriteTime(string path)
    {
        try { return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero); }
        catch (Exception ex) when (IsFileError(ex)) { return null; }
    }

    private static bool IsFileError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException
            or System.Security.SecurityException;

    private sealed class FileChangedException : IOException;
}
