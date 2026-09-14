namespace Aldune.Core;

/// <summary>
/// Listas con viñeta dentro del texto de una nota — hermana de <see cref="TaskLines"/>, mismo patrón
/// (prefijo de texto plano, sin estado propio ni <c>RichTextBox</c>, ver esa clase para el porqué).
/// A diferencia de una tarea, una línea con viñeta no se "marca": no hay estado hecho/pendiente, solo
/// presencia del prefijo.
///
/// El glifo es una flecha (<c>→</c>), no un guion: un guion normal es algo que alguien podría escribir
/// de verdad al empezar una frase, y el sistema lo detectaría como lista sin querer. La flecha, como
/// <c>☐</c>, es un carácter que nadie teclea por accidente — mismo criterio que los glifos de tarea,
/// verificado en <c>Segoe UI Variable Text</c> con un render aislado antes de elegirlo (misma altura
/// de línea que el resto de glifos candidatos, sin caer a fuente sustituta).
/// </summary>
public static class BulletLines
{
    /// <summary>U+2192 RIGHTWARDS ARROW.</summary>
    public const char Glyph = '→';

    /// <summary>El prefijo que se inserta al convertir una línea en viñeta.</summary>
    public const string Prefix = "→ ";

    /// <summary>
    /// Índice del glifo de viñeta dentro de <paramref name="line"/>, o -1 si esa línea no es una
    /// viñeta. Mismo criterio que <see cref="TaskLines.GlyphIndex"/>: se permite sangría delante, y
    /// no se exige un espacio detrás del glifo.
    /// </summary>
    public static int GlyphIndex(string line)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;

        if (i >= line.Length) return -1;
        if (line[i] != Glyph) return -1;

        return i;
    }

    public static bool IsBulletLine(string line) => GlyphIndex(line) >= 0;

    /// <summary>Cuántos caracteres ocupa el prefijo de viñeta a partir de <paramref name="glyphIndex"/>
    /// — ver <see cref="TaskLines.PrefixLength"/>, mismo cálculo.</summary>
    public static int PrefixLength(string line, int glyphIndex) =>
        glyphIndex + 1 < line.Length && line[glyphIndex + 1] == ' ' ? 2 : 1;

    /// <summary>Si <paramref name="line"/> es una viñeta sin contenido real después del prefijo — ver
    /// <see cref="TaskLines.IsEmptyTaskLine"/>, mismo uso (Enter termina la lista en vez de
    /// continuarla).</summary>
    public static bool IsEmptyBulletLine(string line)
    {
        int glyph = GlyphIndex(line);
        return glyph >= 0 && line[(glyph + PrefixLength(line, glyph))..].Trim().Length == 0;
    }

    /// <summary>
    /// Convierte en viñeta la línea donde está el cursor, o le quita el prefijo si ya lo era. Si la
    /// línea ya era una tarea (<see cref="TaskLines"/>), la convierte en viñeta en vez de apilar los
    /// dos prefijos — mismo criterio simétrico que <see cref="TaskLines.ToggleTaskLineAt"/> con una
    /// viñeta. El cursor se desplaza con el texto para que siga señalando la misma palabra.
    /// </summary>
    public static (string Text, int Caret) ToggleBulletLineAt(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineText.Start(text, caret);
        int end = LineText.End(text, caret);
        var line = text[start..end];

        int glyph = GlyphIndex(line);
        if (glyph >= 0)
        {
            int prefixLength = PrefixLength(line, glyph);
            var stripped = line.Remove(glyph, prefixLength);
            int caretInLine = Math.Max(caret - start - prefixLength, glyph);
            return (string.Concat(text.AsSpan(0, start), stripped, text.AsSpan(end)), start + caretInLine);
        }

        int taskGlyph = TaskLines.GlyphIndex(line);
        if (taskGlyph >= 0)
        {
            int taskPrefixLength = TaskLines.PrefixLength(line, taskGlyph);
            var replaced = line.Remove(taskGlyph, taskPrefixLength).Insert(taskGlyph, Prefix);
            int caretInLine = caret - start >= taskGlyph + taskPrefixLength
                ? caret - start - taskPrefixLength + Prefix.Length
                : caret - start;
            return (string.Concat(text.AsSpan(0, start), replaced, text.AsSpan(end)), start + caretInLine);
        }

        int indent = 0;
        while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;

        var prefixed = line.Insert(indent, Prefix);
        int newCaret = caret >= start + indent ? caret + Prefix.Length : caret;
        return (string.Concat(text.AsSpan(0, start), prefixed, text.AsSpan(end)), newCaret);
    }

    /// <summary>Qué hacer cuando se pulsa Enter con el cursor en <paramref name="caret"/> — ver
    /// <see cref="TaskLines.EnterContinuation"/>, mismo comportamiento: continúa la lista, o la
    /// termina si la viñeta actual está vacía.</summary>
    public static (string Text, int Caret)? EnterContinuation(string text, int caret, string newLine = "\r\n")
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineText.Start(text, caret);
        int end = LineText.End(text, caret);
        var line = text[start..end];

        int glyph = GlyphIndex(line);
        if (glyph < 0) return null;

        if (caret != end) return null;

        if (IsEmptyBulletLine(line))
        {
            var cleared = line[..glyph].TrimEnd();
            return (string.Concat(text.AsSpan(0, start), cleared, text.AsSpan(end)), start + cleared.Length);
        }

        var indent = line[..glyph];
        var inserted = newLine + indent + Prefix;
        return (string.Concat(text.AsSpan(0, caret), inserted, text.AsSpan(caret)), caret + inserted.Length);
    }
}
