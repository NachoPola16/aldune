namespace Aldune.Core;

/// <summary>Lo que pasa al elegir un aspecto en Ajustes.</summary>
public static class AppearanceChoice
{
    /// <summary>
    /// Cambia el aspecto. Esquinas y señal vuelven a «lo que diga el aspecto»: quien elige bash espera
    /// verlo como en las maquetas, y puede cambiar las dos después. Devuelve el tema de notas que el
    /// aspecto propone, o null si no propone ninguno o ya es el activo; el tema no se cambia aquí (spec:
    /// se pregunta, nunca se cambia sin preguntar).
    /// </summary>
    public static string? Select(AppSettings settings, AppearanceMode mode)
    {
        settings.Appearance = mode;
        settings.SquareCorners = null;
        settings.ShowSyncSignal = null;

        var suggested = AspectCatalog.For(mode)?.SuggestedThemeId;
        var active = NoteThemes.Resolve(settings.ActiveThemeId, settings.CustomThemes).Id;
        return suggested is null || string.Equals(suggested, active, StringComparison.Ordinal) ? null : suggested;
    }
}
