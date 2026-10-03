namespace Aldune.Core;

/// <summary>
/// El color con el que se pinta una nota, que no siempre es el que tiene guardado: con "mismo color
/// en todas las notas" se ven todas del color elegido. Solo cambia la presentación; la nota conserva
/// su color, así que desactivarlo lo devuelve todo y la sincronización no ve cambios.
/// </summary>
public static class NoteDisplayColor
{
    public static string Resolve(string color, string? uniform) =>
        uniform is not null && OklchColor.TryFromHex(uniform, out _) && uniform.Length == 7
            ? uniform.ToUpperInvariant()
            : color;
}
