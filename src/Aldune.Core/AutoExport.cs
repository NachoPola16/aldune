using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aldune.Core;

/// <summary>Una nota tal como la ve la copia automática: solo lo que hace falta para decidir qué escribir.</summary>
public sealed record ExportNote(Guid Id, string Text, NoteState State, bool IsProtected);

/// <summary>Lo que hay que hacer en la carpeta de copia. Nunca hay un "borrar": solo crear, reescribir lo propio y mover.</summary>
public abstract record ExportAction
{
    public sealed record Write(string Path, string Content) : ExportAction;
    public sealed record Move(string From, string To) : ExportAction;
}

/// <summary>Un fichero que la app escribió: su ruta relativa, la huella de lo que escribió y, si el usuario lo tocó a mano, la copia
/// que se guarda a su lado (ver <see cref="AutoExportPlanner"/>).</summary>
public sealed record ExportEntry(string Path, string Hash, string? SidePath = null, string? SideHash = null);

/// <summary>
/// El manifiesto <c>.aldune-export.json</c> de la carpeta: qué ficheros son de Aldune (por id de nota) y con qué contenido. Es lo que permite
/// reescribir solo lo propio y notar que el usuario cambió algo a mano. Un manifiesto roto o ausente vale como vacío: entonces todo lo que
/// ya hay en la carpeta se trata como ajeno y no se pisa.
/// </summary>
public sealed class ExportManifest
{
    public const string FileName = ".aldune-export.json";

    public Dictionary<string, ExportEntry> Files { get; init; } = new();

    public string ToJson() => JsonSerializer.Serialize(new Dto { Files = Files }, new JsonSerializerOptions { WriteIndented = true });

    public static ExportManifest FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ExportManifest();
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(json);
            return new ExportManifest { Files = dto?.Files ?? new() };
        }
        catch (JsonException)
        {
            return new ExportManifest();
        }
    }

    private sealed class Dto
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, ExportEntry> Files { get; set; } = new();
    }
}

public sealed record AutoExportPlan(IReadOnlyList<ExportAction> Actions, ExportManifest Manifest);

/// <summary>
/// Decide qué escribir en la carpeta de la copia automática (spec de la fase 4). Pura: ve la carpeta solo a través de <c>readHash</c>
/// (huella SHA-256 de un fichero por su ruta relativa, o <c>null</c> si no existe), y devuelve las acciones y el manifiesto nuevo.
///
/// Reglas: un <c>.md</c> por nota activa (y archivada si se pide), con un nombre que se fija la primera vez y no cambia con el título; las
/// protegidas y la papelera quedan fuera; una nota que sale (borrada, a la papelera, protegida) mueve su fichero a <c>_eliminadas/</c> si
/// nadie lo tocó; nunca se borra nada ni se pisa un fichero que el usuario cambió o que no es nuestro (la copia va al lado como
/// <c>nombre (Aldune).md</c>).
/// </summary>
public static class AutoExportPlanner
{
    public const string ActiveDir = "notas";
    public const string ArchivedDir = "archivadas";
    public const string RemovedDir = "_eliminadas";
    private const string SideSuffix = "Aldune";

    /// <summary>Lo que queda en el fichero de una nota que pasó a estar protegida: su texto en claro no puede seguir en la carpeta de copia.</summary>
    public const string ProtectedStub = "# (nota protegida)\r\n\r\nEsta nota ahora está protegida con contraseña: su texto ya no se copia.";

