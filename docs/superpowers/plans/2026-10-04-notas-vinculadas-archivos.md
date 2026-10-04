# Notas vinculadas a archivos — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** que una nota de Aldune pueda *ser* un archivo `.md`/`.markdown`/`.txt` del disco: se abre, se edita en
Aldune y el archivo cambia, sin perder nunca nada del archivo ni de la nota.

**Architecture:** toda la lógica que decide y toca archivos va en Core y se prueba con carpetas temporales:
`LinkedFileFormat` (bytes ↔ texto), `MarkdownLink` (texto del archivo ↔ texto de la nota, conservando
byte a byte lo no editado), `LinkedFileDecision` (escribir, recargar, conflicto, no disponible),
`NoteFileLink` en el repositorio y `LinkedFileService`, que lo orquesta. WPF solo vigila (watcher + sondeo
en un hilo de fondo con una cola en serie), aplica resultados a las ventanas y ofrece los menús.

**Tech Stack:** .NET 10, WPF, Microsoft.Data.Sqlite, xUnit, Inno Setup 6.

**Spec:** `docs/superpowers/specs/2026-10-04-notas-vinculadas-archivos-design.md` (leerla antes de empezar).

## Global Constraints

- Comentarios en español, explicando el porqué; `.cs`/`.xaml` con BOM UTF-8; documentación sin BOM.
- Textos de interfaz solo con `Strings.T(en, es, de, fr, pt)` en `src/Aldune/Resources/Strings.cs`, los cinco.
- Colores del chrome solo con `{DynamicResource Aldune<Clave>Brush}` / `SetResourceReference`; nada de hex.
- TDD en Core: test primero, verlo fallar, implementar, verlo pasar.
- Formato de sync: sigue en **4**. Nada del vínculo entra en el sobre.
- **Aldune nunca borra un archivo vinculado.** Lo único que puede borrar en una carpeta del usuario es un
  temporal propio huérfano cuyo nombre case exactamente con `^\.~aldune-[0-9a-f]{32}\.tmp$`.
- **Nunca se escribe sobre un archivo cuya huella no es la conocida.**
- Extensiones: `.md`, `.markdown`, `.txt`. Tope: 2 MB (2 097 152 bytes). Codificaciones: UTF-8 con o sin
  BOM, UTF-16 LE/BE con BOM; el resto se rechaza.
- Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`; sondas con datos temporales y fuera
  del repositorio (`docs/WPF_PROBES.md`). Sondas que mueven ratón/teclado y el smoke test: avisar al
  usuario y esperar su "ok".
- Commits sin `Co-Authored-By`, **añadiendo los ficheros por nombre** (nunca `git add -A`: puede haber otra
  sesión trabajando en el mismo árbol).
- Comandos: `dotnet build Aldune.slnx -c Debug`, `dotnet test Aldune.slnx --no-build` (tras compilar; nunca
  dar por buenos tests sobre binarios viejos), solo los 4 avisos CA1416 conocidos.

## Review Focus

1. **Una nota que ya se sincronizaba y pasa a vinculada sin sync**: la copia remota no debe sobrescribirla
   ni borrarla (la sync aplica lo remoto cuando no hay versión local en el alcance). Test en la tarea 5.
2. **Archivo con saltos LF** editado en el `TextBox` (que mete CRLF al pulsar Enter, y `NoteText.Join` une
   título y cuerpo con CRLF): las líneas no tocadas conservan su LF. Test en la tarea 2.
3. **Abrir y cerrar sin editar no reescribe el archivo**, aunque el texto de la nota difiera en saltos de
   línea del traducido: si los bytes a escribir son iguales a los del disco, no se escribe. Test en la tarea 6.
4. **Escribir cuando el archivo acaba de desaparecer**: el respaldo de `File.Replace` no debe volver a
   crearlo. Test en la tarea 6.
5. **Mover una línea con Alt+↑** conserva sus bytes originales (`* [X] algo` no se reescribe como
   `- [x] algo`). Test en la tarea 2.

---

## Tanda 1

### Task 1: `LinkedFileFormat` — bytes ↔ texto, rechazos y huellas

**Files:**
- Create: `src/Aldune.Core/LinkedFileFormat.cs`
- Test: `tests/Aldune.Core.Tests/LinkedFileFormatTests.cs`

**Interfaces:**
- Produces:
  - `enum LinkedEncoding { Utf8, Utf8Bom, Utf16LE, Utf16BE }`
  - `enum LinkedFileRejection { None, TooLarge, UnsupportedEncoding }`
  - `sealed record LinkedFileContent(string Text, LinkedEncoding Encoding)`
  - `static class LinkedFileFormat`: `const int MaxBytes`, `IReadOnlyList<string> Extensions`,
    `bool IsSupportedExtension(string path)`, `(LinkedFileContent? Content, LinkedFileRejection Rejection) Decode(byte[] bytes)`,
    `byte[] Encode(string text, LinkedEncoding encoding)`, `string Hash(byte[] bytes)`, `string HashText(string text)`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using Xunit;

namespace Aldune.Core.Tests;

public class LinkedFileFormatTests
{
    [Theory]
    [InlineData("apuntes.md", true)]
    [InlineData("APUNTES.MD", true)]
    [InlineData("tema.markdown", true)]
    [InlineData("lista.txt", true)]
    [InlineData("foto.png", false)]
    [InlineData("sin-extension", false)]
    public void IsSupportedExtension(string path, bool expected) =>
        Assert.Equal(expected, LinkedFileFormat.IsSupportedExtension(path));

    [Fact]
    public void Utf8WithoutBom_RoundTripsByteForByte()
    {
        var bytes = Encoding.UTF8.GetBytes("# Tema\n- [ ] repasar ñ y tildes\r\n");
        var (content, rejection) = LinkedFileFormat.Decode(bytes);

        Assert.Equal(LinkedFileRejection.None, rejection);
        Assert.Equal(LinkedEncoding.Utf8, content!.Encoding);
        Assert.Equal("# Tema\n- [ ] repasar ñ y tildes\r\n", content.Text);
        Assert.Equal(bytes, LinkedFileFormat.Encode(content.Text, content.Encoding));
    }

    [Fact]
    public void Utf8WithBom_KeepsTheBom()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("hola")];
        var (content, _) = LinkedFileFormat.Decode(bytes);

        Assert.Equal(LinkedEncoding.Utf8Bom, content!.Encoding);
        Assert.Equal("hola", content.Text);
        Assert.Equal(bytes, LinkedFileFormat.Encode(content.Text, content.Encoding));
    }

    [Fact]
    public void Utf16LittleAndBigEndian_RoundTrip()
    {
        byte[] le = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("añadir")];
        byte[] be = [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("añadir")];

        var (leContent, _) = LinkedFileFormat.Decode(le);
        var (beContent, _) = LinkedFileFormat.Decode(be);

        Assert.Equal((LinkedEncoding.Utf16LE, "añadir"), (leContent!.Encoding, leContent.Text));
        Assert.Equal((LinkedEncoding.Utf16BE, "añadir"), (beContent!.Encoding, beContent.Text));
        Assert.Equal(le, LinkedFileFormat.Encode(leContent.Text, leContent.Encoding));
        Assert.Equal(be, LinkedFileFormat.Encode(beContent.Text, beContent.Encoding));
    }

    [Fact]
    public void AnsiText_IsRejected()
    {
        // "año" en Windows-1252: 0xF1 suelto no es UTF-8 válido. Guardarlo como UTF-8 estropearía la ñ.
        byte[] ansi = [0x61, 0xF1, 0x6F];
        Assert.Equal(LinkedFileRejection.UnsupportedEncoding, LinkedFileFormat.Decode(ansi).Rejection);
    }

    [Fact]
    public void BinaryWithNulBytes_IsRejected()
    {
        byte[] binary = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00];
        Assert.Equal(LinkedFileRejection.UnsupportedEncoding, LinkedFileFormat.Decode(binary).Rejection);
    }

    [Fact]
    public void LargerThanTheLimit_IsRejected()
    {
        var bytes = new byte[LinkedFileFormat.MaxBytes + 1];
        Array.Fill(bytes, (byte)'a');
        Assert.Equal(LinkedFileRejection.TooLarge, LinkedFileFormat.Decode(bytes).Rejection);
    }

    [Fact]
    public void EmptyFile_IsUtf8AndEmpty()
    {
        var (content, rejection) = LinkedFileFormat.Decode([]);
        Assert.Equal(LinkedFileRejection.None, rejection);
        Assert.Equal(("", LinkedEncoding.Utf8), (content!.Text, content.Encoding));
    }

    [Fact]
    public void Hash_IsStableAndDistinguishesContent()
    {
        Assert.Equal(LinkedFileFormat.Hash([1, 2, 3]), LinkedFileFormat.Hash([1, 2, 3]));
        Assert.NotEqual(LinkedFileFormat.Hash([1, 2, 3]), LinkedFileFormat.Hash([1, 2, 4]));
        Assert.Equal(LinkedFileFormat.HashText("a\r\nb"), LinkedFileFormat.HashText("a\r\nb"));
        Assert.NotEqual(LinkedFileFormat.HashText("a\r\nb"), LinkedFileFormat.HashText("a\nb"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Aldune.slnx -c Debug` → Expected: error CS0103/CS0246 (`LinkedFileFormat` no existe).

- [ ] **Step 3: Implement**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Aldune.Core;

public enum LinkedEncoding { Utf8, Utf8Bom, Utf16LE, Utf16BE }

public enum LinkedFileRejection { None, TooLarge, UnsupportedEncoding }

public sealed record LinkedFileContent(string Text, LinkedEncoding Encoding);

/// <summary>
/// Cómo se leen y se escriben los bytes de un archivo vinculado (ver la spec de notas vinculadas). Se
/// guarda la codificación con la que llegó para escribirlo igual: un archivo con BOM sigue con BOM. Lo
/// que no sea UTF-8 o UTF-16 con BOM se rechaza en vez de adivinar: guardar como UTF-8 un archivo ANSI
/// estropearía sus tildes, y eso sería perder datos de un archivo que no es de Aldune.
/// </summary>
public static class LinkedFileFormat
{
    /// <summary>El mismo tope que el editor de notas.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    public static IReadOnlyList<string> Extensions { get; } = [".md", ".markdown", ".txt"];

    // throwOnInvalidBytes: un byte que no es UTF-8 tiene que dar error, no un carácter de sustitución.
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding StrictUtf16LE = new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding StrictUtf16BE = new(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    public static bool IsSupportedExtension(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static (LinkedFileContent? Content, LinkedFileRejection Rejection) Decode(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) return (null, LinkedFileRejection.TooLarge);
        try
        {
            var content = bytes switch
            {
                [0xEF, 0xBB, 0xBF, ..] => new LinkedFileContent(StrictUtf8.GetString(bytes, 3, bytes.Length - 3), LinkedEncoding.Utf8Bom),
                [0xFF, 0xFE, ..] => new LinkedFileContent(StrictUtf16LE.GetString(bytes, 2, bytes.Length - 2), LinkedEncoding.Utf16LE),
                [0xFE, 0xFF, ..] => new LinkedFileContent(StrictUtf16BE.GetString(bytes, 2, bytes.Length - 2), LinkedEncoding.Utf16BE),
                _ => new LinkedFileContent(StrictUtf8.GetString(bytes), LinkedEncoding.Utf8),
            };
            // Un NUL no aparece en texto: es un binario que por casualidad es UTF-8 válido.
            return content.Text.Contains('\0') ? (null, LinkedFileRejection.UnsupportedEncoding) : (content, LinkedFileRejection.None);
        }
        catch (DecoderFallbackException)
        {
            return (null, LinkedFileRejection.UnsupportedEncoding);
        }
    }

    public static byte[] Encode(string text, LinkedEncoding encoding) => encoding switch
    {
        LinkedEncoding.Utf8Bom => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)],
        LinkedEncoding.Utf16LE => [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)],
        LinkedEncoding.Utf16BE => [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(text)],
        _ => Encoding.UTF8.GetBytes(text),
    };

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static string HashText(string text) => Hash(Encoding.UTF8.GetBytes(text));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build --filter LinkedFileFormatTests`
Expected: PASS (12 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/LinkedFileFormat.cs tests/Aldune.Core.Tests/LinkedFileFormatTests.cs
git commit -m "Notas vinculadas: LinkedFileFormat (codificaciones, rechazos y huellas)"
```

---

### Task 2: `MarkdownLink` — traducción con garantía de "lo que no tocas no cambia"

**Files:**
- Create: `src/Aldune.Core/MarkdownLink.cs`
- Test: `tests/Aldune.Core.Tests/MarkdownLinkTests.cs`

**Interfaces:**
- Consumes: `TaskLines.Unchecked`, `TaskLines.Checked`, `TaskLines.GlyphIndex(string)`, `TaskLines.IsChecked(string)`,
  `TaskLines.PrefixLength(string, int)`, `BulletLines.Prefix`, `BulletLines.GlyphIndex(string)`, `BulletLines.PrefixLength(string, int)`
  (ya existen en Core).
