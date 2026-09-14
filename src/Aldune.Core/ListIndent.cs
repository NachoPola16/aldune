namespace Aldune.Core;

/// <summary>
/// Sube o baja de nivel una línea de tarea (<see cref="TaskLines"/>) o viñeta
/// (<see cref="BulletLines"/>) con Tab/Mayús+Tab. No hace falta un modelo de árbol aparte: el nivel
/// ya es solo la sangría que ambas clases siempre toleraron delante del glifo — esto solo añade la
/// forma de generarla desde el teclado.
///
/// Devuelve <c>null</c> cuando la línea no es ni tarea ni viñeta, para que quien llama (<c>NoteWindow</c>)
/// deje pasar el Tab normal (una tabulación literal, vía <c>AcceptsTab</c>) en vez de indentar texto
/// suelto — indentar prosa no tiene el mismo significado que indentar un punto de una lista.
/// </summary>
public static class ListIndent
{
    private const string Unit = "    "; // 4 espacios por nivel

    /// <summary>Añade un nivel de sangría a la línea de <paramref name="caret"/>, o <c>null</c> si no
    /// es una tarea ni una viñeta.</summary>
    public static (string Text, int Caret)? Indent(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineText.Start(text, caret);
        int end = LineText.End(text, caret);
        var line = text[start..end];

        if (!IsListLine(line)) return null;

        var indented = Unit + line;
        return (string.Concat(text.AsSpan(0, start), indented, text.AsSpan(end)), caret + Unit.Length);
    }

    /// <summary>
    /// Quita un nivel de sangría (o lo que haya, si no llega a un nivel completo) de la línea de
    /// <paramref name="caret"/>, o <c>null</c> si no es una tarea ni una viñeta. Si ya está en el nivel
    /// raíz, devuelve el texto sin cambios — sigue siendo una línea de lista, así que Mayús+Tab no debe
    /// dejar pasar la navegación de foco hacia atrás.
    /// </summary>
    public static (string Text, int Caret)? Outdent(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineText.Start(text, caret);
        int end = LineText.End(text, caret);
        var line = text[start..end];

        if (!IsListLine(line)) return null;

        int removable = 0;
        while (removable < Unit.Length && removable < line.Length && line[removable] == ' ') removable++;

        if (removable == 0) return (text, caret);

        var outdented = line[removable..];
        int caretInLine = Math.Max(caret - start - removable, 0);
        return (string.Concat(text.AsSpan(0, start), outdented, text.AsSpan(end)), start + caretInLine);
    }

    private static bool IsListLine(string line) => TaskLines.IsTaskLine(line) || BulletLines.IsBulletLine(line);
}
