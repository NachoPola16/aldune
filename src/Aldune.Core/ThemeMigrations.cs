namespace Aldune.Core;

/// <summary>
/// Cambios de temas de la 1.5 que tocan datos ya guardados. Se ejecuta al arrancar, antes de abrir
/// ventanas, y es idempotente: cada paso deja una marca o se reconoce por el estado de los ajustes.
/// </summary>
public static class ThemeMigrations
{
    /// <summary>Devuelve true si cambió algo de <paramref name="settings"/> (hay que guardarlos).</summary>
    public static bool Run(AppSettings settings, NotesRepository repository)
    {
        bool changed = false;

        if (!settings.SereneRecolored)
        {
            // Se sincroniza como un cambio de color, y si otro equipo hace lo mismo la sync no lo trata
            // como conflicto (NoteEquivalence). Las notas protegidas también: su color no va cifrado.
            // La fecha avanza solo 1 ms: lo justo para que la versión sea más nueva que la acordada y
            // se publique, sin que la nota pase a "editada hoy" (orden del gestor, Ctrl+Alt+L, plazo
            // de la papelera) ni gane a una edición real hecha en un equipo que aún no ha migrado.
            // Además, dos equipos que migran la misma nota llegan a la misma fecha.
            foreach (var note in repository.GetAllForSync())
            {
                if (NoteThemes.SereneRecolorMap.TryGetValue(note.Color, out var renewed))
                    repository.SetColor(note.Id, renewed, note.UpdatedAt.AddMilliseconds(1));
            }
            if (settings.FixedNoteColor is { } fixedColor &&
                NoteThemes.SereneRecolorMap.TryGetValue(fixedColor, out var renewedFixed))
            {
                settings.FixedNoteColor = renewedFixed;
            }
            settings.SereneRecolored = true;
            changed = true;
        }

        if (settings.ActiveThemeId == NoteThemes.GraphiteId)
        {
            // Grafito se retiró: el mismo aspecto gris con "mismo color en todas las notas". Sus notas
            // guardan su color y no se tocan.
            settings.UniformNoteColor ??= settings.NewNoteTone == NoteTone.Dark ? "#2E2E2E" : "#E8E8E8";
            settings.ActiveThemeId = null;
            changed = true;
        }

        return changed;
    }
}