- Produces: `static class MarkdownLink`: `string ToNoteText(string fileText)`, `string ToFileText(string noteText, string originalFileText)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Xunit;

namespace Aldune.Core.Tests;

public class MarkdownLinkTests
{
    // Archivos difíciles: cada uno tiene que volver idéntico tras traducir y reconstruir sin editar.
    public static TheoryData<string> Corpus => new()
    {
        "",
        "\n",
        "\r\n\r\n",
        "solo una línea sin salto",
        "# Tema 3\r\n\r\n- [ ] repasar\r\n- [x] hecho\r\n- viñeta\r\n",
        "# Tema\n- [ ] lf\n* [X] mayúscula\n+ más\n",
        "mezcla\r\n- [ ] crlf\n- lf\r\nfin",
        "  - [ ] sangría con espacios\n\t- [ ] sangría con tabulador\n    * anidada\n",
        "1. numerada\n2. [ ] numerada con casilla\n",
        "---\ntitle: cabecera YAML\ntags: [a, b]\n---\n- [ ] tarea\n",
        "```\n- [ ] dentro de código\n```\n- [ ] fuera\n",
        "~~~md\n* viñeta en código\n~~~\n",
        "espacios al final   \n- [ ] tarea con espacios   \n",
        "- [ ]\n- [x]\n- \n",
        "**negrita** y *cursiva*\n---\n- [x]sin espacio\n",
        "☐ glifo de Aldune escrito a mano\n→ flecha\n",
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void UneditedRoundTrip_IsByteForByte(string file) =>
        Assert.Equal(file, MarkdownLink.ToFileText(MarkdownLink.ToNoteText(file), file));

    [Fact]
    public void ToNoteText_TranslatesTasksAndBulletsKeepingIndentAndNewLines()
    {
        var note = MarkdownLink.ToNoteText("- [ ] a\r\n  * [X] b\n+ c\n1. d");
        Assert.Equal("☐ a\r\n  ☒ b\n→ c\n1. d", note);
    }

    [Fact]
    public void ToNoteText_LeavesCodeBlocksAlone()
    {
        var file = "```\n- [ ] código\n```\n- [ ] fuera\n";
        Assert.Equal("```\n- [ ] código\n```\n☐ fuera\n", MarkdownLink.ToNoteText(file));
    }

    [Fact]
    public void CheckingATask_ChangesOnlyThatLine()
    {
        var file = "# Lista\n* [ ] uno\n* [ ] dos   \n";
        var edited = MarkdownLink.ToNoteText(file).Replace("☐ uno", "☒ uno");

        Assert.Equal("# Lista\n- [x] uno\n* [ ] dos   \n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void NewLines_UseTheDominantNewLineAndBulletMarker()
    {
        var file = "* a\n* b\n";
        // El TextBox mete CRLF al pulsar Enter, aunque el archivo sea LF.
        var edited = MarkdownLink.ToNoteText(file).Replace("→ b\n", "→ b\r\n→ nueva\r\n☐ tarea\n");

        Assert.Equal("* a\n* b\n* nueva\n- [ ] tarea\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void JoinedTitleWithCrLf_KeepsTheOriginalLf()
    {
        // NoteText.Join une título y cuerpo con CRLF: la primera línea no ha cambiado, y su salto tampoco.
        var file = "# Tema\nlínea\n";
        var note = MarkdownLink.ToNoteText(file).Replace("# Tema\n", "# Tema\r\n");

        Assert.Equal(file, MarkdownLink.ToFileText(note, file));
    }

    [Fact]
    public void DeletingALine_RemovesOnlyThatLine()
    {
        var file = "- [ ] a\r\n- [ ] b\r\n- [ ] c\r\n";
        var edited = MarkdownLink.ToNoteText(file).Replace("☐ b\r\n", "");

        Assert.Equal("- [ ] a\r\n- [ ] c\r\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void MovingALine_KeepsItsOriginalBytes()
    {
        var file = "* [X] hecha\n- [ ] pendiente\n";
        var edited = "☐ pendiente\n☒ hecha\n";

        Assert.Equal("- [ ] pendiente\n* [X] hecha\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void NewTaskInsideACodeBlock_IsWrittenLiterally()
    {
        var file = "```\nx\n```\n";
        var edited = "```\nx\n☐ literal\n```\n";

        Assert.Equal("```\nx\n☐ literal\n```\n", MarkdownLink.ToFileText(edited, file));
    }

    [Fact]
    public void NewFile_UsesCrLfAndDashes()
    {
        Assert.Equal("Título\r\n- [ ] a\r\n- b", MarkdownLink.ToFileText("Título\r\n☐ a\r\n→ b", ""));
    }

    [Fact]
    public void AddingATrailingNewLine_KeepsTheRestIntact()
    {
        var file = "a\nb";
        Assert.Equal("a\nb\n", MarkdownLink.ToFileText("a\nb\r\n", file));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Aldune.slnx -c Debug` → Expected: error (`MarkdownLink` no existe).

- [ ] **Step 3: Implement**

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace Aldune.Core;

/// <summary>
/// Traducción entre el texto de un archivo vinculado y el de su nota (spec, decisión 3). Las tareas y
/// viñetas de Markdown pasan a los glifos de Aldune (☐ ☒ →) para que el editor funcione igual que en
/// cualquier nota; lo demás queda literal. Al volver al archivo, toda línea que el usuario no ha tocado
/// se escribe con sus bytes originales: el archivo es suyo, y Aldune no lo "normaliza".
/// </summary>
public static partial class MarkdownLink
{
    // Por encima, el tramo central se trata como reescrito sin emparejar: la tabla de la subsecuencia
    // común crece con el producto de líneas y un archivo de 2 MB no debe comerse la memoria.
    private const int MaxDiffCells = 4_000_000;

    private readonly record struct Line(string Content, string Terminator);

    // Marcador seguido de espacio (así "**negrita**" y "---" no cuentan) y, opcional, la casilla.
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>[-*+]) (?:\[(?<mark>[ xX])\](?: |$))?")]
    private static partial Regex ListItem();

    public static string ToNoteText(string fileText)
    {
        var lines = SplitLines(fileText);
        var translated = TranslateAll(lines);
        var builder = new StringBuilder(fileText.Length);
        for (int i = 0; i < lines.Count; i++) builder.Append(translated[i]).Append(lines[i].Terminator);
        return builder.ToString();
    }

    public static string ToFileText(string noteText, string originalFileText)
    {
        var original = SplitLines(originalFileText);
        var originalAsNote = TranslateAll(original);
        var edited = SplitLines(noteText);
        var editedContents = edited.Select(line => line.Content).ToList();
        var literal = LiteralLines(editedContents);
        var match = MatchLines(originalAsNote, editedContents);

        // Una línea movida no se empareja en la subsecuencia común, pero sigue siendo la misma: se reutilizan
        // sus bytes originales si quedan sin usar.
        var used = new HashSet<int>(match.Where(index => index >= 0));
        var spare = new Dictionary<string, Queue<int>>();
        for (int i = 0; i < original.Count; i++)
        {
            if (used.Contains(i)) continue;
            if (!spare.TryGetValue(originalAsNote[i], out var queue)) spare[originalAsNote[i]] = queue = new Queue<int>();
            queue.Enqueue(i);
        }

        string newLine = DominantNewLine(original);
        char marker = DominantBulletMarker(original);
        var builder = new StringBuilder(noteText.Length + 64);
        for (int j = 0; j < edited.Count; j++)
        {
            int i = match[j];
            if (i < 0 && spare.TryGetValue(editedContents[j], out var queue) && queue.Count > 0) i = queue.Dequeue();

            builder.Append(i >= 0 ? original[i].Content
                : literal[j] ? editedContents[j]
                : ToFileLine(editedContents[j], marker));

            if (edited[j].Terminator.Length == 0) continue;
            builder.Append(i >= 0 && original[i].Terminator.Length > 0 ? original[i].Terminator : newLine);
        }
        return builder.ToString();
    }

    private static List<Line> SplitLines(string text)
    {
        var lines = new List<Line>();
        int start = 0;
        while (true)
        {
            int newLine = text.IndexOf('\n', start);
            if (newLine < 0)
            {
                lines.Add(new Line(text[start..], ""));
                return lines;
            }
            bool crlf = newLine > start && text[newLine - 1] == '\r';
            lines.Add(new Line(text[start..(crlf ? newLine - 1 : newLine)], crlf ? "\r\n" : "\n"));
            start = newLine + 1;
        }
    }

    private static List<string> TranslateAll(List<Line> lines)
    {
        var contents = lines.Select(line => line.Content).ToList();
        var literal = LiteralLines(contents);
        return contents.Select((content, i) => literal[i] ? content : ToNoteLine(content)).ToList();
    }

    // Las vallas de código (``` o ~~~) y lo que hay entre ellas no se traducen: un "- [ ]" dentro de un
    // bloque de código es texto, no una tarea. Se cierra con la misma valla con la que se abrió.
    private static bool[] LiteralLines(IReadOnlyList<string> contents)
    {
        var literal = new bool[contents.Count];
        string? fence = null;
        for (int i = 0; i < contents.Count; i++)
        {
            var trimmed = contents[i].TrimStart(' ', '\t');
            string? opener = trimmed.StartsWith("```", StringComparison.Ordinal) ? "```"
                : trimmed.StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : null;
            if (fence is null)
            {
                if (opener is not null) { fence = opener; literal[i] = true; }
            }
            else
            {
                literal[i] = true;
                if (opener == fence) fence = null;
            }
        }
        return literal;
    }

    private static string ToNoteLine(string line)
    {
        var match = ListItem().Match(line);
        if (!match.Success) return line;
        var indent = match.Groups["indent"].Value;
        var rest = line[match.Length..];
        if (!match.Groups["mark"].Success) return indent + BulletLines.Prefix + rest;
        return indent + (match.Groups["mark"].Value == " " ? TaskLines.Unchecked : TaskLines.Checked) + " " + rest;
    }

    private static string ToFileLine(string line, char bulletMarker)
    {
        int task = TaskLines.GlyphIndex(line);
        if (task >= 0)
            return line[..task] + (TaskLines.IsChecked(line) ? "- [x] " : "- [ ] ") + line[(task + TaskLines.PrefixLength(line, task))..];
        int bullet = BulletLines.GlyphIndex(line);
        if (bullet >= 0)
            return line[..bullet] + bulletMarker + " " + line[(bullet + BulletLines.PrefixLength(line, bullet))..];
        return line;
    }

    private static string DominantNewLine(List<Line> lines)
    {
        int crlf = lines.Count(line => line.Terminator == "\r\n");
        int lf = lines.Count(line => line.Terminator == "\n");
        // Sin saltos (archivo nuevo o de una línea): CRLF, el de Windows y el del TextBox.
        return lf > crlf ? "\n" : "\r\n";
    }

    private static char DominantBulletMarker(List<Line> lines)
    {
        var literal = LiteralLines(lines.Select(line => line.Content).ToList());
        var counts = new Dictionary<char, int> { ['-'] = 0, ['*'] = 0, ['+'] = 0 };
        for (int i = 0; i < lines.Count; i++)
        {
            if (literal[i]) continue;
            var match = ListItem().Match(lines[i].Content);
            if (match.Success && !match.Groups["mark"].Success) counts[match.Groups["marker"].Value[0]]++;
        }
        // Empate o ninguno: el guion, que es lo que escribe Aldune al exportar.
        return counts.OrderByDescending(pair => pair.Value).ThenBy(pair => "-*+".IndexOf(pair.Key)).First().Key;
    }

    /// <summary>Para cada línea de <paramref name="edited"/>, la de <paramref name="original"/> con la que se
    /// empareja (−1 si es nueva o editada): prefijo y sufijo comunes y, en medio, la subsecuencia común más larga.</summary>
    private static int[] MatchLines(IReadOnlyList<string> original, IReadOnlyList<string> edited)
    {
        var result = Enumerable.Repeat(-1, edited.Count).ToArray();
        int prefix = 0;
        while (prefix < original.Count && prefix < edited.Count && original[prefix] == edited[prefix])
        {
            result[prefix] = prefix;
            prefix++;
        }
        int suffix = 0;
        while (suffix < original.Count - prefix && suffix < edited.Count - prefix &&
               original[original.Count - 1 - suffix] == edited[edited.Count - 1 - suffix])
        {
            result[edited.Count - 1 - suffix] = original.Count - 1 - suffix;
            suffix++;
        }

        int n = original.Count - prefix - suffix, m = edited.Count - prefix - suffix;
        if (n == 0 || m == 0 || (long)n * m > MaxDiffCells) return result;

        var table = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                table[i, j] = original[prefix + i] == edited[prefix + j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);

        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (original[prefix + i] == edited[prefix + j])
            {
                result[prefix + j] = prefix + i;
                i++;
                j++;
            }
            else if (table[i + 1, j] >= table[i, j + 1]) i++;
            else j++;
        }
        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build --filter MarkdownLinkTests`
Expected: PASS. Si `UneditedRoundTrip` falla en una entrada del corpus, el fallo está en la traducción
(no en el emparejamiento): `ToNoteText` tiene que conservar el número de líneas y sus saltos.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/MarkdownLink.cs tests/Aldune.Core.Tests/MarkdownLinkTests.cs
git commit -m "Notas vinculadas: MarkdownLink traduce archivo y nota conservando byte a byte lo no editado"
```

---

### Task 3: `LinkedFileDecision` — qué hacer con un archivo vinculado

**Files:**
- Create: `src/Aldune.Core/LinkedFileDecision.cs`
- Test: `tests/Aldune.Core.Tests/LinkedFileDecisionTests.cs`

**Interfaces:**
- Produces: `enum LinkedFileAction { None, Write, Reload, Conflict, Unavailable }`;
  `static LinkedFileAction LinkedFileDecision.Decide(string? knownFileHash, string? currentFileHash, bool hasPendingChanges)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Xunit;

namespace Aldune.Core.Tests;

public class LinkedFileDecisionTests
{
    [Theory]
    [InlineData("A", "A", true, LinkedFileAction.Write)]
    [InlineData("A", "A", false, LinkedFileAction.None)]
    [InlineData("A", "B", false, LinkedFileAction.Reload)]
    [InlineData("A", "B", true, LinkedFileAction.Conflict)]
    [InlineData("A", null, false, LinkedFileAction.Unavailable)]
    [InlineData("A", null, true, LinkedFileAction.Unavailable)]
    // Sin huella conocida (recién vuelto a vincular con "Buscar…"): el disco cuenta como distinto.
    [InlineData(null, "B", false, LinkedFileAction.Reload)]
    [InlineData(null, "B", true, LinkedFileAction.Conflict)]
    public void Decide(string? known, string? current, bool pending, LinkedFileAction expected) =>
        Assert.Equal(expected, LinkedFileDecision.Decide(known, current, pending));
}
```

- [ ] **Step 2: Run to verify it fails** — `dotnet build Aldune.slnx -c Debug` → error (tipo inexistente).

- [ ] **Step 3: Implement**

```csharp
namespace Aldune.Core;

public enum LinkedFileAction { None, Write, Reload, Conflict, Unavailable }

/// <summary>
/// La tabla de la decisión 4 de la spec. Lo que manda es la huella: si el disco tiene la que Aldune
/// conoce, nadie lo ha tocado y se puede escribir; si no, el archivo es del otro programa y lo de Aldune,
/// si había algo pendiente, va a una nota aparte. Nunca se escribe sobre una huella desconocida.
/// </summary>
public static class LinkedFileDecision
{
    public static LinkedFileAction Decide(string? knownFileHash, string? currentFileHash, bool hasPendingChanges)
    {
        if (currentFileHash is null) return LinkedFileAction.Unavailable;
        if (knownFileHash is not null && currentFileHash == knownFileHash)
            return hasPendingChanges ? LinkedFileAction.Write : LinkedFileAction.None;
        return hasPendingChanges ? LinkedFileAction.Conflict : LinkedFileAction.Reload;
    }
}
```

- [ ] **Step 4: Run** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build --filter LinkedFileDecisionTests` → PASS (8).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/LinkedFileDecision.cs tests/Aldune.Core.Tests/LinkedFileDecisionTests.cs
git commit -m "Notas vinculadas: LinkedFileDecision (escribir, recargar, conflicto o no disponible)"
```

