namespace Aldune.Core;

/// <summary>
/// Tab y Mayús+Tab dentro de una nota. Sobre una tarea (<see cref="TaskLines"/>) o una viñeta
/// (<see cref="BulletLines"/>) suben o bajan la línea entera un nivel. No hace falta un modelo de
/// árbol aparte: el nivel ya es solo la sangría que ambas clases siempre toleraron delante del glifo.
///
/// En texto libre, Tab escribe esa misma sangría donde esté el cursor, y Mayús+Tab quita un nivel del
/// principio de la línea. Antes el texto libre recibía una tabulación literal, que WPF dibuja más
/// ancha que cuatro espacios, así que la prosa y las listas acababan en columnas distintas.
/// </summary>
public static class ListIndent
{
    private const string Unit = "    "; // 4 espacios por nivel

    /// <summary>Añade un nivel de sangría a la línea de <paramref name="caret"/> si es una tarea o una
    /// viñeta; si no, inserta ese nivel en el propio cursor.</summary>
    public static (string Text, int Caret) Indent(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineText.Start(text, caret);
        int end = LineText.End(text, caret);
        var line = text[start..end];

        if (!IsListLine(line))
            return (text.Insert(caret, Unit), caret + Unit.Length);

        var indented = Unit + line;
        return (string.Concat(text.AsSpan(0, start), indented, text.AsSpan(end)), caret + Unit.Length);
    }

    /// <summary>
    /// Quita un nivel de sangría (o lo que haya, si no llega a un nivel completo) de la línea de
    /// <paramref name="caret"/>, sea de lista o de texto libre. Una tabulación literal al principio,
    /// de las notas escritas antes de unificar la sangría, cuenta como un nivel. Si no hay sangría,
    /// devuelve el texto sin cambios: Mayús+Tab dentro de una nota no debe saltar a otro control.
    /// </summary>
    public static (string Text, int Caret) Outdent(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int start = LineText.Start(text, caret);
        int end = LineText.End(text, caret);
        var line = text[start..end];

        int removable = 0;
        if (line.StartsWith('\t'))
        {
            removable = 1;
        }
        else
        {
            while (removable < Unit.Length && removable < line.Length && line[removable] == ' ') removable++;
        }

        if (removable == 0) return (text, caret);

        var outdented = line[removable..];
        int caretInLine = Math.Max(caret - start - removable, 0);
        return (string.Concat(text.AsSpan(0, start), outdented, text.AsSpan(end)), start + caretInLine);
    }

    /// <summary>
    /// Tab con una selección de varias líneas: un nivel más en cada línea que toca la selección, y la
    /// selección sigue cubriendo esas líneas enteras, para poder repetir el gesto. Una selección que
    /// acaba justo al principio de una línea (lo que deja Mayús+Abajo) no incluye esa línea, y las
    /// líneas en blanco se quedan vacías.
    /// </summary>
    public static (string Text, int SelectionStart, int SelectionLength) IndentLines(
        string text, int selectionStart, int selectionLength) =>
        MapLines(text, selectionStart, selectionLength,
            line => line.TrimEnd('\r').Length == 0 ? line : Unit + line);

    /// <summary>Mayús+Tab con varias líneas: un nivel menos en cada una (ver <see cref="Outdent"/>).</summary>
    public static (string Text, int SelectionStart, int SelectionLength) OutdentLines(
        string text, int selectionStart, int selectionLength) =>
        MapLines(text, selectionStart, selectionLength, line => Outdent(line, 0).Text);

    private static (string Text, int SelectionStart, int SelectionLength) MapLines(
        string text, int selectionStart, int selectionLength, Func<string, string> map)
    {
        selectionStart = Math.Clamp(selectionStart, 0, text.Length);
        int selectionEnd = Math.Clamp(selectionStart + selectionLength, selectionStart, text.Length);
        if (selectionEnd > selectionStart && selectionEnd == LineText.Start(text, selectionEnd))
            selectionEnd--;

        int start = LineText.Start(text, selectionStart);
        int end = LineText.End(text, selectionEnd);

        // Se parte por '\n' y el '\r' de un salto de Windows se queda al final de su línea, fuera
        // de la sangría, que siempre va al principio.
        var lines = text[start..end].Split('\n').Select(map);
        var replaced = string.Join('\n', lines);
        var result = string.Concat(text.AsSpan(0, start), replaced, text.AsSpan(end));
        return (result, start, replaced.Length);
    }

    /// <summary>¿Es tarea o viñeta la línea de <paramref name="caret"/>? En texto libre el llamante
    /// puede insertar la sangría por su cuenta para conservar el historial de deshacer del control.</summary>
    public static bool IsListLineAt(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        return IsListLine(text[LineText.Start(text, caret)..LineText.End(text, caret)]);
    }

    /// <summary>La sangría de un nivel, para quien la inserta por su cuenta.</summary>
    public const string IndentUnit = Unit;

    private static bool IsListLine(string line) => TaskLines.IsTaskLine(line) || BulletLines.IsBulletLine(line);
}
