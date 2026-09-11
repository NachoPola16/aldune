using System.Text;

namespace Fanote.Core;

public enum LineDirection
{
    Up,
    Down
}

/// <summary>
/// Subir o bajar de sitio la línea donde está el cursor, intercambiándola con la de arriba o la de
/// abajo — el atajo estándar de editor (Alt+Arriba/Alt+Abajo) para reordenar sin arrastrar nada.
///
/// Nace de una petición concreta ("mover de sitio las tareas con casilla dentro de una nota"), pero
/// no se limita a líneas de tarea: arrastrar dentro de un <c>TextBox</c> plano habría exigido
/// simular el gesto a mano (detectar el arrastre sin confundirlo con el clic de marcar la casilla,
/// dibujar una línea fantasma, reconstruir el texto al soltar) — bastante más complejo y en tensión
/// con esa misma decisión de no usar texto enriquecido. El atajo de teclado da el mismo resultado
/// (reordenar) intercambiando dos líneas de texto sin más, y sirve igual para cualquier línea: no
/// hay motivo para que solo funcione en una tarea, y restringirlo sería una sorpresa frente a lo que
/// ya hacen otros editores con este mismo atajo.
/// </summary>
public static class LineMovement
{
    /// <summary>
    /// Devuelve el texto con la línea de <paramref name="caret"/> intercambiada por su vecina en
    /// <paramref name="direction"/>, y dónde queda el cursor (mismo desplazamiento dentro de la
    /// línea, que ahora vive en su nueva posición). <c>null</c> si no hay vecina en esa dirección
    /// (ya es la primera o la última línea).
    /// </summary>
    public static (string Text, int Caret)? Move(string text, int caret, LineDirection direction)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var (contents, terminators) = SplitLines(text);

        int lineIndex = contents.Count - 1;
        int lineStart = 0;
        int consumed = 0;
        for (int i = 0; i < contents.Count; i++)
        {
            if (caret <= consumed + contents[i].Length)
            {
                lineIndex = i;
                lineStart = consumed;
                break;
            }
            consumed += contents[i].Length + terminators[i].Length;
        }

        int offsetInLine = caret - lineStart;
        int targetIndex = direction == LineDirection.Up ? lineIndex - 1 : lineIndex + 1;
        if (targetIndex < 0 || targetIndex >= contents.Count) return null;

        // Solo se intercambia el CONTENIDO, nunca los terminadores: cada posición lleva su propio
        // terminador (línea normal -> "\r\n"/"\n", última línea -> "") porque depende de dónde
        // queda esa posición en la lista, no de qué texto se haya movido a vivir ahí. Intercambiar
        // los terminadores junto con el contenido dejaba un "\r" colgando o una línea sin el suyo —
        // el mismo tipo de bug que ya apareció una vez al borrar líneas (ver TaskCompletion.JoinLines).
        (contents[lineIndex], contents[targetIndex]) = (contents[targetIndex], contents[lineIndex]);

        var builder = new StringBuilder();
        for (int i = 0; i < contents.Count; i++)
        {
            builder.Append(contents[i]).Append(terminators[i]);
        }

        int newLineStart = 0;
        for (int i = 0; i < targetIndex; i++)
        {
            newLineStart += contents[i].Length + terminators[i].Length;
        }
        int newCaret = newLineStart + Math.Min(offsetInLine, contents[targetIndex].Length);

        return (builder.ToString(), newCaret);
    }

    /// <summary>Cada línea de <paramref name="text"/> como (contenido, terminador) — "\r\n"/"\n" para
    /// todas menos la última, que lleva "" porque no hay nada detrás. Separar contenido de
    /// terminador es lo que permite mover líneas de sitio sin desordenar los saltos de línea.</summary>
    private static (List<string> Contents, List<string> Terminators) SplitLines(string text)
    {
        var contents = new List<string>();
        var terminators = new List<string>();
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;

            bool hasCr = i > start && text[i - 1] == '\r';
            int contentEnd = hasCr ? i - 1 : i;
            contents.Add(text[start..contentEnd]);
            terminators.Add(hasCr ? "\r\n" : "\n");
            start = i + 1;
        }

        contents.Add(text[start..]);
        terminators.Add("");
        return (contents, terminators);
    }
}