---

### Task 4: `NoteFileLink` en el repositorio, borrados y notas vinculadas sin proteger

**Files:**
- Create: `src/Aldune.Core/NoteFileLink.cs`
- Modify: `src/Aldune.Core/NotesDatabase.cs` (texto del `CREATE TABLE` en `Initialize`, tras `SyncConflict`)
- Modify: `src/Aldune.Core/NotesRepository.cs` (`Delete`, `PurgeExpiredTrash`, `ApplySyncTombstone`, `Protect` y métodos nuevos)
- Test: `tests/Aldune.Core.Tests/NotesRepositoryFileLinkTests.cs`

**Interfaces:**
- Produces:
  - `sealed record NoteFileLink(Guid NoteId, string Path, bool SyncEnabled, string? KnownHash, DateTimeOffset? KnownWriteTime, string? KnownTextHash)`
  - `NotesRepository`: `void SaveFileLink(NoteFileLink link)`, `NoteFileLink? GetFileLink(Guid noteId)`,
    `IReadOnlyList<NoteFileLink> GetFileLinks()`, `void DeleteFileLink(Guid noteId)`,
    `Guid? FindNoteByLinkedPath(string path)`, `IReadOnlySet<Guid> GetUnsyncedLinkedNoteIds()`
  - `NotesRepository.Protect` lanza `InvalidOperationException` si la nota está vinculada.

La ruta va cifrada con el mismo `ContentCipher` que el texto: dice de qué trata la nota (`Z:\apuntes\tema3.md`).

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryFileLinkTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-link-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryFileLinkTests()
    {
        _sut = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private NoteFileLink LinkFor(Guid id, string path = @"Z:\apuntes\tema3.md", bool sync = false) =>
        new(id, path, sync, "HASH", new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero), "TEXT");

    [Fact]
    public void SaveAndGet_RoundTripsEveryField()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        var link = LinkFor(note.Id);

        _sut.SaveFileLink(link);

        Assert.Equal(link, _sut.GetFileLink(note.Id));
        Assert.Equal(new[] { link }, _sut.GetFileLinks());
    }

    [Fact]
    public void SaveFileLink_ReplacesTheExistingRow()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id));
        _sut.SaveFileLink(LinkFor(note.Id) with { KnownHash = "OTRO", SyncEnabled = true });

        Assert.Equal("OTRO", _sut.GetFileLink(note.Id)!.KnownHash);
        Assert.Single(_sut.GetFileLinks());
    }

    [Fact]
    public void ThePathIsNotStoredInClear()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id, @"Z:\secreto\diario.md"));

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EncryptedPath FROM NoteFileLink;";
        var raw = System.Text.Encoding.UTF8.GetString((byte[])command.ExecuteScalar()!);
        Assert.DoesNotContain("diario", raw);
    }

    [Fact]
    public void FindNoteByLinkedPath_IgnoresCase()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id, @"Z:\Apuntes\Tema3.md"));

        Assert.Equal(note.Id, _sut.FindNoteByLinkedPath(@"z:\apuntes\tema3.MD"));
        Assert.Null(_sut.FindNoteByLinkedPath(@"Z:\apuntes\otro.md"));
    }

    [Fact]
    public void GetUnsyncedLinkedNoteIds_ListsOnlyThoseWithoutSync()
    {
        var local = _sut.Create("a", "#EBD38B", "primary");
        var synced = _sut.Create("b", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(local.Id, @"C:\a.md"));
        _sut.SaveFileLink(LinkFor(synced.Id, @"C:\b.md", sync: true));

        Assert.Equal(new[] { local.Id }, _sut.GetUnsyncedLinkedNoteIds());
    }

    [Fact]
    public void DeletingTheNote_DeletesTheLinkButNeverTheFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"aldune-link-{Guid.NewGuid():N}.md");
        File.WriteAllText(file, "contenido");
        try
        {
            var note = _sut.Create("contenido", "#EBD38B", "primary");
            _sut.SaveFileLink(LinkFor(note.Id, file));

            Assert.True(_sut.Delete(note.Id));

            Assert.Null(_sut.GetFileLink(note.Id));
            Assert.True(File.Exists(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void PurgingTheTrashAndRemoteTombstones_DeleteTheLink()
    {
        var purged = _sut.Create("a", "#EBD38B", "primary");
        var tombstoned = _sut.Create("b", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(purged.Id, @"C:\a.md"));
        _sut.SaveFileLink(LinkFor(tombstoned.Id, @"C:\b.md"));
        _sut.SetState(purged.Id, NoteState.Trashed);

        _sut.PurgeExpiredTrash(TimeSpan.Zero);
        _sut.ApplySyncTombstone(new SyncTombstone(tombstoned.Id, DateTimeOffset.UtcNow, "otro"));

        Assert.Empty(_sut.GetFileLinks());
    }

    [Fact]
    public void ALinkedNote_CannotBeProtected()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id));

        Assert.Throws<InvalidOperationException>(() => _sut.Protect(note.Id, "contraseña-larga-1"));
    }
}
```

Antes de escribir el test de `PurgeExpiredTrash` y `ApplySyncTombstone`, comprobar en `SyncModels.cs` el orden de
los parámetros de `SyncTombstone` (`NoteId, DeletedAt, DeviceId`) y ajustar la llamada si difiere.

- [ ] **Step 2: Run to verify it fails** — `dotnet build Aldune.slnx -c Debug` → error (tipos y métodos inexistentes).

- [ ] **Step 3: Implement**

`src/Aldune.Core/NoteFileLink.cs`:

```csharp
namespace Aldune.Core;

/// <summary>
/// El vínculo de una nota con un archivo del disco (spec, decisión 2). Es de este equipo: nunca viaja por
/// la sync, porque la ruta no significa nada en otro equipo. Las huellas dicen cómo estaban el archivo y la
/// nota la última vez que Aldune los puso de acuerdo; con ellas se sabe quién ha cambiado qué.
/// </summary>
public sealed record NoteFileLink(
    Guid NoteId,
    string Path,
    bool SyncEnabled,
    string? KnownHash,
    DateTimeOffset? KnownWriteTime,
    string? KnownTextHash);
```

En `NotesDatabase.Initialize`, al final del texto del `CREATE TABLE` (tras `SyncConflict`):

```sql
            -- Notas vinculadas a un archivo del disco (spec 2026-10-04). Local: nunca viaja por la sync. La
            -- ruta va cifrada como el texto de las notas.
            CREATE TABLE IF NOT EXISTS NoteFileLink (
                NoteId TEXT PRIMARY KEY NOT NULL,
                EncryptedPath BLOB NOT NULL,
                PathNonce BLOB NOT NULL,
                PathTag BLOB NOT NULL,
                SyncEnabled INTEGER NOT NULL DEFAULT 0,
                KnownHash TEXT,
                KnownWriteTime TEXT,
                KnownTextHash TEXT
            );
