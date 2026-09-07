namespace Fanote.Core;

/// <summary>
/// Casillas de tarea dentro del texto de una nota.
///
/// Son texto plano, no un tipo aparte: una línea que empieza por <c>"☐ "</c> o <c>"☑ "</c> es una
/// tarea, y nada más. Esa decisión no es pereza — el cuerpo de la nota es un <c>TextBox</c> plano a
/// propósito (la spec v1 descartó texto enriquecido, ver docs/STATUS.md), así que una casilla que
/// fuera un control de verdad exigiría un <c>RichTextBox</c> y arrastraría con él el formato, el
/// portapapeles con estilos y un modelo de guardado distinto. Como prefijo de texto, en cambio, la
/// casilla se cifra, se busca, se exporta y se ve en la pestaña del dock sin código nuevo en
/// ninguno de esos sitios.
///
/// Todo aquí es puro: recibe el texto y la posición del cursor, devuelve texto y posición nuevos.
/// Quien lo llama (<c>NoteWindow</c>) solo traduce clics y teclas.
/// </summary>
public static class TaskLines
{
    /// <summary>U+2610 BALLOT BOX.</summary>
    public const char Unchecked = '☐';

    /// <summary>
    /// U+2612 BALLOT BOX WITH X — el que se escribe al marcar.
    ///
    /// No es U+2611 (☑, la caja con el tick), que sería el candidato obvio: rasterizando los dos en
    /// la fuente real de la nota se ve que ☑ no está en <c>Segoe UI Variable Text</c> y cae en una
    /// fuente sustituta que lo dibuja como un cuadrado negro macizo — más pesado que el ☐ fino, más
    /// ancho (el texto de la tarea se desplazaba al marcarla) y el único negro puro de una ventana
    /// que evita el negro absoluto a propósito. ☒ sale de la misma fuente que ☐: mismo peso, misma
    /// anchura, misma caja.
    /// </summary>
    public const char Checked = '☒';

    /// <summary>
    /// También cuenta como marcada al leer, aunque nunca se escriba: es la que llega pegada desde
    /// otras apps de tareas y desde Markdown ya renderizado.
    /// </summary>
    public const char CheckedAlternate = '☑';

    /// <summary>El prefijo que se inserta al convertir una línea en tarea.</summary>
    public const string Prefix = "☐ ";

    private const string DefaultNewLine = "\r\n";

    /// <summary>
    /// Índice del glifo de casilla dentro de <paramref name="line"/>, o -1 si esa línea no es una
    /// tarea. Se permite sangría delante (para listas indentadas) y se exige un espacio detrás: así
    /// un ☐ suelto escrito en mitad de una frase no convierte la línea en tarea sin querer.
    /// </summary>
    public static int GlyphIndex(string line)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;

        if (i >= line.Length) return -1;
        if (!IsBoxGlyph(line[i])) return -1;
        if (i + 1 >= line.Length || line[i + 1] != ' ') return -1;

