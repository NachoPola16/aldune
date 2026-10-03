namespace Aldune.Core;

/// <summary>
/// El color con el que se pinta una nota, que no siempre es el que tiene guardado: con "mismo color
/// en todas las notas" se ven todas del color elegido. Solo cambia la presentación; la nota conserva
/// su color, así que desactivarlo lo devuelve todo y la sincronización no ve cambios.
/// </summary>
public static class NoteDisplayColor
{
    /// <summary>¿Hay un color único válido? Uno inválido (settings.json editado a mano) cuenta como apagado.</summary>
    public static bool IsActive(string? uniform) =>
        uniform is not null && uniform.Length == 7 && OklchColor.TryFromHex(uniform, out _);

    public static string Resolve(string color, string? uniform) =>
        IsActive(uniform) ? uniform!.ToUpperInvariant() : color;
}