```

En `NotesRepository`, añadir (junto a los métodos de sync):

```csharp
    public void SaveFileLink(NoteFileLink link)
    {
        var path = _cipher.Encrypt(link.Path);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO NoteFileLink
                (NoteId, EncryptedPath, PathNonce, PathTag, SyncEnabled, KnownHash, KnownWriteTime, KnownTextHash)
            VALUES ($id, $path, $nonce, $tag, $sync, $hash, $writeTime, $textHash);
            """;
        command.Parameters.AddWithValue("$id", link.NoteId.ToString());
        command.Parameters.AddWithValue("$path", path.CipherText);
        command.Parameters.AddWithValue("$nonce", path.Nonce);
        command.Parameters.AddWithValue("$tag", path.Tag);
        command.Parameters.AddWithValue("$sync", link.SyncEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$hash", (object?)link.KnownHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$writeTime", (object?)link.KnownWriteTime?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$textHash", (object?)link.KnownTextHash ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public NoteFileLink? GetFileLink(Guid noteId) => ReadFileLinks(noteId).FirstOrDefault();

    public IReadOnlyList<NoteFileLink> GetFileLinks() => ReadFileLinks(null);

    public void DeleteFileLink(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM NoteFileLink WHERE NoteId = $id;";
        command.Parameters.AddWithValue("$id", noteId.ToString());
        command.ExecuteNonQuery();
    }

    /// <summary>La nota vinculada a <paramref name="path"/>, comparando la ruta completa sin distinguir
    /// mayúsculas (Windows no las distingue). Recorre los vínculos porque las rutas van cifradas.</summary>
    public Guid? FindNoteByLinkedPath(string path)
    {
        var wanted = System.IO.Path.GetFullPath(path);
        return GetFileLinks().FirstOrDefault(link =>
            string.Equals(System.IO.Path.GetFullPath(link.Path), wanted, StringComparison.OrdinalIgnoreCase))?.NoteId;
    }

    /// <summary>Notas vinculadas que no entran en la sync. Sin descifrar nada: la sync lo pide en cada pasada.</summary>
    public IReadOnlySet<Guid> GetUnsyncedLinkedNoteIds()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NoteId FROM NoteFileLink WHERE SyncEnabled = 0;";
        using var reader = command.ExecuteReader();
        var ids = new HashSet<Guid>();
        while (reader.Read()) ids.Add(Guid.Parse(reader.GetString(0)));
        return ids;
    }

    private List<NoteFileLink> ReadFileLinks(Guid? noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NoteId, EncryptedPath, PathNonce, PathTag, SyncEnabled, KnownHash, KnownWriteTime, KnownTextHash
            FROM NoteFileLink
            """ + (noteId is null ? ";" : " WHERE NoteId = $id;");
        if (noteId is not null) command.Parameters.AddWithValue("$id", noteId.Value.ToString());
        using var reader = command.ExecuteReader();
        var links = new List<NoteFileLink>();
        while (reader.Read())
        {
            var path = _cipher.Decrypt(new EncryptedContent(
                (byte[])reader["EncryptedPath"], (byte[])reader["PathNonce"], (byte[])reader["PathTag"]));
            links.Add(new NoteFileLink(
                Guid.Parse(reader.GetString(0)),
                path,
                reader.GetInt64(4) != 0,
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : ParseDate(reader["KnownWriteTime"]),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }
        return links;
    }
```

(`ParseDate` ya existe en `NotesRepository`; si su firma no acepta `object`, usar
`DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`.)

En `Delete`, añadir dentro del texto SQL, antes de `DELETE FROM Note WHERE Id = $id;`:

```sql
            DELETE FROM NoteFileLink WHERE NoteId = $id;
```

En `PurgeExpiredTrash`, antes de `DELETE FROM Note WHERE ...`:

```sql
            DELETE FROM NoteFileLink WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
```

En `ApplySyncTombstone`, junto a los demás `DELETE`:

```sql
            DELETE FROM NoteFileLink WHERE NoteId = $id;
```

En `Protect`, tras la comprobación de `IsProtected`:

```csharp
        // Una nota vinculada es su archivo, y el archivo está en claro: protegerla solo dentro de Aldune
        // sería una falsa seguridad (spec, decisión 7). La interfaz ya no lo ofrece; esto es la red.
        if (GetFileLink(id) is not null) throw new InvalidOperationException("A linked note cannot be protected.");
```

- [ ] **Step 4: Run** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build` → PASS, todos.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/NoteFileLink.cs src/Aldune.Core/NotesDatabase.cs src/Aldune.Core/NotesRepository.cs tests/Aldune.Core.Tests/NotesRepositoryFileLinkTests.cs
git commit -m "Notas vinculadas: tabla NoteFileLink local con la ruta cifrada; borrar la nota nunca borra el archivo"
```

---

### Task 5: La sync deja fuera las notas vinculadas sin sync, también lo que llega de fuera

**Files:**
- Modify: `src/Aldune.Core/SyncScopeFilter.cs`
- Modify: `src/Aldune.Core/NoteSyncSignal.cs`
- Modify: `src/Aldune.Core/SyncService.cs` (`Synchronize`, hacia la línea 300; `CompletePendingKeyRotation`, hacia la 440)
- Modify: `src/Aldune/Windowing/AppCoordinator.cs` (`SyncSignalFor`)
- Test: `tests/Aldune.Core.Tests/SyncConflictTests.cs` (tests nuevos al final, con sus `Device` y `ShareKeyAndSynchronizeInitialNote`)

**Interfaces:**
- Consumes: `NotesRepository.SaveFileLink`, `GetUnsyncedLinkedNoteIds` (tarea 4).
- Produces: `SyncScopeFilter.Includes(Note note, AppSettings settings, IReadOnlySet<Guid>? localOnly = null)`;
  `NoteSyncSignal.For(Note, AppSettings, SyncBaseVersion?, bool hasConflict, IReadOnlySet<Guid>? localOnly = null)`.

- [ ] **Step 1: Write the failing tests** (al final de `SyncConflictTests`, antes de `CreateDevice`)

```csharp
    [Fact]
    public void LinkedNoteWithoutSync_IsNeitherOverwrittenNorPublished()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        _deviceB.Repository.UpdateText(note.Id, "edit from B");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        // A la vincula a un archivo sin sync y la edita: lo de B no debe pisarla, ni lo de A salir.
        _deviceA.Repository.SaveFileLink(new NoteFileLink(note.Id, @"C:\apuntes\tema.md", false, null, null, null));
        _deviceA.Repository.UpdateText(note.Id, "local de A");
        var result = _deviceA.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("local de A", _deviceA.Repository.GetById(note.Id)!.Text);
        Assert.Empty(_deviceA.Sync.GetConflicts());
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.Equal("edit from B", _deviceB.Repository.GetById(note.Id)!.Text);
    }

    [Fact]
    public void LinkedNoteWithoutSync_SurvivesARemoteDeletion()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        _deviceA.Repository.SaveFileLink(new NoteFileLink(note.Id, @"C:\apuntes\tema.md", false, null, null, null));
        Assert.True(_deviceB.Repository.Delete(note.Id));
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.NotNull(_deviceA.Repository.GetById(note.Id));
        Assert.NotNull(_deviceA.Repository.GetFileLink(note.Id));
    }

    [Fact]
    public void LinkedNoteWithSync_TravelsLikeANormalNote()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        _deviceA.Repository.SaveFileLink(new NoteFileLink(note.Id, @"C:\apuntes\tema.md", true, null, null, null));
        ShareKeyAndSynchronizeInitialNote(note.Id);

        Assert.Null(_deviceB.Repository.GetFileLink(note.Id));
        Assert.Equal("original", _deviceB.Repository.GetById(note.Id)!.Text);
    }

    [Fact]
    public void Signal_SaysExcludedForALinkedNoteWithoutSync()
    {
        var note = _deviceA.Repository.Create("x", "#EBD38B", "primary");
        _deviceA.Repository.SaveFileLink(new NoteFileLink(note.Id, @"C:\a.md", false, null, null, null));

        var state = NoteSyncSignal.For(note, _deviceA.Settings, null, false, _deviceA.Repository.GetUnsyncedLinkedNoteIds());

        Assert.Equal(SyncSignalState.Excluded, state);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build --filter "LinkedNote|Signal_SaysExcluded"`
Expected: no compila (`NoteSyncSignal.For` con 5 argumentos); tras añadir el parámetro sin usarlo, los dos
primeros fallan (el texto de A pasa a "edit from B"; la nota de A desaparece).

- [ ] **Step 3: Implement**

`SyncScopeFilter.Includes`:

```csharp
    /// <param name="localOnly">Notas vinculadas a un archivo sin "Sincronizar esta nota": fuera siempre,
    /// sea cual sea el alcance (spec de notas vinculadas, decisión 2).</param>
    public static bool Includes(Note note, AppSettings settings, IReadOnlySet<Guid>? localOnly = null)
    {
        if (localOnly is not null && localOnly.Contains(note.Id)) return false;
        return settings.SyncScope switch
        {
            SyncScopeKind.SelectedNotes => settings.SyncNoteIds.Contains(note.Id),
            SyncScopeKind.Tag => string.IsNullOrWhiteSpace(settings.SyncTag) ||
                note.Tags.Any(tag => string.Equals(tag, settings.SyncTag, StringComparison.OrdinalIgnoreCase)),
            _ => true,
        };
    }
```

`NoteSyncSignal.For`: añadir `IReadOnlySet<Guid>? localOnly = null` al final y pasarlo a `SyncScopeFilter.Includes`.

En `SyncService.Synchronize`, justo antes de `HandleUnsignedTombstones(...)`:

```csharp
                // Una nota vinculada sin sync es solo de este equipo: lo que llegue de fuera con su id (una
                // copia de cuando sí se sincronizaba, o su borrado) no puede pisarla ni borrarla. Sin esto,
                // al no estar en el lado local se aplicaría lo remoto tal cual.
                var localOnly = _repository.GetUnsyncedLinkedNoteIds();
                if (localOnly.Count > 0)
                    remote = remote.Where(pair => !localOnly.Contains(pair.Key))
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
```

y en el filtro de `localNotes` de ese mismo método: `.Where(note => SyncScopeFilter.Includes(note, _settings, localOnly))`.

En `CompletePendingKeyRotation`, el filtro de `localNotes` igual, con
`var localOnly = _repository.GetUnsyncedLinkedNoteIds();` declarado antes. (Ahí no hace falta filtrar lo
remoto: la rotación borra del almacén lo que no está en el lado local, que es lo correcto para una nota que
ya no se sincroniza.)

En `AppCoordinator.SyncSignalFor`:

```csharp
        return NoteSyncSignal.For(note, _settings, _repository.GetSyncBase(noteId), hasConflict,
            _repository.GetUnsyncedLinkedNoteIds());
```

- [ ] **Step 4: Run** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build` → PASS, todos.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/SyncScopeFilter.cs src/Aldune.Core/NoteSyncSignal.cs src/Aldune.Core/SyncService.cs src/Aldune/Windowing/AppCoordinator.cs tests/Aldune.Core.Tests/SyncConflictTests.cs
git commit -m "Notas vinculadas: la sync deja fuera las que no se sincronizan, tambien lo que llega de otro equipo"
```

---

### Task 6: `LinkedFileService` — abrir, poner de acuerdo archivo y nota, escribir de forma atómica

**Files:**
- Create: `src/Aldune.Core/LinkedFileService.cs`
- Test: `tests/Aldune.Core.Tests/LinkedFileServiceTests.cs`

**Interfaces:**
- Consumes: tareas 1 a 4.
- Produces:
  - `enum LinkOutcome { Linked, AlreadyLinked, UnsupportedExtension, TooLarge, UnsupportedEncoding, Unreadable }`
  - `sealed record LinkResult(LinkOutcome Outcome, Guid? NoteId = null)`
  - `enum ReconcileOutcome { Unchanged, Written, Reloaded, Conflict, Unavailable, NotLinked }`
  - `sealed record ReconcileResult(ReconcileOutcome Outcome, Guid? ConflictNoteId = null)`
  - `sealed class LinkedFileService(NotesRepository repository, Func<string> nextColor, Func<string> conflictPrefix, Action<TimeSpan>? sleep = null)`:
    `LinkResult Open(string path)`, `LinkResult Relink(Guid noteId, string newPath)`, `ReconcileResult Reconcile(Guid noteId)`,
    `void Unlink(Guid noteId)`, `static bool LooksChanged(NoteFileLink link)`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class LinkedFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-linked-{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly NotesRepository _repository;
    private readonly LinkedFileService _sut;

    public LinkedFileServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "notes.db");
        _repository = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
        _sut = new LinkedFileService(_repository, () => "#EBD38B", () => "⚠ Conflicto: ", sleep: _ => { });
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        Directory.Delete(_dir, recursive: true);
    }

    private string WriteFile(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private Guid OpenLinked(string path)
    {
        var result = _sut.Open(path);
        Assert.Equal(LinkOutcome.Linked, result.Outcome);
        return result.NoteId!.Value;
    }

    [Fact]
    public void Open_CreatesATranslatedNoteWithoutSync()
    {
        var path = WriteFile("tema.md", "# Tema\n- [ ] repasar\n");

        var id = OpenLinked(path);

        Assert.Equal("# Tema\n☐ repasar\n", _repository.GetById(id)!.Text);
        var link = _repository.GetFileLink(id)!;
        Assert.Equal(Path.GetFullPath(path), link.Path);
        Assert.False(link.SyncEnabled);
    }

    [Fact]
    public void Open_TheSameFileTwice_ReturnsTheExistingNote()
    {
        var path = WriteFile("tema.md", "x");
        var id = OpenLinked(path);

        var again = _sut.Open(path.ToUpperInvariant());

        Assert.Equal(new LinkResult(LinkOutcome.AlreadyLinked, id), again);
        Assert.Single(_repository.GetByState(NoteState.Active));
    }

    [Fact]
    public void Open_RejectsWhatItCannotSafelyEdit()
    {
        var png = WriteFile("foto.png", "x");
        var ansi = Path.Combine(_dir, "ansi.txt");
        File.WriteAllBytes(ansi, [0x61, 0xF1, 0x6F]);
        var big = Path.Combine(_dir, "grande.md");
        File.WriteAllBytes(big, new byte[LinkedFileFormat.MaxBytes + 1]);

        Assert.Equal(LinkOutcome.UnsupportedExtension, _sut.Open(png).Outcome);
        Assert.Equal(LinkOutcome.UnsupportedEncoding, _sut.Open(ansi).Outcome);
        Assert.Equal(LinkOutcome.TooLarge, _sut.Open(big).Outcome);
        Assert.Equal(LinkOutcome.Unreadable, _sut.Open(Path.Combine(_dir, "no-existe.md")).Outcome);
        Assert.Empty(_repository.GetByState(NoteState.Active));
    }

    [Fact]
    public void AnEditInAldune_IsWrittenToTheFile()
    {
        var path = WriteFile("tema.md", "* [ ] uno\n* [ ] dos\n");
        var id = OpenLinked(path);

        _repository.UpdateText(id, "☒ uno\n☐ dos\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Written, result.Outcome);
        Assert.Equal("- [x] uno\n* [ ] dos\n", File.ReadAllText(path));
        Assert.Equal(ReconcileOutcome.Unchanged, _sut.Reconcile(id).Outcome);
        Assert.Empty(Directory.GetFiles(_dir, ".~aldune-*"));
    }

    [Fact]
    public void AnOutsideChange_IsReloadedIntoTheNote()
    {
        var path = WriteFile("tema.md", "- [ ] uno\n");
        var id = OpenLinked(path);

        File.WriteAllText(path, "- [ ] uno\n- [ ] dos\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Reloaded, result.Outcome);
        Assert.Equal("☐ uno\n☐ dos\n", _repository.GetById(id)!.Text);
    }

    [Fact]
    public void BothSidesChanged_KeepsTheFileAndMovesAldunesTextToANewNote()
    {
        var path = WriteFile("tema.md", "original\n");
        var id = OpenLinked(path);

        _repository.UpdateText(id, "lo de Aldune\n");
        File.WriteAllText(path, "lo del otro programa\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Conflict, result.Outcome);
        Assert.Equal("lo del otro programa\n", File.ReadAllText(path));
        Assert.Equal("lo del otro programa\n", _repository.GetById(id)!.Text);
        var copy = _repository.GetById(result.ConflictNoteId!.Value)!;
        Assert.Equal("⚠ Conflicto: lo de Aldune\n", copy.Text);
        Assert.Null(_repository.GetFileLink(copy.Id));
    }

    [Fact]
    public void AMissingFile_IsUnavailable_AndPendingEditsWaitForIt()
    {
        var path = WriteFile("tema.md", "original\n");
        var id = OpenLinked(path);
        var moved = path + ".fuera";
        File.Move(path, moved);

        _repository.UpdateText(id, "editado mientras no estaba\n");
        Assert.Equal(ReconcileOutcome.Unavailable, _sut.Reconcile(id).Outcome);
        Assert.False(File.Exists(path));

        File.Move(moved, path);
        Assert.Equal(ReconcileOutcome.Written, _sut.Reconcile(id).Outcome);
        Assert.Equal("editado mientras no estaba\n", File.ReadAllText(path));
    }

    [Fact]
    public void UnchangedBytes_AreNotRewritten()
    {
        // NoteText.Join pone CRLF entre título y cuerpo: el texto de la nota cambia, los bytes del archivo no.
        var path = WriteFile("tema.md", "# Tema\nlínea\n");
        var id = OpenLinked(path);
        var before = File.GetLastWriteTimeUtc(path);
        Thread.Sleep(20);

        _repository.UpdateText(id, "# Tema\r\nlínea\n");
        var result = _sut.Reconcile(id);

        Assert.Equal(ReconcileOutcome.Unchanged, result.Outcome);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.Equal(ReconcileOutcome.Unchanged, _sut.Reconcile(id).Outcome);
    }

    [Fact]
    public void Relink_ToAnotherFile_TreatsItsContentAsNew()
    {
        var first = WriteFile("a.md", "a\n");
        var id = OpenLinked(first);
        var second = WriteFile("b.md", "b\n");

        Assert.Equal(LinkOutcome.Linked, _sut.Relink(id, second).Outcome);
        Assert.Equal(ReconcileOutcome.Reloaded, _sut.Reconcile(id).Outcome);
        Assert.Equal("b\n", _repository.GetById(id)!.Text);
    }

    [Fact]
    public void OrphanTemporaryFiles_AreCleanedButNothingElse()
    {
        var path = WriteFile("tema.md", "x\n");
        var id = OpenLinked(path);
        var orphan = Path.Combine(_dir, $".~aldune-{Guid.NewGuid():N}.tmp");
        var lookalike = Path.Combine(_dir, ".~aldune-notas.tmp");
        File.WriteAllText(orphan, "");
        File.WriteAllText(lookalike, "");
        File.SetLastWriteTimeUtc(orphan, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(lookalike, DateTime.UtcNow.AddMinutes(-5));

        _repository.UpdateText(id, "y\n");
        _sut.Reconcile(id);

        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(lookalike));
    }

    [Fact]
    public void Unlink_KeepsTheNoteAndTheFile()
    {
        var path = WriteFile("tema.md", "x\n");
        var id = OpenLinked(path);

        _sut.Unlink(id);

        Assert.Null(_repository.GetFileLink(id));
        Assert.NotNull(_repository.GetById(id));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LooksChanged_DetectsAnOutsideWrite()
    {
        var path = WriteFile("tema.md", "x\n");
        var id = OpenLinked(path);
        Assert.False(LinkedFileService.LooksChanged(_repository.GetFileLink(id)!));

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.True(LinkedFileService.LooksChanged(_repository.GetFileLink(id)!));
    }
}
```

- [ ] **Step 2: Run to verify they fail** — `dotnet build Aldune.slnx -c Debug` → error (`LinkedFileService` no existe).

- [ ] **Step 3: Implement**

```csharp
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

    public ReconcileResult Reconcile(Guid noteId)
    {
        var link = _repository.GetFileLink(noteId);
        var note = _repository.GetById(noteId);
        if (link is null || note is null) return new ReconcileResult(ReconcileOutcome.NotLinked);

        var bytes = TryReadStable(link.Path);
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
            _repository.SaveFileLink(link with { KnownTextHash = textHash });
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

        _repository.SaveFileLink(link with
        {
            KnownHash = LinkedFileFormat.Hash(bytes), KnownWriteTime = WriteTime(link.Path), KnownTextHash = textHash,
        });
        return new ReconcileResult(ReconcileOutcome.Written);
    }

    private void Reload(Note note, NoteFileLink link, byte[] bytes, LinkedFileContent content)
    {
        var text = MarkdownLink.ToNoteText(content.Text);
        if (text != note.Text) _repository.UpdateText(note.Id, text);
        _repository.SaveFileLink(link with
        {
            KnownHash = LinkedFileFormat.Hash(bytes), KnownWriteTime = WriteTime(link.Path),
            KnownTextHash = LinkedFileFormat.HashText(text),
        });
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
```

Nota: `FileChangedException` hereda de `IOException`, así que el `catch` específico tiene que ir **antes** del
genérico, como está arriba.

- [ ] **Step 4: Run** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build --filter LinkedFileServiceTests` → PASS (12).
  Luego la suite entera: `dotnet test Aldune.slnx --no-build` → PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/LinkedFileService.cs tests/Aldune.Core.Tests/LinkedFileServiceTests.cs
git commit -m "Notas vinculadas: LinkedFileService abre, recarga, escribe de forma atomica y separa los conflictos"
```

---

### Task 7: Título sin `#` y textos de la tanda 1

**Files:**
- Modify: `src/Aldune.Core/NoteTitleHelper.cs` (`GetTitle`)
- Modify: `src/Aldune/Resources/Strings.cs`
- Test: `tests/Aldune.Core.Tests/NoteTitleHelperTests.cs`

**Interfaces:**
- Produces: `NoteTitleHelper.GetTitle` sin los `#` de encabezado; propiedades y métodos de `Strings` listados abajo
  (los usan las tareas 8 a 10).

- [ ] **Step 1: Write the failing tests** (en `NoteTitleHelperTests`)

```csharp
    [Theory]
    [InlineData("# Tema 3", "Tema 3")]
    [InlineData("### Sub  ", "Sub")]
    [InlineData("#hashtag", "#hashtag")]
    [InlineData("####### siete", "####### siete")]
    public void GetTitle_StripsMarkdownHeadingMarks(string firstLine, string expected) =>
        Assert.Equal(expected, NoteTitleHelper.GetTitle(firstLine + "\nresto"));

    [Fact]
    public void GetTitle_HeadingWithoutText_IsThePlaceholder() =>
        Assert.Equal(NoteTitleHelper.PlaceholderTitle, NoteTitleHelper.GetTitle("#   \nresto"));
```

- [ ] **Step 2: Run to verify they fail** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build --filter NoteTitleHelperTests` → FAIL ("# Tema 3").

- [ ] **Step 3: Implement**

```csharp
    // "# Tema 3" en la pestaña es "Tema 3": los # son marcas de Markdown, no parte del nombre (spec de notas
    // vinculadas, decisión 6). Solo de 1 a 6 almohadillas seguidas de espacio, como en Markdown: "#hashtag" no.
    [System.Text.RegularExpressions.GeneratedRegex(@"^#{1,6}(?:[ \t]+|$)")]
    private static partial System.Text.RegularExpressions.Regex HeadingMarks();

    public static string GetTitle(string text)
    {
        var firstLine = text.Split('\n')[0].TrimEnd('\r').Trim();
        firstLine = HeadingMarks().Replace(firstLine, "", 1).Trim();
        return string.IsNullOrEmpty(firstLine) ? PlaceholderTitle : firstLine;
    }
```

`NoteTitleHelper` pasa a `public static partial class NoteTitleHelper`. Comprobar que `MarkdownExportTests` sigue
en verde (una nota que empieza por `# X` exporta ahora `# X`, no `# # X`; si algún test esperaba lo antiguo,
corregir la expectativa, que era el fallo).