    public static string HashOf(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static AutoExportPlan Plan(IReadOnlyList<ExportNote> notes, ExportManifest manifest, bool includeArchived, Func<string, string?> readHash)
    {
        var actions = new List<ExportAction>();
        var files = manifest.Files.Where(kv => IsSafe(kv.Value.Path) && (kv.Value.SidePath is null || IsSafe(kv.Value.SidePath)))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var used = new HashSet<string>(files.Values.SelectMany(e => new[] { e.Path, e.SidePath }).Where(p => p is not null)!, StringComparer.OrdinalIgnoreCase);

        var wanted = notes.Where(n => !n.IsProtected && (n.State == NoteState.Active || (includeArchived && n.State == NoteState.Archived)))
            .ToDictionary(n => n.Id.ToString());

        // Lo que ya no se exporta: su fichero se aparta si sigue como lo dejamos; si el usuario lo cambió, se deja donde está.
        foreach (var (id, entry) in files.Where(kv => !wanted.ContainsKey(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList())
        {
            files.Remove(id);
            if (readHash(entry.Path) != entry.Hash) continue;
            // Una nota que pasó a protegida: el fichero (nuestro, intacto) se reescribe sin su texto antes de apartarlo. No se borra nada: es reescribir lo propio.
            if (notes.Any(n => n.Id.ToString() == id && n.IsProtected)) actions.Add(new ExportAction.Write(entry.Path, ProtectedStub));
            var target = UniquePath(RemovedDir, FileNameOf(entry.Path), used, readHash, checkDisk: true);
            actions.Add(new ExportAction.Move(entry.Path, target));
        }

        foreach (var note in notes.Where(n => wanted.ContainsKey(n.Id.ToString())))
        {
            var id = note.Id.ToString();
            var dir = note.State == NoteState.Archived ? ArchivedDir : ActiveDir;
            var content = MarkdownExport.ToMarkdown(note.Text);
            var hash = HashOf(content);

            files.TryGetValue(id, out var entry);

            if (entry is not null && DirOf(entry.Path) != dir)
            {
                // Archivada o desarchivada: su fichero cambia de carpeta si es nuestro y sigue intacto.
                if (readHash(entry.Path) == entry.Hash)
                {
                    var moved = UniquePath(dir, FileNameOf(entry.Path), used, readHash, checkDisk: false);
                    actions.Add(new ExportAction.Move(entry.Path, moved));
                    entry = entry with { Path = moved, SidePath = null, SideHash = null };
                    if (entry.Hash != hash)
                    {
                        actions.Add(new ExportAction.Write(moved, content));
                        entry = entry with { Hash = hash };
                    }
                    files[id] = entry;
                    continue;
                }
                entry = null;   // el usuario lo cambió: se queda donde está y la nota empieza un fichero nuevo
            }

            if (entry is null)
            {
                var path = UniquePath(dir, TitleBase(note.Text), used, readHash, checkDisk: false);
                var disk = readHash(path);
                if (disk is null)
                {
                    actions.Add(new ExportAction.Write(path, content));
                }
                else if (disk != hash)
                {
                    path = SidePathFor(path, hash, ownSideHash: null, used, readHash, out var write);
                    if (write) actions.Add(new ExportAction.Write(path, content));
                }
                files[id] = new ExportEntry(path, hash);
                continue;
            }

            var current = readHash(entry.Path);
            if (current is null || current == entry.Hash)
            {
                if (current is null || entry.Hash != hash) actions.Add(new ExportAction.Write(entry.Path, content));
                files[id] = entry with { Hash = hash };
            }
            else if (current == hash)
            {
                files[id] = entry with { Hash = hash };
            }
            else if (entry.Hash != hash)
            {
                // El usuario cambió el fichero a mano y la nota también cambió: la copia nueva va al lado.
                var side = entry.SidePath ?? SidePathFor(entry.Path, hash, null, used, readHash, out _);
                var sideDisk = readHash(side);
                if (entry.SidePath is not null && sideDisk is not null && sideDisk != hash && sideDisk != entry.SideHash)
                    side = SidePathFor(entry.Path, hash, null, used, readHash, out _);   // la de al lado también se tocó: otra distinta
                if (readHash(side) != hash) actions.Add(new ExportAction.Write(side, content));
                files[id] = entry with { SidePath = side, SideHash = hash };
            }
        }

        return new AutoExportPlan(actions, new ExportManifest { Files = files });
    }

    /// <summary>Ruta de la copia que va junto a <paramref name="mainPath"/>: <c>nombre (Aldune).md</c>, <c>(Aldune 2)</c>… la primera que no
    /// exista o que ya tenga justo este contenido. <paramref name="write"/> dice si hay que escribirla.</summary>
    private static string SidePathFor(string mainPath, string hash, string? ownSideHash, HashSet<string> used, Func<string, string?> readHash, out bool write)
    {
        var dir = DirOf(mainPath);
        var name = FileNameOf(mainPath)[..^3];
        for (var n = 1; ; n++)
        {
            var candidate = $"{dir}/{name} ({SideSuffix}{(n > 1 ? " " + n : "")}).md";
            var disk = readHash(candidate);
            if (disk is null || disk == ownSideHash)
            {
                used.Add(candidate);
                write = true;
                return candidate;
            }
            if (disk == hash)
            {
                used.Add(candidate);
                write = false;
                return candidate;
            }
        }
    }

    private static string TitleBase(string text)
    {
        var name = MarkdownExport.SuggestedFileName(text);
        return name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
    }

    /// <summary>Una ruta libre en <paramref name="dir"/>: ni usada por otra nota ni (con <paramref name="checkDisk"/>) ya existente.</summary>
    private static string UniquePath(string dir, string fileName, HashSet<string> used, Func<string, string?> readHash, bool checkDisk)
    {
        var baseName = fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? fileName[..^3] : fileName;
        for (var n = 1; ; n++)
        {
            var candidate = $"{dir}/{baseName}{(n > 1 ? $" ({n})" : "")}.md";
            if (!used.Contains(candidate) && (!checkDisk || readHash(candidate) is null))
            {
                used.Add(candidate);
                return candidate;
            }
        }
    }

    private static string DirOf(string path) => path[..path.LastIndexOf('/')];

    private static string FileNameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Solo rutas relativas de dos niveles (<c>carpeta/fichero.md</c>) sin salir de la carpeta de copia: un manifiesto manipulado
    /// no puede hacer que se toque nada fuera de ella.</summary>
    private static bool IsSafe(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path.StartsWith('/')) return false;
        var parts = path.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && p != "." && p != "..") && parts[1].EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Aplica un <see cref="AutoExportPlan"/> sobre una carpeta real. La carpeta raíz tiene que existir ya: si el usuario la borró o desconectó el
/// disco, no se recrea (se avisa con la excepción) para no esparcir ficheros donde no se pidió.
/// </summary>
public static class AutoExportRunner
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>Devuelve cuántas acciones se aplicaron. Lanza <see cref="IOException"/> o <see cref="UnauthorizedAccessException"/> si la
    /// carpeta no se puede usar; el manifiesto se guarda aunque falle una acción a medias.</summary>
    public static int Run(string root, IReadOnlyList<ExportNote> notes, bool includeArchived)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var manifestPath = Path.Combine(root, ExportManifest.FileName);
        var manifest = ExportManifest.FromJson(File.Exists(manifestPath) ? File.ReadAllText(manifestPath, Encoding.UTF8) : null);

        string? HashOnDisk(string relative)
        {
            var full = Path.Combine(root, relative);
            return File.Exists(full) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))) : null;
        }

        var plan = AutoExportPlanner.Plan(notes, manifest, includeArchived, HashOnDisk);
        if (plan.Actions.Count == 0 && JsonEquals(manifest, plan.Manifest)) return 0;

        try
        {
            foreach (var action in plan.Actions)
            {
                switch (action)
                {
                    case ExportAction.Write w:
                        var target = Path.Combine(root, w.Path);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.WriteAllText(target, w.Content, Utf8NoBom);
                        break;
                    case ExportAction.Move m:
                        var to = Path.Combine(root, m.To);
                        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                        File.Move(Path.Combine(root, m.From), to);
                        break;
                }
            }
        }
        finally
        {
            // Atómico: en un fichero temporal y luego reemplazo, para que un corte a medias no deje un manifiesto roto (que se leería como vacío).
            var temp = manifestPath + ".tmp";
            File.WriteAllText(temp, plan.Manifest.ToJson(), Utf8NoBom);
            File.Move(temp, manifestPath, overwrite: true);
        }
        return plan.Actions.Count;
    }

    private static bool JsonEquals(ExportManifest a, ExportManifest b) => a.ToJson() == b.ToJson();
}
