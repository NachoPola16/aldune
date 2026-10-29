namespace Aldune.Core;

/// <summary>
/// El «nombre visible» de una nota protegida: un texto en claro y opcional que sustituye a «Nota protegida» en el dock y en
/// Gestionar notas. Es un campo aparte del contenido cifrado, a propósito: sacarlo del texto filtraría sin querer lo que se cree oculto.
/// </summary>
public static class ProtectedTitleRules
{
    public const int MaxLength = 60;

    /// <summary>Una sola línea, sin espacios sobrantes y con tope. Vacío (o solo espacios) es «sin nombre».</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var oneLine = string.Join(' ', raw.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim()).Where(part => part.Length > 0));
        if (oneLine.Length > MaxLength) oneLine = oneLine[..MaxLength].TrimEnd();
        return oneLine.Length == 0 ? null : oneLine;
    }

    /// <summary>El nombre que se puede enseñar, o null si hay que enseñar «Nota protegida». Solo cuenta en notas protegidas.</summary>
    public static string? Visible(Note note) => note.IsProtected ? Normalize(note.ProtectedTitle) : null;
}
