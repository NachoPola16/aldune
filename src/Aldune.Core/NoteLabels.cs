using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aldune.Core;

/// <summary>Partes de la línea de prompt de la nota (piel bash), cada una con su color.</summary>
public sealed record PromptParts(string User, string Path, string Command);

/// <summary>
/// Títulos tal como se pintan según la piel. Solo presentación: el texto de la nota no cambia
/// (<c>hoy/</c> o <c>CH1 HOY</c> se pintan, no se guardan).
/// </summary>
public static class NoteLabels
{
    // Escapes explícitos y no los caracteres literales: son invisibles en el editor.
    private const char HairSpace = '\u200A';
    private const char NoBreakSpace = '\u00A0';

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static string Dock(string title, SkinTitleAdornment adornment, int channel, CultureInfo culture)
    {
        title = title.Trim();
        if (title.Length == 0) return string.Empty;

        return adornment switch
        {
            SkinTitleAdornment.Channel => $"{NoteChannels.Number(channel)} {Whitespace.Replace(title.ToUpper(culture), "_")}",
            SkinTitleAdornment.Folder => Folder(title, culture) + "/",
            SkinTitleAdornment.Uppercase => title.ToUpper(culture),
            _ => Spaced(title.ToUpper(culture).Replace(' ', NoBreakSpace)),
        };
    }

    /// <summary>El título como nombre de carpeta de terminal: minúsculas y guiones bajos.</summary>
    public static string Folder(string title, CultureInfo culture) =>
        Whitespace.Replace(title.Trim().ToLower(culture), "_");

    /// <summary>
    /// Las partes del prompt. <c>Command</c> es solo el comando (<c>cat hoy</c>, vacío sin título): va en su
    /// propia línea, para que un título largo pueda partirse sin perder el usuario y la ruta.
    /// </summary>
    public static PromptParts Prompt(string userName, string notesFolder, string title, CultureInfo culture)
    {
        var user = Whitespace.Replace(userName.Trim().ToLower(culture), "_");
        if (user.Length == 0) user = "user";
        var folder = Folder(title, culture);
        return new PromptParts($"{user}@aldune", $"~/{notesFolder}", folder.Length == 0 ? "" : $"cat {folder}");
    }

    // Mayúsculas espaciadas de la pestaña de siempre (antes en NoteTabLabelConverter).
    private static string Spaced(string text)
    {
        var builder = new StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0) builder.Append(HairSpace);
            builder.Append(text[i]);
        }
        return builder.ToString();
    }
}