Textos nuevos en `Strings.cs` (sección propia "Notas vinculadas"):

```csharp
    // --- Notas vinculadas a archivos ---
    public static string LinkedOpenFile => T("Open file…", "Abrir archivo…", "Datei öffnen…", "Ouvrir un fichier…", "Abrir arquivo…");
    public static string LinkedFileFilter => T("Text and Markdown files", "Archivos de texto y Markdown",
        "Text- und Markdown-Dateien", "Fichiers texte et Markdown", "Arquivos de texto e Markdown")
        + " (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt";
    public static string LinkedUnsupported(string name) => T(
        $"{name} isn't a .md, .markdown or .txt file.", $"{name} no es un archivo .md, .markdown ni .txt.",
        $"{name} ist keine .md-, .markdown- oder .txt-Datei.", $"{name} n'est pas un fichier .md, .markdown ou .txt.",
        $"{name} não é um arquivo .md, .markdown nem .txt.");
    public static string LinkedTooLarge(string name) => T(
        $"{name} is larger than 2 MB and can't be opened as a note.", $"{name} ocupa más de 2 MB y no se puede abrir como nota.",
        $"{name} ist größer als 2 MB und kann nicht als Notiz geöffnet werden.", $"{name} dépasse 2 Mo et ne peut pas être ouvert comme note.",
        $"{name} tem mais de 2 MB e não pode ser aberto como nota.");
    public static string LinkedBadEncoding(string name) => T(
        $"{name} isn't UTF-8 or UTF-16 text. Aldune won't open it so as not to damage its accents.",
        $"{name} no está en UTF-8 ni en UTF-16. Aldune no lo abre para no estropear sus tildes.",
        $"{name} ist kein UTF-8- oder UTF-16-Text. Aldune öffnet die Datei nicht, um Sonderzeichen nicht zu beschädigen.",
        $"{name} n'est pas en UTF-8 ni en UTF-16. Aldune ne l'ouvre pas pour ne pas abîmer ses accents.",
        $"{name} não está em UTF-8 nem em UTF-16. O Aldune não o abre para não estragar os acentos.");
    public static string LinkedUnreadable(string name) => T(
        $"{name} couldn't be read.", $"No se pudo leer {name}.", $"{name} konnte nicht gelesen werden.",
        $"Impossible de lire {name}.", $"Não foi possível ler {name}.");
    public static string LinkedAlreadyLinkedElsewhere(string name) => T(
        $"{name} is already open in another note.", $"{name} ya está abierto en otra nota.",
        $"{name} ist bereits in einer anderen Notiz geöffnet.", $"{name} est déjà ouvert dans une autre note.",
        $"{name} já está aberto em outra nota.");
    public static string LinkedUnavailable(string path) => T(
        $"File not available: {path}", $"Archivo no disponible: {path}", $"Datei nicht verfügbar: {path}",
        $"Fichier indisponible : {path}", $"Arquivo indisponível: {path}");
    public static string LinkedRetry => T("Retry", "Reintentar", "Erneut versuchen", "Réessayer", "Tentar de novo");
    public static string LinkedLocate => T("Find…", "Buscar…", "Suchen…", "Rechercher…", "Procurar…");
    public static string LinkedConvert => T("Convert to normal note", "Convertir en nota normal",
        "In normale Notiz umwandeln", "Convertir en note normale", "Converter em nota normal");
    public static string LinkedConvertWarning(string path) => T(
        $"The note keeps its text and stops following the file. The file stays on disk, as plain text:\n{path}",
        $"La nota conserva su texto y deja de seguir al archivo. El archivo sigue en el disco, en claro:\n{path}",
        $"Die Notiz behält ihren Text und folgt der Datei nicht mehr. Die Datei bleibt unverschlüsselt auf dem Datenträger:\n{path}",
        $"La note garde son texte et ne suit plus le fichier. Le fichier reste sur le disque, en clair :\n{path}",
        $"A nota mantém o texto e deixa de acompanhar o arquivo. O arquivo continua no disco, sem criptografia:\n{path}");
    public static string LinkedConflictPrefix => T("⚠ Conflict: ", "⚠ Conflicto: ", "⚠ Konflikt: ", "⚠ Conflit : ", "⚠ Conflito: ");
    public static string LinkedConflictToast(string title) => T(
        $"{title} changed in another program while you were editing it here. Your version is in a new note.",
        $"{title} cambió en otro programa mientras lo editabas aquí. Tu versión está en una nota nueva.",
        $"{title} wurde in einem anderen Programm geändert, während du es hier bearbeitet hast. Deine Version steht in einer neuen Notiz.",
        $"{title} a été modifié dans un autre programme pendant que vous l'éditiez ici. Votre version est dans une nouvelle note.",
        $"{title} mudou em outro programa enquanto você o editava aqui. Sua versão está em uma nota nova.");
    public static string LinkedProtectDisabled => T(
        "A linked note is the file itself, and the file is plain text. Convert it to a normal note to protect it.",
        "Una nota vinculada es el propio archivo, y el archivo está en claro. Conviértela en nota normal para protegerla.",
        "Eine verknüpfte Notiz ist die Datei selbst, und die Datei ist unverschlüsselt. Wandle sie in eine normale Notiz um, um sie zu schützen.",
        "Une note liée est le fichier lui-même, et le fichier est en clair. Convertissez-la en note normale pour la protéger.",
        "Uma nota vinculada é o próprio arquivo, e o arquivo está sem criptografia. Converta-a em nota normal para protegê-la.");
```

- [ ] **Step 4: Run** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build` → PASS, todos.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/NoteTitleHelper.cs tests/Aldune.Core.Tests/NoteTitleHelperTests.cs src/Aldune/Resources/Strings.cs
git commit -m "Titulo de la nota sin los # de encabezado; textos de las notas vinculadas en cinco idiomas"
```

---

### Task 8: WPF — vigilancia en segundo plano y ventanas que siguen al archivo

**Files:**
- Create: `src/Aldune/Windowing/LinkedNoteDisplay.cs`
- Create: `src/Aldune/Windowing/LinkedFileCoordinator.cs`
- Modify: `src/Aldune/Windowing/AppCoordinator.cs`
- Modify: `src/Aldune/Windowing/NoteWindow.xaml.cs` (`Flush`, `PruneExpiredTasks`, `ToggleTask`, métodos nuevos)
- Modify: `src/Aldune/App.xaml.cs` (crear y arrancar tras el coordinador)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (`Refresh`: publicar los vínculos a `LinkedNoteDisplay`)

**Interfaces:**
- Consumes: `LinkedFileService`, `NoteFileLink`, `ReconcileResult` (tareas 4 y 6); `Strings.LinkedConflictPrefix`,
  `Strings.LinkedConflictToast` (tarea 7).
- Produces:
  - `internal static class LinkedNoteDisplay`: `void Set(IEnumerable<NoteFileLink>)`, `bool IsLinked(Guid)`,
    `string? PathOf(Guid)`, `bool IsUnavailable(Guid)`, `void SetUnavailable(Guid, bool)`, `string Title(Note)`.
  - `internal sealed class LinkedFileCoordinator : IDisposable`: `Start()`, `RequestCheck(Guid)`, `Refresh()`, `Dispose()`.
  - `AppCoordinator`: `LinkedFileService LinkedFiles { get; }`, `bool IsLinked(Guid)`, `void RequestLinkedCheck(Guid)`.
  - `NoteWindow`: `internal void FlushPending()`, `internal void ReloadFromRepository()`, `internal void SetFileUnavailable(string? path)` (la franja llega en la tarea 9; aquí solo el método, que la tarea 9 completa).

Diseño del hilo: **nada que toque el disco corre en el hilo de la interfaz**. `LinkedFileCoordinator` tiene una
cola en serie (`System.Threading.Channels.Channel<Guid>` sin límite, un solo consumidor en `Task.Run`) que llama a
`LinkedFileService.Reconcile` y devuelve el resultado al `Dispatcher`. Una petición para una nota con ventana
abierta pasa antes por el hilo de la interfaz para vaciar lo escrito (`FlushPending`), de modo que lo tecleado y
aún no guardado cuente como cambio pendiente y un cambio externo dé conflicto en vez de pisarlo.

- [ ] **Step 1: `LinkedNoteDisplay`** (mismo patrón que `NoteChannelDisplay`)

```csharp
using System.IO;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Qué notas son archivos, para los convertidores de XAML y las ventanas (que no reciben el repositorio).
/// Mismo patrón que <see cref="NoteChannelDisplay"/>: lo rellena el dock al refrescarse.
/// </summary>
internal static class LinkedNoteDisplay
{
    private static Dictionary<Guid, string> _paths = [];
    private static readonly HashSet<Guid> Unavailable = [];

    public static void Set(IEnumerable<NoteFileLink> links) =>
        _paths = links.ToDictionary(link => link.NoteId, link => link.Path);

    public static bool IsLinked(Guid noteId) => _paths.ContainsKey(noteId);

    public static string? PathOf(Guid noteId) => _paths.GetValueOrDefault(noteId);

    public static bool IsUnavailable(Guid noteId) => Unavailable.Contains(noteId);

    public static void SetUnavailable(Guid noteId, bool unavailable)
    {
        if (unavailable) Unavailable.Add(noteId);
        else Unavailable.Remove(noteId);
    }

    /// <summary>El título de siempre; si la primera línea está vacía y la nota es un archivo, su nombre.</summary>
    public static string Title(Note note)
    {
        var title = NoteTitleHelper.GetTitle(note.Text);
        return title == NoteTitleHelper.PlaceholderTitle && PathOf(note.Id) is { } path
            ? Path.GetFileNameWithoutExtension(path)
            : title;
    }
}
```

En `EdgeDockWindow.Refresh`, junto a `NoteChannelDisplay.Set(...)`: `LinkedNoteDisplay.Set(_repository.GetFileLinks());`.
En `NoteTitleConverter` y `NoteTabLabelConverter`, sustituir `NoteTitleHelper.GetTitle(text)` por
`LinkedNoteDisplay.Title(note)` cuando tengan la `Note` (en `NoteTabLabelConverter`, mirar cómo recibe la nota y
pasarla). En `NoteWindow`, las dos asignaciones a `Title` (líneas ~119 y ~623) usan
`LinkedNoteDisplay.Title(...)` con una `Note` temporal o, más simple, comprobando el placeholder igual que arriba.

- [ ] **Step 2: `LinkedFileCoordinator`**

