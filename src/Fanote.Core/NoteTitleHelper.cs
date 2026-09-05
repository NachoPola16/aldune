namespace Fanote.Core;

public static class NoteTitleHelper
{
    public const string PlaceholderTitle = "Nueva nota";

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
}
