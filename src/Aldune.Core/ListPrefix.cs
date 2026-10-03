namespace Aldune.Core;

/// <summary>
/// Lo común a los dos prefijos de lista (<see cref="TaskLines"/> y <see cref="BulletLines"/>): ambos son
/// un glifo y un espacio detrás de la sangría.
/// </summary>
public static class ListPrefix
{
    /// <summary>
    /// Retroceso con el cursor justo detrás del prefijo (o entre el glifo y su espacio): quita el
    /// prefijo entero de una vez y deja la línea como texto normal, con su sangría. El retroceso
    /// nativo borraba solo el espacio y dejaba el glifo pegado al texto, que ya no cuenta como lista
    /// y había que borrar con una segunda pulsación. Devuelve <c>null</c> en cualquier otro sitio,
    /// para que el retroceso haga lo de siempre.
    /// </summary>
    public static (string Text, int Caret)? RemoveOnBackspace(string text, int caret)
    {
        if (caret <= 0 || caret > text.Length) return null;

        int start = LineText.Start(text, caret);
        var line = text[start..LineText.End(text, caret)];

        int glyph = TaskLines.GlyphIndex(line);
        if (glyph < 0) glyph = BulletLines.GlyphIndex(line);
        if (glyph < 0) return null;

        int caretInLine = caret - start;
        if (caretInLine != glyph + 1 && caretInLine != glyph + 2) return null;

        return (text.Remove(start + glyph, 2), start + glyph);
    }

    /// <summary>Cuántos espacios o tabuladores hay al principio de <paramref name="line"/>.</summary>
    internal static int IndentLength(string line)
    {
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        return i;
    }

    /// <summary>Mete un espacio detrás del glifo que está en <paramref name="glyphAt"/> (posición en el
    /// texto completo), moviendo el cursor con el texto si estaba detrás.</summary>
    internal static (string Text, int Caret) InsertSpaceAfterGlyph(string text, int caret, int glyphAt) =>
        (text.Insert(glyphAt + 1, " "), caret > glyphAt ? caret + 1 : caret);
}