```csharp
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Channels;
using System.Windows.Threading;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// Vigila los archivos de las notas vinculadas activas y pasa cada comprobación a
/// <see cref="LinkedFileService"/> en un hilo de fondo, de una en una (spec, decisión 4). El vigilante de
/// Windows falla en unidades de red, así que además se sondea la fecha: cada 3 s las notas abiertas, cada
/// 30 s las demás. Los resultados vuelven al hilo de la interfaz por <paramref name="onResult"/>.
/// </summary>
internal sealed class LinkedFileCoordinator : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private const int ClosedNotePollEvery = 10;   // 10 × 3 s = 30 s

    private readonly LinkedFileService _service;
    private readonly NotesRepository _repository;
    private readonly Dispatcher _dispatcher;
    private readonly Func<Guid, bool> _isOpen;
    private readonly Action<Guid> _flushOpenWindow;
    private readonly Action<Guid, ReconcileResult> _onResult;
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _queued = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private volatile IReadOnlyList<NoteFileLink> _activeLinks = [];
    private HashSet<Guid> _activeIds = [];
    private Timer? _pollTimer;
    private int _pollTick;

    public LinkedFileCoordinator(LinkedFileService service, NotesRepository repository, Dispatcher dispatcher,
        Func<Guid, bool> isOpen, Action<Guid> flushOpenWindow, Action<Guid, ReconcileResult> onResult)
    {
        _service = service;
        _repository = repository;
        _dispatcher = dispatcher;
        _isOpen = isOpen;
        _flushOpenWindow = flushOpenWindow;
        _onResult = onResult;
    }

    public void Start()
    {
        _ = Task.Run(ConsumeAsync);
        Refresh();
        foreach (var link in _activeLinks) RequestCheck(link.NoteId);   // al arrancar: lo que cambió con Aldune cerrada
        _pollTimer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
    }

    /// <summary>En el hilo de la interfaz: vacía lo escrito en la ventana abierta y encola la comprobación.</summary>
    public void RequestCheck(Guid noteId)
    {
        _flushOpenWindow(noteId);
        if (_queued.TryAdd(noteId, 0)) _queue.Writer.TryWrite(noteId);
    }

    /// <summary>
    /// Rehace la lista de vínculos activos y los vigilantes por carpeta. Se llama en cada refresco del
    /// coordinador; las notas que acaban de volver a activas (restauradas del archivo o la papelera) se
    /// comprueban, porque mientras no estaban activas nadie las vigilaba.
    /// </summary>
    public void Refresh()
    {
        var active = _repository.GetByState(NoteState.Active).Select(note => note.Id).ToHashSet();
        var links = _repository.GetFileLinks().Where(link => active.Contains(link.NoteId)).ToList();
        var newlyActive = links.Where(link => !_activeIds.Contains(link.NoteId)).Select(link => link.NoteId).ToList();
        _activeLinks = links;
        _activeIds = links.Select(link => link.NoteId).ToHashSet();

        var folders = links.Select(link => Path.GetDirectoryName(link.Path)!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in _watchers.Keys.Except(folders, StringComparer.OrdinalIgnoreCase).ToList())
        {
            _watchers[gone].Dispose();
            _watchers.Remove(gone);
        }
        foreach (var folder in folders.Where(folder => !_watchers.ContainsKey(folder)))
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                };
                watcher.Changed += (_, e) => OnFileEvent(e.FullPath);
                watcher.Created += (_, e) => OnFileEvent(e.FullPath);
                watcher.Deleted += (_, e) => OnFileEvent(e.FullPath);
                watcher.Renamed += (_, e) => OnRenamed(e.OldFullPath, e.FullPath);
                watcher.EnableRaisingEvents = true;
                _watchers[folder] = watcher;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Carpeta que no existe o unidad desconectada: el sondeo lo cubre.
            }
        }

        foreach (var id in newlyActive) RequestCheck(id);
    }

    private void OnFileEvent(string path)
    {
        foreach (var link in _activeLinks.Where(link => string.Equals(link.Path, path, StringComparison.OrdinalIgnoreCase)))
            _dispatcher.BeginInvoke(() => RequestCheck(link.NoteId));
    }

    // Un renombrado dentro de la misma carpeta se sigue solo (spec, decisión 5). Los editores que guardan con
    // "temporal + renombrar" producen un Renamed hacia la ruta vinculada: eso es un cambio, no un renombrado.
    private void OnRenamed(string oldPath, string newPath)
    {
        OnFileEvent(newPath);
        foreach (var link in _activeLinks.Where(link => string.Equals(link.Path, oldPath, StringComparison.OrdinalIgnoreCase)))
        {
            if (!LinkedFileFormat.IsSupportedExtension(newPath)) continue;
            _dispatcher.BeginInvoke(() =>
            {
                _repository.SaveFileLink(link with { Path = newPath });
                Refresh();
                RequestCheck(link.NoteId);
            });
        }
    }

    // En el hilo del temporizador: solo mira fechas (puede tardar en una unidad de red) y pide comprobar.
    private void Poll()
    {
        bool all = Interlocked.Increment(ref _pollTick) % ClosedNotePollEvery == 0;
        foreach (var link in _activeLinks)
        {
            if (!all && !_isOpen(link.NoteId)) continue;
            if (LinkedFileService.LooksChanged(link) || LinkedNoteDisplay.IsUnavailable(link.NoteId))
                _dispatcher.BeginInvoke(() => RequestCheck(link.NoteId));
        }
    }

    private async Task ConsumeAsync()
    {
        await foreach (var noteId in _queue.Reader.ReadAllAsync())
        {
            _queued.TryRemove(noteId, out _);
            ReconcileResult result;
            try
            {
                result = _service.Reconcile(noteId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                result = new ReconcileResult(ReconcileOutcome.Unavailable);
            }
            await _dispatcher.BeginInvoke(() => _onResult(noteId, result));
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _queue.Writer.TryComplete();
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
    }
}
```

`_isOpen` se llama desde el hilo del temporizador: el coordinador le pasa una función que lee un
`ConcurrentDictionary<Guid, byte>` de ids abiertos que él mantiene al abrir y cerrar ventanas (no
`_openNoteWindows`, que es del hilo de la interfaz).

- [ ] **Step 3: `AppCoordinator`**

Campos y arranque (el constructor recibe ya el repositorio; añadir):

```csharp
    private readonly ConcurrentDictionary<Guid, byte> _openNoteIds = new();
    private LinkedFileCoordinator? _linkedWatch;

    public LinkedFileService LinkedFiles { get; }

    public bool IsLinked(Guid noteId) => LinkedNoteDisplay.IsLinked(noteId);

    /// <summary>Lo llama App tras crear el coordinador, ya con el Dispatcher en marcha.</summary>
    public void StartLinkedFiles(Dispatcher dispatcher)
    {
        LinkedNoteDisplay.Set(_repository.GetFileLinks());
        _linkedWatch = new LinkedFileCoordinator(LinkedFiles, _repository, dispatcher,
            id => _openNoteIds.ContainsKey(id),
            id => { if (_openNoteWindows.TryGetValue(id, out var window)) window.FlushPending(); },
            OnLinkedFileResult);
        _linkedWatch.Start();
    }

    public void RequestLinkedCheck(Guid noteId) => _linkedWatch?.RequestCheck(noteId);
```

En el constructor: `LinkedFiles = new LinkedFileService(repository, NextNoteColor, () => Strings.LinkedConflictPrefix);`
(`NextNoteColor` ya existe y es lo que usa `CreateAndOpenNote`).

Mantener `_openNoteIds` donde se añaden y quitan ventanas de `_openNoteWindows` (`_openNoteIds[note.Id] = 0;` /
`_openNoteIds.TryRemove(id, out _);`).

`RefreshAll` termina con `_linkedWatch?.Refresh();`. En `AfterSuccessfulSync`, antes de `RefreshNoteAppearance()`:
`foreach (var link in _repository.GetFileLinks().Where(l => l.SyncEnabled)) RequestLinkedCheck(link.NoteId);`
(lo que llegó por la sync es un cambio pendiente que hay que escribir en el archivo).

En `PruneCompletedTasksInClosedNotes`, el conjunto que se salta incluye las vinculadas:

```csharp
        var skip = _openNoteWindows.Keys.ToHashSet();
        // En una nota vinculada no se reescriben líneas solas: borrarían líneas del archivo sin que el usuario
        // haga nada (spec, decisión 4).
        skip.UnionWith(_repository.GetFileLinks().Select(link => link.NoteId));
        if (_repository.PruneExpiredCompletedTasksInActiveNotes(_settings.AutoHideCompletedTasksDelay, skip) > 0)
```

Resultado de cada comprobación:

```csharp
    private void OnLinkedFileResult(Guid noteId, ReconcileResult result)
    {
        _openNoteWindows.TryGetValue(noteId, out var window);
        bool unavailable = result.Outcome == ReconcileOutcome.Unavailable;
        bool changedState = LinkedNoteDisplay.IsUnavailable(noteId) != unavailable;
        LinkedNoteDisplay.SetUnavailable(noteId, unavailable);
        window?.SetFileUnavailable(unavailable ? LinkedNoteDisplay.PathOf(noteId) : null);

        switch (result.Outcome)
        {
            case ReconcileOutcome.Reloaded:
                window?.ReloadFromRepository();
                RefreshAll();
                break;
            case ReconcileOutcome.Conflict:
                window?.ReloadFromRepository();
                RefreshAll();
                if (_repository.GetById(noteId) is { } note)
                    ShowToast(Strings.LinkedConflictToast(LinkedNoteDisplay.Title(note)));
                break;
            case ReconcileOutcome.Written:
                window?.ReapplyAppearance();   // la señal de sync pasa a pendiente si la nota se sincroniza
                break;
            default:
                if (changedState) RefreshAll();
                break;
        }
    }
```

`ShowToast`: usar el mismo camino con el que el coordinador ya enseña avisos (buscar `ToastCenter` en
`AppCoordinator`/`App.xaml.cs` y llamar a su método de mostrar con el texto; si solo existe para
recordatorios, añadir una sobrecarga de texto simple en `ToastCenter` sin cambiar las existentes).

En `App.xaml.cs`, justo después de `_coordinator = coordinator;`: `coordinator.StartLinkedFiles(Dispatcher);` y, en
el cierre de la app donde se libera `_singleInstance`, liberar también el vigilante (añadir a `AppCoordinator` un
`public void StopLinkedFiles() => _linkedWatch?.Dispose();` y llamarlo ahí).

- [ ] **Step 4: `NoteWindow`**

```csharp
    private bool IsLinked => _coordinator.IsLinked(_note.Id);

    /// <summary>Guarda ya lo tecleado. Antes de mirar el archivo: así lo escrito cuenta como pendiente y un
    /// cambio externo da conflicto en vez de pisarlo.</summary>
    internal void FlushPending()
    {
        _autosaveTimer.Stop();
        Flush();
    }

    /// <summary>El archivo cambió por fuera y la nota ya tiene su texto: se pone en la ventana sin marcarla
    /// como editada, con el cursor en la misma línea si existe.</summary>
    internal void ReloadFromRepository()
    {
        if (_hasPendingEdit || _repository.GetById(_note.Id) is not { } fresh) return;
        int line = TextBody.GetLineIndexFromCharacterIndex(TextBody.CaretIndex);
        var (title, body) = NoteText.Split(fresh.Text);
        TitleBox.Text = title;
        TextBody.Text = body;
        _note.Text = fresh.Text;
        // Los TextChanged de arriba marcan "hay cambios": no los hay, lo que se ve es lo guardado.
        _hasPendingEdit = false;
        _autosaveTimer.Stop();
        if (line >= 0 && line < TextBody.LineCount) TextBody.CaretIndex = TextBody.GetCharacterIndexFromLineIndex(line);
    }

    /// <summary>Con <paramref name="path"/>: archivo no disponible, solo lectura. Con null: normal.</summary>
    internal void SetFileUnavailable(string? path)
    {
        TextBody.IsReadOnly = TitleBox.IsReadOnly = path is not null;
    }
```

(Si `_note.Text` no es asignable porque `_note` es `readonly` pero la propiedad sí es `set`, vale; la tarea 9 añade
la franja a `SetFileUnavailable`.)

En `Flush()`, tras `_repository.UpdateText(_note.Id, CurrentText);`:

```csharp
            // La nota vinculada se guarda primero en la base de datos (a salvo aunque el disco falle) y luego
            // se pide escribir el archivo, en segundo plano.
            if (IsLinked) _coordinator.RequestLinkedCheck(_note.Id);
```

Cuidado: `RequestCheck` llama a `FlushPending` → `Flush`, que ya no tiene nada pendiente (`_hasPendingEdit` es
false al entrar), así que no hay recursión.

En `PruneExpiredTasks`, primera línea: `if (IsLinked) return;`. En `ToggleTask`, la condición de asentar:
`var settled = _settings is { MoveCompletedTasksToEnd: true } && !IsLinked ? ... : ...`.

- [ ] **Step 5: Build y tests**

Run: `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build` → 0 errores, 4 avisos CA1416, todo PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Aldune/Windowing/LinkedNoteDisplay.cs src/Aldune/Windowing/LinkedFileCoordinator.cs src/Aldune/Windowing/AppCoordinator.cs src/Aldune/Windowing/NoteWindow.xaml.cs src/Aldune/Windowing/EdgeDockWindow.xaml.cs src/Aldune/Windowing/NoteTitleConverter.cs src/Aldune/Windowing/NoteTabLabelConverter.cs src/Aldune/App.xaml.cs src/Aldune/Windowing/ToastCenter.cs
git commit -m "Notas vinculadas en WPF: vigilancia en segundo plano, guardado al archivo, recarga y conflictos"
```

(Quitar de la lista `ToastCenter.cs` si no se tocó.)

---

### Task 9: Franja de "no disponible", Proteger desactivado y convertir en nota normal

**Files:**
- Modify: `src/Aldune/Windowing/NoteWindow.xaml` (franja encima del cuerpo)
- Modify: `src/Aldune/Windowing/NoteWindow.xaml.cs` (`SetFileUnavailable`, manejadores, botón de proteger)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (`TabMenuProtectionButton` desactivado para vinculadas, ~línea 2319)
- Modify: `src/Aldune/Windowing/AppCoordinator.cs` (`LocateLinkedFile`, `ConvertLinkedToNormal`)

**Interfaces:**
- Consumes: `LinkedFileService.Relink`, `Unlink`; `Strings.LinkedUnavailable`, `LinkedRetry`, `LinkedLocate`, `LinkedConvert`,
  `LinkedConvertWarning`, `LinkedProtectDisabled`, `LinkedFileFilter`, `LinkedAlreadyLinkedElsewhere` y los de rechazo.
- Produces: `AppCoordinator.LocateLinkedFile(Guid, Window)`, `AppCoordinator.ConvertLinkedToNormal(Guid, Window)`,
  `AppCoordinator.LinkMessage(LinkResult, string path)` (texto del error, o null).

- [ ] **Step 1: XAML de la franja** — en `NoteWindow.xaml`, como primer hijo del contenedor que aloja `BodyHost`
  (mismo `Grid.Row`, `VerticalAlignment="Top"`, o una fila nueva encima si el cuerpo está en un `Grid` con filas):

```xml
            <!-- Archivo vinculado no disponible (Z: desconectado, borrado…): la nota pasa a solo lectura y
                 ofrece reintentar, buscar el archivo o quedarse como nota normal (spec, decisión 5). -->
            <Border x:Name="UnavailableBar" Visibility="Collapsed" Padding="10,6" Margin="0,0,0,4"
                    Background="{DynamicResource AlduneDangerBgBrush}" CornerRadius="{DynamicResource AlduneRadius4}">
                <StackPanel>
                    <TextBlock x:Name="UnavailableText" TextWrapping="Wrap" FontSize="12"
                               Foreground="{DynamicResource AlduneOnDangerBrush}" />
                    <WrapPanel Margin="0,6,0,0">
                        <Button x:Name="UnavailableRetryButton" Click="OnUnavailableRetryClick" Margin="0,0,6,0"
                                Style="{StaticResource NoteMenuItemStyle}" />
                        <Button x:Name="UnavailableLocateButton" Click="OnUnavailableLocateClick" Margin="0,0,6,0"
                                Style="{StaticResource NoteMenuItemStyle}" />
                        <Button x:Name="UnavailableConvertButton" Click="OnUnavailableConvertClick"
                                Style="{StaticResource NoteMenuItemStyle}" />
                    </WrapPanel>
                </StackPanel>
            </Border>