        return i;
    }

    public static bool IsTaskLine(string line) => GlyphIndex(line) >= 0;

    public static bool IsChecked(string line)
    {
        int glyph = GlyphIndex(line);
        return glyph >= 0 && IsCheckedGlyph(line[glyph]);
    }

    private static bool IsBoxGlyph(char c) => c == Unchecked || IsCheckedGlyph(c);

    private static bool IsCheckedGlyph(char c) => c == Checked || c == CheckedAlternate;

    /// <summary>
    /// Marca o desmarca la casilla que hay en <paramref name="index"/>. Devuelve <c>null</c> si ahí
    /// no hay una casilla de verdad — el llamante usa ese null para dejar pasar el clic como un clic
    /// normal de colocar el cursor.
    /// </summary>
    public static string? ToggleCheckboxAt(string text, int index)
    {
        if (index < 0 || index >= text.Length) return null;
        if (!IsBoxGlyph(text[index])) return null;

        int start = LineStart(text, index);
        int end = LineEnd(text, index);
        var line = text[start..end];

        // Solo cuenta si ese glifo es el prefijo de SU línea, no uno escrito más adelante.
        if (GlyphIndex(line) != index - start) return null;

        char flipped = IsCheckedGlyph(text[index]) ? Unchecked : Checked;
        return string.Concat(text.AsSpan(0, index), flipped.ToString(), text.AsSpan(index + 1));
    }

    /// <summary>
    /// Convierte en tarea la línea donde está el cursor, o le quita el prefijo si ya lo era. El
    /// cursor se desplaza con el texto para que siga señalando la misma palabra.
    /// </summary>
    public static (string Text, int Caret) ToggleTaskLineAt(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineStart(text, caret);
        int end = LineEnd(text, caret);
        var line = text[start..end];

        int glyph = GlyphIndex(line);
        if (glyph >= 0)
        {
            // Quitar: fuera el glifo y su espacio.
            var stripped = line.Remove(glyph, 2);
            int caretInLine = Math.Max(caret - start - 2, glyph);
            return (string.Concat(text.AsSpan(0, start), stripped, text.AsSpan(end)), start + caretInLine);
        }

        // Poner: detrás de la sangría que ya tuviera la línea.
        int indent = 0;
        while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;

        var prefixed = line.Insert(indent, Prefix);
        int newCaret = caret >= start + indent ? caret + Prefix.Length : caret;
        return (string.Concat(text.AsSpan(0, start), prefixed, text.AsSpan(end)), newCaret);
    }

    /// <summary>
    /// Qué hacer cuando se pulsa Enter con el cursor en <paramref name="caret"/>:
    /// <list type="bullet">
    /// <item>En una tarea con contenido, la lista continúa: nueva línea ya con su casilla.</item>
    /// <item>En una tarea vacía, Enter la termina — se le quita el prefijo y se queda una línea en
    /// blanco, que es como se sale de una lista en cualquier editor.</item>
    /// <item>En cualquier otro sitio devuelve <c>null</c>: Enter hace lo de siempre.</item>
    /// </list>
    /// </summary>
    public static (string Text, int Caret)? EnterContinuation(string text, int caret, string newLine = DefaultNewLine)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineStart(text, caret);
        int end = LineEnd(text, caret);
        var line = text[start..end];

        int glyph = GlyphIndex(line);
        if (glyph < 0) return null;

        // Solo continúa desde el final de la línea: pulsar Enter en mitad de una tarea la parte en
        // dos, que es lo que hace un editor normal y lo que el usuario espera.
        if (caret != end) return null;

        var rest = line[(glyph + 2)..];
        if (rest.Trim().Length == 0)
        {
            var cleared = line[..glyph].TrimEnd();
            return (string.Concat(text.AsSpan(0, start), cleared, text.AsSpan(end)), start + cleared.Length);
        }

        var indent = line[..glyph];
        var inserted = newLine + indent + Prefix;
        return (string.Concat(text.AsSpan(0, caret), inserted, text.AsSpan(caret)), caret + inserted.Length);
    }

    /// <summary>Cuántas tareas hay y cuántas están hechas. Para el resumen de la pestaña del dock.</summary>
    public static (int Done, int Total) Count(string text)
    {
        int done = 0, total = 0;
        foreach (var line in text.Split('\n'))
        {
            var clean = line.TrimEnd('\r');
            if (!IsTaskLine(clean)) continue;
            total++;
            if (IsChecked(clean)) done++;
        }
        return (done, total);
    }

    private static int LineStart(string text, int index)
    {
        for (int i = Math.Min(index, text.Length) - 1; i >= 0; i--)
        {
            if (text[i] == '\n') return i + 1;
        }
        return 0;
    }

    private static int LineEnd(string text, int index)
    {
        for (int i = Math.Max(index, 0); i < text.Length; i++)
        {
            if (text[i] == '\r' || text[i] == '\n') return i;
        }
        return text.Length;
    }
}
