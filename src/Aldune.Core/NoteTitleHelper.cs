namespace Aldune.Core;

public static class NoteTitleHelper
{
    /// <summary>
    /// El título de una nota sin texto todavía. Mutable y no <c>const</c> a propósito: Core no
    /// depende de nada de idiomas (es la capa Win32-libre del proyecto), así que es la capa WPF
    /// quien fija esto una vez al arrancar, según el idioma resuelto de la interfaz — ver
    /// <c>Aldune.Resources.Strings</c> y <c>App.OnStartup</c>.
    /// </summary>
    public static string PlaceholderTitle { get; set; } = "New note";

    public static string GetTitle(string text)
    {
        var firstLine = text.Split('\n')[0].TrimEnd('\r').Trim();
        return string.IsNullOrEmpty(firstLine) ? PlaceholderTitle : firstLine;
    }

    /// <summary>
    /// Lo que va después del título: el resto del texto colapsado en una sola línea, para la
    /// segunda línea de la pestaña del dock.
    ///
    /// Es lo que convierte la pestaña en algo que te dice qué hay dentro de la nota y no solo cómo
    /// se llama. Devuelve cadena vacía cuando no hay nada más, y la pestaña centra entonces su
    /// título en vez de dejar una segunda línea en blanco.
    ///
    /// Colapsa cualquier hilera de espacios, tabuladores y saltos de línea en un solo espacio: en
    /// una línea de ~26 caracteres, respetar los saltos originales gastaría el hueco en huecos.
    /// </summary>
    public static string GetPreview(string text)
    {
        int firstBreak = text.IndexOf('\n');
        if (firstBreak < 0) return string.Empty;

        var rest = text[(firstBreak + 1)..];

        var builder = new System.Text.StringBuilder(rest.Length);
        bool pendingSpace = false;
        foreach (var character in rest)
        {
            // Las casillas se caen de la vista previa: en una línea de ~26 caracteres, repetir
            // "☐" delante de cada tarea gasta el hueco en decir algo que el contador de
            // GetTabPreview ya dice mejor.
            if (character is TaskLines.Unchecked or TaskLines.Checked) continue;

            if (char.IsWhiteSpace(character))
            {
                // Solo se materializa si después viene texto de verdad, así no quedan espacios
                // sueltos al principio ni al final.
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// La segunda línea de la pestaña del dock. Si la nota tiene casillas de tarea, empieza por el
    /// progreso ("1/3 · "): de una lista, lo que se quiere saber desde el mazo es cuánto queda, no
    /// cuál era la primera tarea. Detrás sigue la vista previa de siempre.
    /// </summary>
    public static string GetTabPreview(string text)
    {
        var preview = GetPreview(text);
        var (done, total) = TaskLines.Count(text);

        if (total == 0) return preview;
        return preview.Length == 0 ? $"{done}/{total}" : $"{done}/{total} · {preview}";
    }
}