```

Si `NoteMenuItemStyle` no existe en `NoteWindow.xaml` (está en el dock), usar el estilo de botón que use el
`ActionsPopup` de la nota. Comprobar que `AlduneDangerBgBrush` y `AlduneOnDangerBrush` existen (las claves
`DangerBg` y `OnDanger` de `AppPalette` se publican como `Aldune<Clave>Brush`).

- [ ] **Step 2: Código de la ventana**

```csharp
    internal void SetFileUnavailable(string? path)
    {
        bool unavailable = path is not null;
        TextBody.IsReadOnly = TitleBox.IsReadOnly = unavailable;
        UnavailableBar.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        if (!unavailable) return;
        UnavailableText.Text = Strings.LinkedUnavailable(path!);
        UnavailableRetryButton.Content = Strings.LinkedRetry;
        UnavailableLocateButton.Content = Strings.LinkedLocate;
        UnavailableConvertButton.Content = Strings.LinkedConvert;
        FitHeightToContent();
    }

    private void OnUnavailableRetryClick(object sender, RoutedEventArgs e) => _coordinator.RequestLinkedCheck(_note.Id);

    private void OnUnavailableLocateClick(object sender, RoutedEventArgs e) => _coordinator.LocateLinkedFile(_note.Id, this);

    private void OnUnavailableConvertClick(object sender, RoutedEventArgs e) => _coordinator.ConvertLinkedToNormal(_note.Id, this);
