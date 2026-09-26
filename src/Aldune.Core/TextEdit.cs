using System.Text;

namespace Aldune.Core;

/// <summary>
/// Un cambio de texto hecho por líneas enteras (borrar o reordenar) y cómo trasladar una posición del
/// texto viejo al nuevo.
///
/// Existe porque la ventana de nota sustituye el texto entero cuando la app lo cambia sola (borrar las
/// tareas vencidas, mandar una hecha al final): sin mapear, el cursor se quedaba en el mismo índice y
/// saltaba tantos caracteres como tuviera la línea borrada o movida — si se estaba escribiendo en ese
/// momento, lo siguiente caía en otro sitio (reproducido con una sonda, 2026-09-26).
/// </summary>
public sealed class TextEdit
{
    // Por cada línea vieja: dónde empezaba, cuánto medía su contenido y en qué línea nueva acabó (-1 si
    // se borró). Por cada línea nueva: dónde empieza y cuánto mide su contenido.
    private readonly int[] _oldStarts;
    private readonly int[] _oldLengths;
    private readonly int[] _newLineOfOld;
    private readonly int[] _newStarts;
    private readonly int[] _newLengths;

    public string Text { get; }
    public bool Changed { get; }

    private TextEdit(string text, bool changed, int[] oldStarts, int[] oldLengths, int[] newLineOfOld,
        int[] newStarts, int[] newLengths)
    {
        Text = text;
        Changed = changed;
        _oldStarts = oldStarts;
        _oldLengths = oldLengths;
        _newLineOfOld = newLineOfOld;
        _newStarts = newStarts;
        _newLengths = newLengths;
    }

    /// <summary>Sin cambios: toda posición se queda donde estaba.</summary>
    public static TextEdit Unchanged(string text) => new(text, false, [], [], [], [], []);

    /// <summary>Un cambio que no mueve ningún carácter de sitio (cambiar un glifo por otro de la misma
    /// longitud): las posiciones valen tal cual.</summary>
    internal static TextEdit InPlace(string original, string text) =>
        new(text, !string.Equals(original, text, StringComparison.Ordinal), [], [], [], [], []);

    /// <summary>
    /// Posición en <see cref="Text"/> que corresponde a <paramref name="oldIndex"/> del texto original:
    /// el mismo carácter si su línea sigue, o el principio de la siguiente que sigue si la suya se borró.
    /// </summary>
    public int MapIndex(int oldIndex)
    {
        if (_oldStarts.Length == 0) return Math.Clamp(oldIndex, 0, Text.Length);

        int line = _oldStarts.Length - 1;
        for (int i = 0; i < _oldStarts.Length - 1; i++)
        {
            if (oldIndex < _oldStarts[i + 1]) { line = i; break; }
        }

        int target = _newLineOfOld[line];
        if (target >= 0)
        {
            int offset = Math.Clamp(oldIndex - _oldStarts[line], 0, _newLengths[target]);
            return Math.Min(_newStarts[target] + offset, Text.Length);
        }

        for (int next = line + 1; next < _newLineOfOld.Length; next++)
        {
            if (_newLineOfOld[next] >= 0) return _newStarts[_newLineOfOld[next]];
        }
        return Text.Length;
    }

    /// <summary>Quita las líneas de <paramref name="removed"/> (índices de línea). Cada línea que queda
    /// conserva su propio salto; la que pasa a ser la última pierde el suyo — así borrar la última línea
    /// de una nota en CRLF no deja un "\r" colgando (el bug que ya arregló TaskCompletion.JoinLines).</summary>
    internal static TextEdit RemoveLines(string text, IReadOnlySet<int> removed)
    {
        var lines = Lines.Parse(text);
        if (removed.Count == 0) return Unchanged(text);

        var kept = Enumerable.Range(0, lines.Count).Where(i => !removed.Contains(i)).ToList();
        var terminators = kept.Select(i => lines.Terminators[i]).ToList();
        if (terminators.Count > 0) terminators[^1] = string.Empty;
        return Build(text, lines, kept, terminators);
    }

    /// <summary>Reordena las líneas según <paramref name="order"/> (índices viejos en su orden nuevo).
    /// Los saltos se quedan en su posición, no viajan con el contenido: la última posición no lleva, y
    /// una línea que se mueve ahí lo pierde (mismo criterio que <see cref="LineMovement"/>).</summary>
    internal static TextEdit Reorder(string text, IReadOnlyList<int> order)
    {
        var lines = Lines.Parse(text);
        return Build(text, lines, order, lines.Terminators);
    }

    private static TextEdit Build(string original, Lines lines, IReadOnlyList<int> order, IReadOnlyList<string> terminators)
    {
        var builder = new StringBuilder(original.Length);
        var newStarts = new int[order.Count];
        var newLengths = new int[order.Count];
        var newLineOfOld = Enumerable.Repeat(-1, lines.Count).ToArray();

        for (int position = 0; position < order.Count; position++)
        {
            int old = order[position];
            newStarts[position] = builder.Length;
            newLengths[position] = lines.Contents[old].Length;
            newLineOfOld[old] = position;
            builder.Append(lines.Contents[old]).Append(terminators[position]);
        }

        var text = builder.ToString();
        return new TextEdit(text, !string.Equals(original, text, StringComparison.Ordinal),
            lines.Starts, lines.Contents.Select(c => c.Length).ToArray(), newLineOfOld, newStarts, newLengths);
    }

    /// <summary>Cada línea como (inicio, contenido sin salto, salto): "\r\n"/"\n" para todas menos la
    /// última, que lleva "".</summary>
    internal sealed class Lines
    {
        public List<string> Contents { get; } = [];
        public List<string> Terminators { get; } = [];
        public int[] Starts { get; private set; } = [];
        public int Count => Contents.Count;

        public static Lines Parse(string text)
        {
            var result = new Lines();
            var starts = new List<int>();
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                bool hasCr = i > start && text[i - 1] == '\r';
                starts.Add(start);
                result.Contents.Add(text[start..(hasCr ? i - 1 : i)]);
                result.Terminators.Add(hasCr ? "\r\n" : "\n");
                start = i + 1;
            }
            starts.Add(start);
            result.Contents.Add(text[start..]);
            result.Terminators.Add(string.Empty);
            result.Starts = starts.ToArray();
            return result;
        }
    }
}
