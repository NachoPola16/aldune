using System.IO;
using System.Linq;
using System.Text;

namespace Aldune.Core;

/// <summary>
/// Convierte el texto de una nota a Markdown, para "Exportar" — ver docs/ROADMAP.md, es una función
/// de confianza tanto como de utilidad ("no te secuestro tus datos").
///
/// Reutiliza <see cref="NoteTitleHelper"/> y <see cref="NoteText"/> tal cual: el título exportado es
/// el mismo que ya se ve en la pestaña del dock y la barra de título, no un cálculo aparte.
/// </summary>
public static class MarkdownExport
{
    private const int MaxFileNameLength = 80;

    /// <summary>
    /// El texto completo de la nota en Markdown: título como encabezado, casillas de tarea
    /// (<see cref="TaskLines"/>) traducidas a la sintaxis de tareas de Markdown
    /// (<c>- [ ]</c>/<c>- [x]</c>), el resto del cuerpo tal cual.
    /// </summary>
    public static string ToMarkdown(string noteText)
    {
        var title = NoteTitleHelper.GetTitle(noteText);
        var (_, body) = NoteText.Split(noteText);

        var heading = "# " + title;
        if (string.IsNullOrEmpty(body)) return heading;

        var convertedLines = body.Split('\n').Select(ConvertLine);
        return heading + "\r\n\r\n" + string.Join("\r\n", convertedLines);
    }

    private static string ConvertLine(string rawLine)
    {
        var line = rawLine.TrimEnd('\r');

        int taskGlyph = TaskLines.GlyphIndex(line);
        if (taskGlyph >= 0)
        {
            int prefixLength = TaskLines.PrefixLength(line, taskGlyph);
            var indent = line[..taskGlyph];
            var content = line[(taskGlyph + prefixLength)..];
            var marker = TaskLines.IsChecked(line) ? "[x]" : "[ ]";
            return $"{indent}- {marker} {content}";
        }

        int bulletGlyph = BulletLines.GlyphIndex(line);
        if (bulletGlyph >= 0)
        {
            int prefixLength = BulletLines.PrefixLength(line, bulletGlyph);
            var indent = line[..bulletGlyph];
            var content = line[(bulletGlyph + prefixLength)..];
            return $"{indent}- {content}";
        }

        return line;
    }

    /// <summary>
    /// Nombre de fichero sugerido a partir del título de la nota: sin caracteres inválidos para
    /// Windows, recortado a una longitud razonable, y con extensión <c>.md</c>.
    /// </summary>
    public static string SuggestedFileName(string noteText)
    {
        var title = NoteTitleHelper.GetTitle(noteText);
        return Sanitize(title) + ".md";
    }

    private static string Sanitize(string title)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            builder.Append(invalidChars.Contains(c) ? '_' : c);
        }

        var result = builder.ToString().Trim();
        if (result.Length > MaxFileNameLength) result = result[..MaxFileNameLength].TrimEnd();
        return result.Length == 0 ? "Nota" : result;
    }
}