```

Al abrir la ventana (constructor, tras `ApplyColor`): `if (LinkedNoteDisplay.IsUnavailable(note.Id)) SetFileUnavailable(LinkedNoteDisplay.PathOf(note.Id));`.

Proteger: donde la ventana prepara `ProtectionButton` (buscar su `Content`), añadir
`ProtectionButton.IsEnabled = !IsLinked; ProtectionButton.ToolTip = IsLinked ? Strings.LinkedProtectDisabled : null;`
y lo mismo en `EdgeDockWindow` junto a la línea `TabMenuProtectionButton.Content = ...`, con
`LinkedNoteDisplay.IsLinked(note.Id)`. `ToolTipService.ShowOnDisabled="True"` en los dos botones para que la
explicación se vea con el botón desactivado.

- [ ] **Step 3: `AppCoordinator`**

```csharp
    /// <summary>El texto de un vínculo rechazado, o null si salió bien.</summary>
    internal static string? LinkMessage(LinkResult result, string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return result.Outcome switch
        {
            LinkOutcome.UnsupportedExtension => Strings.LinkedUnsupported(name),
            LinkOutcome.TooLarge => Strings.LinkedTooLarge(name),
            LinkOutcome.UnsupportedEncoding => Strings.LinkedBadEncoding(name),
            LinkOutcome.Unreadable => Strings.LinkedUnreadable(name),
            _ => null,
        };
    }

    public void LocateLinkedFile(Guid noteId, Window owner)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Strings.LinkedFileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(owner) != true) return;

        var result = LinkedFiles.Relink(noteId, dialog.FileName);
        if (result.Outcome == LinkOutcome.AlreadyLinked)
        {
            AppDialog.Show(owner, Strings.LinkedAlreadyLinkedElsewhere(System.IO.Path.GetFileName(dialog.FileName)),
                Strings.LinkedLocate, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (LinkMessage(result, dialog.FileName) is { } error)
        {
            AppDialog.Show(owner, error, Strings.LinkedLocate, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        RefreshAll();
        RequestLinkedCheck(noteId);
    }

    public void ConvertLinkedToNormal(Guid noteId, Window owner)
    {
        if (LinkedNoteDisplay.PathOf(noteId) is not { } path) return;
        if (AppDialog.Show(owner, Strings.LinkedConvertWarning(path), Strings.LinkedConvert,
                MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;

        LinkedFiles.Unlink(noteId);
        LinkedNoteDisplay.SetUnavailable(noteId, false);
        if (_openNoteWindows.TryGetValue(noteId, out var window)) window.SetFileUnavailable(null);
        RefreshAll();
    }
```

Comprobar la firma real de `AppDialog.Show` (devuelve `MessageBoxResult`) y ajustar si difiere.

- [ ] **Step 4: Build y tests** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build` → verde.

- [ ] **Step 5: Commit**

```bash
git add src/Aldune/Windowing/NoteWindow.xaml src/Aldune/Windowing/NoteWindow.xaml.cs src/Aldune/Windowing/EdgeDockWindow.xaml.cs src/Aldune/Windowing/AppCoordinator.cs
git commit -m "Notas vinculadas: franja de archivo no disponible con solo lectura, buscar y convertir; Proteger desactivado"
```

---

### Task 10: Abrir archivos (menú del "+", bandeja y arrastrar) y marca en la pestaña

**Files:**
- Modify: `src/Aldune/Windowing/AppCoordinator.cs` (`OpenLinkedFiles`, `ShowOpenLinkedFileDialog`)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml` (`NewNoteMenuPopup`, `AllowDrop`, marca en la plantilla de la pestaña)
- Modify: `src/Aldune/Windowing/EdgeDockWindow.xaml.cs` (manejadores)
- Modify: `src/Aldune/Windowing/TrayIcon.cs`
- Create: `src/Aldune/Windowing/NoteLinkedConverter.cs`
- Modify: `src/Aldune/Windowing/NotesManagerWindow.xaml` (misma marca y tooltip en la fila)

**Interfaces:**
- Consumes: `LinkedFiles.Open`, `LinkMessage` (tareas 6 y 9), `Strings.LinkedOpenFile`, `Strings.LinkedFileFilter`.
- Produces: `AppCoordinator.OpenLinkedFiles(IEnumerable<string> paths, Window? owner)` (la usa también la tanda 2,
  "Abrir con"); `AppCoordinator.ShowOpenLinkedFileDialog(Window? owner)`.

- [ ] **Step 1: `AppCoordinator`**

```csharp
    public void ShowOpenLinkedFileDialog(Window? owner)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Strings.LinkedFileFilter, Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(owner) != true) return;
        OpenLinkedFiles(dialog.FileNames, owner);
    }

    /// <summary>
    /// Abre como notas vinculadas los archivos dados (diálogo, arrastrar, "Abrir con"). Uno ya vinculado
    /// muestra su nota; uno nuevo entra al final del mazo, con la etiqueta de la vista si la hay, y se abre
    /// el último. Los rechazados se explican juntos al final.
    /// </summary>
    public void OpenLinkedFiles(IEnumerable<string> paths, Window? owner)
    {
        Guid? last = null;
        var errors = new List<string>();
        foreach (var path in paths)
        {
            var result = LinkedFiles.Open(path);
            if (result.NoteId is { } id)
            {
                last = id;
                if (result.Outcome == LinkOutcome.Linked &&
                    _settings is { DockView: DockViewKind.Tag, DockTagFilter: { } viewTag })
                    _repository.SetTags(id, [viewTag]);
            }
            else if (LinkMessage(result, path) is { } error)
            {
                errors.Add(error);
            }
        }

        LinkedNoteDisplay.Set(_repository.GetFileLinks());
        RefreshAll();
        if (last is { } open) OpenNoteById(open);
        if (errors.Count > 0)
            AppDialog.Show(owner, string.Join("\n", errors), Strings.LinkedOpenFile, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
```

(Comprobar que `AppDialog.Show` admite `owner` nulo; si no, usar el dock más cercano: `DockNearCursor()`.)

- [ ] **Step 2: Menú del "+" y bandeja**

En `EdgeDockWindow.xaml`, dentro de `NewNoteMenuPopup`, tras el botón de `OnNewNoteMenuClipboardClick`:

```xml
                        <Button Content="{x:Static res:Strings.LinkedOpenFile}" Click="OnNewNoteMenuOpenFileClick"
                                Style="{StaticResource NoteMenuItemStyle}" />
```

En `EdgeDockWindow.xaml.cs`, junto a `OnNewNoteMenuClipboardClick` (copiar cómo cierra el popup y mantiene el dock):

```csharp
    private void OnNewNoteMenuOpenFileClick(object sender, RoutedEventArgs e)
    {
        NewNoteMenuPopup.IsOpen = false;
        HoldOpenForWindow();
        _coordinator.ShowOpenLinkedFileDialog(this);
    }
```

En `TrayIcon.cs`, tras la línea de `Strings.TrayNewNote`:

```csharp
        menu.Items.Add(new ToolStripMenuItem(Strings.LinkedOpenFile, null, (_, _) => _coordinator.ShowOpenLinkedFileDialog(null)));
```

- [ ] **Step 3: Arrastrar al dock**

En la raíz de `EdgeDockWindow.xaml`: `AllowDrop="True" DragOver="OnDockDragOver" Drop="OnDockDrop"`. En el `.cs`:

```csharp
    // Arrastrar archivos desde el Explorador abre cada uno como nota vinculada (spec, decisión 6).
    private void OnDockDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDockDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        e.Handled = true;
        _coordinator.OpenLinkedFiles(files, this);
    }
```

Si el dock en reposo es una tira fina que no recibe el arrastre, la sonda de la tarea 11 lo dirá; en ese caso,
expandir el dock en `DragEnter` con el mismo mecanismo que el paso del ratón (buscar cómo `_fanState` expande al
pasar) y anotarlo en STATUS.

- [ ] **Step 4: Marca en la pestaña y en el gestor**

`NoteLinkedConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>Visible si la nota es un archivo vinculado. Con ConverterParameter="Path", la ruta (tooltip).</summary>
public sealed class NoteLinkedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Note note) return parameter as string == "Path" ? null : Visibility.Collapsed;
        return parameter as string == "Path"
            ? LinkedNoteDisplay.PathOf(note.Id)
            : LinkedNoteDisplay.IsLinked(note.Id) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

En la plantilla de la pestaña del dock (cerca de la etiqueta del título; mirar cómo se enlaza
`NoteTitleConverter` para saber si el `DataContext` es la `Note`), un glifo pequeño:

```xml
<TextBlock Text="&#xE7C3;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="10" Margin="0,0,4,0"
           VerticalAlignment="Center" Opacity="0.75"
           Visibility="{Binding Converter={StaticResource NoteLinkedConverter}}"
           ToolTip="{Binding Converter={StaticResource NoteLinkedConverter}, ConverterParameter=Path}" />
```

Declarar `<local:NoteLinkedConverter x:Key="NoteLinkedConverter" />` en los recursos de la ventana junto a los
otros convertidores. El color hereda el de la etiqueta de la pestaña. Lo mismo en la fila del gestor
(`NotesManagerWindow.xaml`, junto al título de la fila).

- [ ] **Step 5: Build y tests** — `dotnet build Aldune.slnx -c Debug && dotnet test Aldune.slnx --no-build` → verde.

- [ ] **Step 6: Commit**

```bash
git add src/Aldune/Windowing/AppCoordinator.cs src/Aldune/Windowing/EdgeDockWindow.xaml src/Aldune/Windowing/EdgeDockWindow.xaml.cs src/Aldune/Windowing/TrayIcon.cs src/Aldune/Windowing/NoteLinkedConverter.cs src/Aldune/Windowing/NotesManagerWindow.xaml
git commit -m "Notas vinculadas: abrir archivos desde el +, la bandeja y arrastrando al dock; marca en pestana y gestor"
```

---

### Task 11: Verificación de la tanda 1 (sondas) y STATUS

**Files:**
- Modify: `docs/STATUS.md` (sección nueva "2026-10-xx: notas vinculadas, tanda 1")
- Sonda fuera del repositorio (scratchpad), según `docs/WPF_PROBES.md`, con base de datos y carpeta temporales.

- [ ] **Step 1: Sonda sin ratón** (no mueve nada: llama al coordinador por código). Comprueba y guarda capturas:
  1. `OpenLinkedFiles([a.md, b.txt])` → dos notas nuevas con la marca; la ventana de `b` abierta.
  2. Escribir en la ventana (asignando texto al `TextBody` y llamando a `FlushPending`), esperar a la cola → el
     archivo tiene el cambio y el resto de líneas idénticas byte a byte.
  3. Cambiar el archivo desde la sonda → en ≤ 4 s la ventana muestra el texto nuevo, sin marcarse editada.
  4. Cambiar a la vez ventana (sin vaciar) y archivo → nota "⚠ Conflicto: …" creada, archivo intacto, aviso.
  5. Renombrar la carpeta → franja "Archivo no disponible", cuerpo de solo lectura; devolver el nombre → la
     franja desaparece sola.
  6. Renombrar el archivo dentro de su carpeta → la nota sigue la ruta nueva.
  7. Archivar y restaurar la nota → al restaurar se relee el archivo cambiado mientras tanto.
  8. Con "ocultar tareas hechas" activado, una tarea hecha en la nota vinculada no se borra sola.
  9. Proteger desactivado con su tooltip, en la ventana y en el menú de la pestaña.
- [ ] **Step 2: Sonda de arrastrar** — **mueve el ratón: avisar al usuario y esperar su "ok"**. Arrastra dos archivos
  del Explorador al dock y comprueba que se abren.
- [ ] **Step 3: Smoke test** — **avisar y esperar "ok"**: `dotnet run --project tests/Aldune.Ui.SmokeTests -c Debug`,
  salida a un fichero y contar PASS/FAIL sin volver a lanzarlo.
- [ ] **Step 4: STATUS** — sección con lo hecho, decisiones tomadas al implementar y menores pendientes.
- [ ] **Step 5: Commit**

```bash
git add docs/STATUS.md
git commit -m "Docs: estado tras la tanda 1 de las notas vinculadas"
```

---

## Tanda 2

### Task 12: "Abrir con → Aldune": argumentos, buzón entre instancias e instalador

Decisión de implementación: en vez de una tubería con nombre, la segunda instancia deja las rutas en un
**buzón** (`%LOCALAPPDATA%\Aldune\inbox\<guid>.txt`, una ruta por línea) y avisa con el evento que ya existe.
Hace lo mismo que la tubería de la spec con menos piezas (sin hilo servidor ni protocolo) y reutiliza el aviso
de `SingleInstance`.

**Files:**
- Create: `src/Aldune.Core/OpenRequestInbox.cs`
- Test: `tests/Aldune.Core.Tests/OpenRequestInboxTests.cs`
- Modify: `src/Aldune/App.xaml.cs` (`OnStartup`: argumentos; `ListenForActivation`)
- Modify: `installer/Aldune.iss` (`[Registry]`, `ChangesAssociations=yes` en `[Setup]`)

**Interfaces:**
- Produces: `static class OpenRequestInbox`: `void Post(string inboxDirectory, IEnumerable<string> paths)`,
  `IReadOnlyList<string> Drain(string inboxDirectory)`.

- [ ] **Step 1: Failing tests**

```csharp
using Xunit;

namespace Aldune.Core.Tests;

public class OpenRequestInboxTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-inbox-{Guid.NewGuid():N}");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    [Fact]
    public void PostedPaths_AreDrainedOnceInOrder()
    {
        OpenRequestInbox.Post(_dir, [@"C:\a.md", @"Z:\apuntes\b.txt"]);
        OpenRequestInbox.Post(_dir, [@"C:\c.md"]);

        Assert.Equal(new[] { @"C:\a.md", @"Z:\apuntes\b.txt", @"C:\c.md" }, OpenRequestInbox.Drain(_dir));
        Assert.Empty(OpenRequestInbox.Drain(_dir));
    }

    [Fact]
    public void DrainingAMissingInbox_IsEmpty() => Assert.Empty(OpenRequestInbox.Drain(_dir));

    [Fact]
    public void BlankLines_AreIgnored()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "x.txt"), "\r\nC:\\a.md\r\n\r\n");
        Assert.Equal(new[] { @"C:\a.md" }, OpenRequestInbox.Drain(_dir));
    }
}
```

- [ ] **Step 2: Run** → no compila.
- [ ] **Step 3: Implement**

```csharp
namespace Aldune.Core;

/// <summary>
/// Rutas que una segunda Aldune ("Abrir con" en el Explorador) deja para la que ya está en marcha. La segunda
/// escribe aquí y avisa con el evento de <c>SingleInstance</c>; la primera vacía el buzón al recibir el aviso.
/// Un archivo por envío, nombrado por fecha para conservar el orden.
/// </summary>
public static class OpenRequestInbox
{
    public static void Post(string inboxDirectory, IEnumerable<string> paths)
    {
        Directory.CreateDirectory(inboxDirectory);
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.txt";
        var temporary = Path.Combine(inboxDirectory, name + ".part");
        File.WriteAllLines(temporary, paths);
        // Renombrar al final: quien vacía el buzón nunca lee un envío a medio escribir.
        File.Move(temporary, Path.Combine(inboxDirectory, name));
    }

    public static IReadOnlyList<string> Drain(string inboxDirectory)
    {
        if (!Directory.Exists(inboxDirectory)) return [];
        var paths = new List<string>();
        foreach (var file in Directory.GetFiles(inboxDirectory, "*.txt").Order(StringComparer.Ordinal))
        {
            try
            {
                paths.AddRange(File.ReadAllLines(file).Where(line => !string.IsNullOrWhiteSpace(line)));
                File.Delete(file);
            }
            catch (IOException) { }   // otro proceso lo tiene: se leerá en el siguiente aviso
        }
        return paths;
    }
}
```

- [ ] **Step 4: Run** → PASS (3).
- [ ] **Step 5: `App.xaml.cs`**

`ResolveAppDataDirectory()` ya es `static`. Antes de `SingleInstance.TryAcquire()`:

```csharp
        // "Abrir con → Aldune" o `aldune.exe "<ruta>"`: archivos que abrir como notas vinculadas.
        var filesToOpen = e.Args.Where(arg => !arg.StartsWith('-') && File.Exists(arg)).ToList();
        var inbox = Path.Combine(ResolveAppDataDirectory(), "inbox");
        if (filesToOpen.Count > 0) OpenRequestInbox.Post(inbox, filesToOpen);
```

(Si hay otra instancia, `TryAcquire` la avisa y esta sale; la primera vacía el buzón. Si esta es la primera, el
buzón se vacía al terminar de arrancar.) Cambiar `ListenForActivation` y añadir el vaciado inicial:

```csharp
        _singleInstance.ListenForActivation(() => Dispatcher.BeginInvoke(() =>
        {
            var pending = OpenRequestInbox.Drain(Path.Combine(appDataDir, "inbox"));
            if (pending.Count > 0) _coordinator?.OpenLinkedFiles(pending, null);
            else _coordinator?.OpenNotesManager();
        }));
        var startupFiles = OpenRequestInbox.Drain(Path.Combine(appDataDir, "inbox"));
        if (startupFiles.Count > 0) Dispatcher.BeginInvoke(() => coordinator.OpenLinkedFiles(startupFiles, null));
```

Cuidado con `ResolveAppDataDirectory()`: migra la carpeta antigua la primera vez; llamarlo dos veces es seguro (la
segunda ya encuentra la nueva). Si se ve frágil, guardar el resultado de la primera llamada y reutilizarlo.

- [ ] **Step 6: Instalador** — en `[Setup]`: `ChangesAssociations=yes`. En `[Registry]`:

```ini
; "Abrir con → Aldune" para .md, .markdown y .txt, sin hacerse el programa predeterminado (spec, decisión 6).
Root: HKCU; Subkey: "Software\Classes\Aldune.LinkedNote"; ValueType: string; ValueName: ""; ValueData: "Aldune"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Aldune.LinkedNote\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Aldune.LinkedNote\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "Aldune.LinkedNote"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "Aldune.LinkedNote"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "Aldune.LinkedNote"; ValueData: ""; Flags: uninsdeletevalue
```

Verificar con `./scripts/build-installer.ps1` que compila. Probar la asociación en una máquina o usuario de
prueba solo si el usuario lo autoriza (toca su registro); si no, dejarlo anotado como "sin probar" en STATUS.

- [ ] **Step 7: Build, tests y commit**

```bash
git add src/Aldune.Core/OpenRequestInbox.cs tests/Aldune.Core.Tests/OpenRequestInboxTests.cs src/Aldune/App.xaml.cs installer/Aldune.iss
git commit -m "Abrir con Aldune: argumentos, buzon entre instancias y OpenWithProgids en el instalador"
```

---

### Task 13: Menú de la nota vinculada y "Guardar como archivo vinculado…"

**Files:**
- Modify: `src/Aldune.Core/LinkedFileService.cs` (`SaveAs`, `SetSync`)
- Test: `tests/Aldune.Core.Tests/LinkedFileServiceTests.cs`
- Modify: `src/Aldune/Windowing/NoteWindow.xaml` y `.xaml.cs` (`ActionsPopup`)
- Modify: `src/Aldune/Windowing/AppCoordinator.cs`
- Modify: `src/Aldune/Resources/Strings.cs`

**Interfaces:**
- Produces: `LinkResult LinkedFileService.SaveAs(Guid noteId, string path)`; `void LinkedFileService.SetSync(Guid noteId, bool enabled)`.

- [ ] **Step 1: Failing tests** (en `LinkedFileServiceTests`)

```csharp
    [Fact]
    public void SaveAs_WritesTheFileAndLinksTheNote()
    {
        var note = _repository.Create("Título\r\n☐ a\r\n→ b", "#EBD38B", "primary");
        var path = Path.Combine(_dir, "nueva.md");

        var result = _sut.SaveAs(note.Id, path);

        Assert.Equal(new LinkResult(LinkOutcome.Linked, note.Id), result);
        Assert.Equal("Título\r\n- [ ] a\r\n- b", File.ReadAllText(path));
        Assert.Equal(ReconcileOutcome.Unchanged, _sut.Reconcile(note.Id).Outcome);
    }

    [Fact]
    public void SaveAs_NeverOverwritesAnExistingFile()
    {
        var note = _repository.Create("x", "#EBD38B", "primary");
        var path = WriteFile("existe.md", "del usuario");

        Assert.Equal(LinkOutcome.Unreadable, _sut.SaveAs(note.Id, path).Outcome);
        Assert.Equal("del usuario", File.ReadAllText(path));
    }

    [Fact]
    public void SetSync_TogglesTheFlag()
    {
        var id = OpenLinked(WriteFile("tema.md", "x"));
        _sut.SetSync(id, true);
        Assert.True(_repository.GetFileLink(id)!.SyncEnabled);
    }
```

(La interfaz abre el diálogo de guardar con `OverwritePrompt = false` y, si el archivo elegido ya existe,
avisa y no hace nada: Aldune no reemplaza archivos del usuario con una nota. `SaveAs` tampoco sobrescribe,
por si se llama desde otro sitio: usa `FileMode.CreateNew`.)

- [ ] **Step 2: Run** → no compila.
- [ ] **Step 3: Implement** (en `LinkedFileService`)

```csharp
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
```

- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Textos** (en la sección de notas vinculadas de `Strings.cs`)

```csharp
    public static string LinkedSyncThisNote => T("Sync this note", "Sincronizar esta nota",
        "Diese Notiz synchronisieren", "Synchroniser cette note", "Sincronizar esta nota");
    public static string LinkedOpenInEditor => T("Open in its editor", "Abrir en su editor",
        "Im zugehörigen Editor öffnen", "Ouvrir dans son éditeur", "Abrir no editor padrão");
    public static string LinkedShowInExplorer => T("Show in File Explorer", "Mostrar en el Explorador",
        "Im Explorer anzeigen", "Afficher dans l'Explorateur", "Mostrar no Explorador");
    public static string LinkedSaveAs => T("Save as linked file…", "Guardar como archivo vinculado…",
        "Als verknüpfte Datei speichern…", "Enregistrer comme fichier lié…", "Salvar como arquivo vinculado…");
    public static string LinkedSaveAsExists(string name) => T(
        $"{name} already exists. Aldune doesn't replace files: choose another name.",
        $"{name} ya existe. Aldune no reemplaza archivos: elige otro nombre.",
        $"{name} existiert bereits. Aldune ersetzt keine Dateien: Wähle einen anderen Namen.",
        $"{name} existe déjà. Aldune ne remplace pas de fichiers : choisissez un autre nom.",
        $"{name} já existe. O Aldune não substitui arquivos: escolha outro nome.");
```

- [ ] **Step 6: Menú de la nota** — en `ActionsPopup` de `NoteWindow.xaml`, cinco entradas nuevas con el estilo de
  las vecinas (nombres: `LinkedSyncCheck` como `CheckBox` con el estilo de casilla del menú si existe, si no un
  `Button` que alterna y muestra "✓"; `OpenInEditorButton`, `ShowInExplorerButton`, `ConvertToNormalButton`,
  `SaveAsLinkedButton`). Al abrir el menú: las cuatro primeras visibles solo si `IsLinked`; `SaveAsLinkedButton`
  solo si `!IsLinked && !_note.IsProtected`. Manejadores:

```csharp
    private void OnLinkedSyncClick(object sender, RoutedEventArgs e)
    {
        var enabled = LinkedSyncCheck.IsChecked == true;
        _coordinator.LinkedFiles.SetSync(_note.Id, enabled);
        UpdateSyncSignal();
    }

    private void OnOpenInEditorClick(object sender, RoutedEventArgs e)
    {
        if (LinkedNoteDisplay.PathOf(_note.Id) is { } path)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OnShowInExplorerClick(object sender, RoutedEventArgs e)
    {
        if (LinkedNoteDisplay.PathOf(_note.Id) is { } path)
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void OnConvertToNormalClick(object sender, RoutedEventArgs e) => _coordinator.ConvertLinkedToNormal(_note.Id, this);

    private void OnSaveAsLinkedClick(object sender, RoutedEventArgs e) => _coordinator.SaveNoteAsLinkedFile(_note.Id, this);
```

`Process.Start` con la ruta puede lanzar `Win32Exception` si no hay programa asociado: capturarla y no hacer nada
más (Windows ya muestra su propio diálogo de "¿Con qué quieres abrirlo?" en la mayoría de casos).

En `AppCoordinator`:

```csharp
    public void SaveNoteAsLinkedFile(Guid noteId, Window owner)
    {
        if (_repository.GetById(noteId) is not { IsProtected: false } note) return;
        if (_openNoteWindows.TryGetValue(noteId, out var window)) window.FlushPending();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Strings.LinkedFileFilter,
            FileName = MarkdownExport.SuggestedFileName(note.Text),
            DefaultExt = ".md",
            OverwritePrompt = false,
        };
        if (dialog.ShowDialog(owner) != true) return;
        if (System.IO.File.Exists(dialog.FileName))
        {
            AppDialog.Show(owner, Strings.LinkedSaveAsExists(System.IO.Path.GetFileName(dialog.FileName)),
                Strings.LinkedSaveAs, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (LinkMessage(LinkedFiles.SaveAs(noteId, dialog.FileName), dialog.FileName) is { } error)
        {
            AppDialog.Show(owner, error, Strings.LinkedSaveAs, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LinkedNoteDisplay.Set(_repository.GetFileLinks());
        RefreshAll();
    }
```

(`MarkdownExport.SuggestedFileName` ya devuelve un nombre con `.md`; si devuelve sin extensión, añadirla.)

- [ ] **Step 7: Build, tests y commit**

```bash
git add src/Aldune.Core/LinkedFileService.cs tests/Aldune.Core.Tests/LinkedFileServiceTests.cs src/Aldune/Windowing/NoteWindow.xaml src/Aldune/Windowing/NoteWindow.xaml.cs src/Aldune/Windowing/AppCoordinator.cs src/Aldune/Resources/Strings.cs
git commit -m "Notas vinculadas: sincronizar por nota, abrir en su editor, mostrar en el Explorador y guardar como archivo vinculado"
```

---

### Task 14: Verificación final, documentación y versión

**Files:**
- Modify: `docs/STATUS.md`, `docs/ROADMAP.md` (§11 hecho; versión **v1.6.0** en la línea de "La ronda actual")
- Modify: `src/Aldune/Aldune.csproj` (`Version` 1.6.0, `AssemblyVersion`/`FileVersion` 1.6.0.0)
- Modify: `AGENTS.md` (una línea en "Reglas del proyecto": "Aldune nunca borra un archivo vinculado; ver la spec de notas vinculadas")

- [ ] **Step 1: Sonda sin ratón de la tanda 2**: `aldune.exe "<ruta>"` con Aldune ya en marcha (segunda instancia)
  abre la nota; "Sincronizar esta nota" cambia la señal de "fuera del alcance" a "pendiente"; "Guardar como
  archivo vinculado…" crea el archivo y la marca aparece; "Convertir en nota normal" quita la marca y deja el
  archivo.
- [ ] **Step 2: Suite completa en Release**: `dotnet build Aldune.slnx -c Release --no-incremental` (4 avisos CA1416)
  y `dotnet test Aldune.slnx -c Release --no-build` → todo PASS.
- [ ] **Step 3: Smoke test** — **avisar y esperar "ok"**; salida a fichero.
- [ ] **Step 4: Revisión independiente** de `MarkdownLink`, `LinkedFileService` y `LinkedFileCoordinator` (Opus):
  pérdida de datos del archivo, carreras entre el hilo de fondo y la interfaz, rutas de red.
- [ ] **Step 5: Docs, versión y commit**

```bash
git add docs/STATUS.md docs/ROADMAP.md src/Aldune/Aldune.csproj AGENTS.md
git commit -m "Notas vinculadas: estado, roadmap y version 1.6.0"
```

Publicar (push, tag `v1.6.0`, servidor de sync) es una acción hacia fuera: **pedir confirmación al usuario** y
seguir `docs/RELEASING.md`.
