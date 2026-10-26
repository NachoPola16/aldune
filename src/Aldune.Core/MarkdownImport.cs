using System.Text.RegularExpressions;

namespace Aldune.Core;

/// <summary>
/// Importar un archivo Markdown como nota normal (sin vínculo con el archivo): es la vuelta de
/// <see cref="MarkdownExport"/>. Las tareas y viñetas pasan a los glifos de Aldune (lo hace
/// <see cref="MarkdownLink"/>, el mismo traductor de las notas vinculadas), el encabezado "# " de la
/// primera línea vuelve a ser el título de la nota y los saltos de línea pasan a CRLF, que es lo que
/// escribe el editor.
/// </summary>
public static partial class MarkdownImport
{
    [GeneratedRegex(@"^# +")]
    private static partial Regex TitleMarker();

    [GeneratedRegex(@"\r\n|\n")]
    private static partial Regex NewLine();

    public static string ToNoteText(string markdown)
    {
        var text = NewLine().Replace(MarkdownLink.ToNoteText(markdown), "\r\n");
        return TitleMarker().Replace(text, "", 1);
    }

    /// <summary>Lee los bytes de un archivo (mismos límites y codificaciones que un archivo vinculado) y los pasa a texto de nota.</summary>
    public static (string? Text, LinkedFileRejection Rejection) Read(byte[] bytes)
    {
        var (content, rejection) = LinkedFileFormat.Decode(bytes);
        return content is null ? (null, rejection) : (ToNoteText(content.Text), LinkedFileRejection.None);
    }
}
