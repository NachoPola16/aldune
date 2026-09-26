using System.Text.RegularExpressions;

namespace Aldune.Core;

/// <summary>
/// Acciones sobre la lista de tareas entera de una nota — lo que pide una lista que se reutiliza, como
/// la de la compra: marcar desde el teclado, desmarcarlo todo para la semana siguiente, quitar lo hecho,
/// mandar lo hecho al final y pegar una lista escrita en Markdown.
///
/// Todo sobre el texto plano, igual que <see cref="TaskLines"/> (ver allí por qué las casillas no son
/// controles). No hay un "tipo de nota lista": sería un campo nuevo en el formato de sync, que las
/// versiones anteriores rechazarían, para algo que ya se reconoce en el propio texto.
/// </summary>
public static partial class TaskLists
{
    /// <summary>Índice del glifo de la tarea en la línea de <paramref name="caret"/>, o null si esa línea
    /// no es una tarea. Es lo que marca Ctrl+Enter.</summary>
    public static int? CheckboxOnLine(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = TaskLines.LineStart(text, caret);
        int glyph = TaskLines.GlyphIndex(TaskLines.LineContaining(text, caret));
        return glyph >= 0 ? start + glyph : null;
    }

    public static bool HasChecked(string text) => TaskLines.Count(text).Done > 0;

    /// <summary>Hay tareas y están todas hechas.</summary>
    public static bool IsAllDone(string text)
    {
        var (done, total) = TaskLines.Count(text);
        return total > 0 && done == total;
    }

    /// <summary>Desmarca todas las tareas. Cambia un glifo por otro de la misma longitud, así que las
    /// posiciones no se mueven.</summary>
    public static TextEdit UncheckAll(string text)
    {
        var lines = TextEdit.Lines.Parse(text);
        var chars = text.ToCharArray();
        for (int i = 0; i < lines.Count; i++)
        {
            if (!TaskLines.IsChecked(lines.Contents[i])) continue;
            chars[lines.Starts[i] + TaskLines.GlyphIndex(lines.Contents[i])] = TaskLines.Unchecked;
        }
        return TextEdit.InPlace(text, new string(chars));
    }

    /// <summary>Quita las líneas de las tareas hechas.</summary>
    public static TextEdit RemoveChecked(string text)
    {
        var lines = TextEdit.Lines.Parse(text);
        var removed = Enumerable.Range(0, lines.Count).Where(i => TaskLines.IsChecked(lines.Contents[i])).ToHashSet();
        return TextEdit.RemoveLines(text, removed);
    }

    /// <summary>
    /// Tras marcar o desmarcar la tarea de <paramref name="glyphIndex"/>, la coloca en su sitio dentro de
    /// su lista: la recién hecha al final (debajo de las ya hechas, así el orden de lo hecho es el orden
    /// en que se hizo) y la recién desmarcada justo encima de la primera hecha.
    ///
    /// La lista son las tareas seguidas con la misma sangría. Si alguna tiene subtareas debajo no se mueve
    /// nada: mover una madre o saltar por encima de ella dejaría sus hijas colgando de otra tarea.
    /// </summary>
    public static TextEdit SettleToggled(string text, int glyphIndex)
    {
        var lines = TextEdit.Lines.Parse(text);
        int line = LineOf(lines, glyphIndex);
        var content = lines.Contents[line];
        if (!TaskLines.IsTaskLine(content)) return TextEdit.Unchanged(text);

        var indent = Indent(content);
        bool SameList(int i) => TaskLines.IsTaskLine(lines.Contents[i]) && Indent(lines.Contents[i]) == indent;

        int first = line, last = line;
        while (first > 0 && SameList(first - 1)) first--;
        while (last < lines.Count - 1 && SameList(last + 1)) last++;

        for (int i = first; i <= last; i++)
        {
            if (HasChildren(lines, i)) return TextEdit.Unchanged(text);
        }

        int target;
        if (TaskLines.IsChecked(content))
        {
            target = last;
        }
        else
        {
            int firstChecked = Enumerable.Range(first, line - first).FirstOrDefault(i => TaskLines.IsChecked(lines.Contents[i]), -1);
            if (firstChecked < 0) return TextEdit.Unchanged(text);
            target = firstChecked;
        }
        if (target == line) return TextEdit.Unchanged(text);

        var order = Enumerable.Range(0, lines.Count).Where(i => i != line).ToList();
        order.Insert(target, line);
        return TextEdit.Reorder(text, order);
    }

    /// <summary>
    /// Convierte la sintaxis de tareas de Markdown (<c>- [ ]</c>, <c>- [x]</c>, también con <c>*</c>,
    /// <c>+</c> o sin viñeta) en casillas, al principio de línea y conservando la sangría. Es lo que
    /// exporta Aldune (ver MarkdownExport), así que una lista exportada vuelve a entrar tal cual.
    /// </summary>
    public static string ConvertMarkdownTasks(string text) =>
        MarkdownTask().Replace(text, match =>
            match.Groups["indent"].Value
            + (match.Groups["mark"].Value == " " ? TaskLines.Unchecked : TaskLines.Checked)
            + " ");

    /// <summary>El texto de las primeras <paramref name="max"/> tareas pendientes con contenido, para el
    /// aviso de un recordatorio.</summary>
    public static IReadOnlyList<string> PendingTasks(string text, int max)
    {
        var pending = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            if (pending.Count >= max) break;
            var line = raw.TrimEnd('\r');
            int glyph = TaskLines.GlyphIndex(line);
            if (glyph < 0 || TaskLines.IsChecked(line)) continue;
            var content = line[(glyph + TaskLines.PrefixLength(line, glyph))..].Trim();
            if (content.Length > 0) pending.Add(content);
        }
        return pending;
    }

    private static int LineOf(TextEdit.Lines lines, int index)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (index >= lines.Starts[i]) return i;
        }
        return 0;
    }

    private static string Indent(string line)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        return line[..i];
    }

    /// <summary>La línea siguiente tiene contenido y más sangría: son hijas de esta.</summary>
    private static bool HasChildren(TextEdit.Lines lines, int i) =>
        i + 1 < lines.Count
        && lines.Contents[i + 1].Trim().Length > 0
        && Indent(lines.Contents[i + 1]).Length > Indent(lines.Contents[i]).Length;

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?:[-*+][ \t]+)?\[(?<mark>[ xX])\][ \t]?", RegexOptions.Multiline)]
    private static partial Regex MarkdownTask();
}
