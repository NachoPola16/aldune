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

    // Marcador seguido de espacio (así "**negrita**" y "---" no cuentan) y, opcional, la casilla: "[ ]" de
    // Markdown o el glifo tal cual ("- ☐ tarea", como en PENDIENTES.md): ambas son tareas, no "→ ☐".
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>[-*+]) (?:\[(?<mark>[ xX])\](?: |$)|(?<glyph>[☐☑☒])(?: |$))?")]
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
        bool glyphTasks = UsesGlyphTasks(original);
        var builder = new StringBuilder(noteText.Length + 64);
        for (int j = 0; j < edited.Count; j++)
        {
            int i = match[j];
            if (i < 0 && spare.TryGetValue(editedContents[j], out var queue) && queue.Count > 0) i = queue.Dequeue();

            builder.Append(i >= 0 ? original[i].Content
                : literal[j] ? editedContents[j]
                : ToFileLine(editedContents[j], marker, glyphTasks));

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
        if (match.Groups["glyph"].Success) return indent + match.Groups["glyph"].Value + " " + rest;
        if (!match.Groups["mark"].Success) return indent + BulletLines.Prefix + rest;
        return indent + (match.Groups["mark"].Value == " " ? TaskLines.Unchecked : TaskLines.Checked) + " " + rest;
    }

    private static string ToFileLine(string line, char bulletMarker, bool glyphTasks)
    {
        int task = TaskLines.GlyphIndex(line);
        if (task >= 0 && glyphTasks) return line[..task] + "- " + line[task..];
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

    // Un archivo que escribe sus tareas con el glifo ("- ☐") recibe las nuevas igual; si mezcla, gana la
    // forma más usada y, en empate, la de Markdown ("- [ ]"), que es lo que lee cualquier otro programa.
    private static bool UsesGlyphTasks(List<Line> lines)
    {
        var literal = LiteralLines(lines.Select(line => line.Content).ToList());
        int glyph = 0, brackets = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (literal[i]) continue;
            var match = ListItem().Match(lines[i].Content);
            if (match.Groups["glyph"].Success) glyph++;
            else if (match.Groups["mark"].Success) brackets++;
        }
        return glyph > brackets;
    }

    private static char DominantBulletMarker(List<Line> lines)
    {
        var literal = LiteralLines(lines.Select(line => line.Content).ToList());
        var counts = new Dictionary<char, int> { ['-'] = 0, ['*'] = 0, ['+'] = 0 };
        for (int i = 0; i < lines.Count; i++)
        {
            if (literal[i]) continue;
            var match = ListItem().Match(lines[i].Content);
            if (match.Success && !match.Groups["mark"].Success && !match.Groups["glyph"].Success) counts[match.Groups["marker"].Value[0]]++;
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
