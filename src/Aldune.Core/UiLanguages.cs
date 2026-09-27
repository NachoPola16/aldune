namespace Aldune.Core;

/// <summary>Un idioma de la interfaz: código de <c>AppSettings.Language</c>, su nombre en su propio
/// idioma (así se nombra en cualquier selector) y la cultura para fechas.</summary>
public sealed record UiLanguage(string Code, string Name, string Culture);

public static class UiLanguages
{
    /// <summary>
    /// En el orden del selector de Ajustes: alfabético por el nombre de cada idioma en sí mismo, que
    /// es como lo busca quien no entiende la interfaz actual ("Español" siempre en la E). Un idioma
    /// nuevo se añade en su sitio alfabético. El portugués es el de Brasil: es con diferencia el
    /// mayor número de hablantes y de usuarios de Windows.
    /// </summary>
    public static IReadOnlyList<UiLanguage> All { get; } =
    [
        new("de", "Deutsch", "de-DE"),
        new("en", "English", "en-US"),
        new("es", "Español", "es-ES"),
        new("fr", "Français", "fr-FR"),
        new("pt", "Português (Brasil)", "pt-BR"),
    ];

    /// <summary>La elección guardada si es un idioma conocido; si no (nunca se eligió, o viene de
    /// una versión posterior con más idiomas), el de Windows si está; si tampoco, inglés.</summary>
    public static string Resolve(string? chosen, string windowsTwoLetterName) =>
        IsKnown(chosen) ? chosen! : IsKnown(windowsTwoLetterName) ? windowsTwoLetterName : "en";

    public static string CultureFor(string code) =>
        All.FirstOrDefault(l => l.Code == code)?.Culture ?? "en-US";

    private static bool IsKnown(string? code) => code is not null && All.Any(l => l.Code == code);
}
