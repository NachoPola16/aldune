namespace Aldune.Core;

/// <summary>Una copia anterior del texto de una nota, tal como estaba en <see cref="SavedAt"/>.</summary>
public sealed record NoteVersion(long Id, Guid NoteId, DateTimeOffset SavedAt, string Text);

/// <summary>
/// Reglas del historial de versiones de una nota. Es LOCAL: no viaja por la sync (el formato no cambia; cada dispositivo guarda las suyas),
/// va cifrado con la misma clave que el texto de la nota, y una nota protegida no guarda nada (su texto en claro no puede quedar en ningún
/// sitio fuera de ella). Se guarda el texto que se va a reemplazar, no el nuevo, para que la primera edición de una nota deje la original.
/// </summary>
public static class NoteVersionRules
{
    /// <summary>Versiones por nota; al pasar de ahí se borran las más antiguas.</summary>
    public const int MaxVersions = 20;

    /// <summary>Mínimo entre dos copias automáticas durante la edición; la copia al cerrar la nota se salta este plazo.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);
}
