using System.Security.Cryptography;
using System.Text;

namespace Aldune.Core;

/// <summary>Unidad del plazo de <see cref="AppSettings.AutoHideCompletedTasksDelayValue"/>.</summary>
public enum TaskDelayUnit
{
    Minutes,
    Hours,
    Days,
    Weeks
}

/// <summary>
/// Cuándo se marcó cada tarea como hecha, para poder borrarla sola pasado un tiempo (ajuste
/// opcional y desactivado por defecto — ver <see cref="AppSettings.AutoHideCompletedTasks"/>).
///
/// Cada tarea se identifica por un hash de su contenido (el texto después del glifo), no por su
/// posición en la nota: la posición cambia en cuanto se edita cualquier línea de alrededor, y el
/// hash sigue apuntando a la misma tarea aunque la nota crezca o encoja por otro sitio. El hash se
/// calcula sobre el texto sin el glifo, así que desmarcar y volver a marcar la misma tarea más
/// tarde reinicia el reloj en vez de arrastrar el momento en que se marcó la primera vez.
///
/// Dos tareas con texto idéntico en la misma nota comparten hash y por tanto también el reloj —
/// igual que <see cref="NoteOrdering"/> acepta posiciones duplicadas, se acepta aquí por la misma
/// razón: es una colisión rara y sin consecuencia grave (como mucho, una tarea desaparece un poco
/// antes o después de lo esperado).
/// </summary>
public static class TaskCompletion
{
    /// <summary>
    /// Hash estable del contenido de una tarea (sin el glifo ni el espacio que lo sigue). No vale
    /// <c>string.GetHashCode()</c>: en .NET varía a propósito entre ejecuciones del proceso, y este
    /// hash tiene que sobrevivir a cerrar y volver a abrir la app.
    /// </summary>
    public static string HashLine(string line)
    {
        int glyph = TaskLines.GlyphIndex(line);
        // PrefixLength y no un +2 fijo: una tarea sin espacio detrás del glifo (ver
        // TaskLines.PrefixLength) solo tiene 1 carácter de prefijo, y quitar 2 a ciegas se comería
        // la primera letra real de la tarea, cambiando el hash cada vez que se mide.
        var content = glyph >= 0 ? line[(glyph + TaskLines.PrefixLength(line, glyph))..] : line;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16]; // de sobra para no chocar entre tareas de una misma nota
    }

    /// <summary>Resultado de podar un texto: el texto que queda, si cambió algo, y los hashes cuyo registro
    /// de <c>TaskCompletion</c> ya no corresponde a ninguna tarea marcada (para que el llamante los borre).</summary>
    public readonly record struct PruneResult(string Text, bool Changed, IReadOnlyCollection<string> HashesToClear);

    /// <summary>
    /// Quita de <paramref name="text"/> las líneas de tarea marcadas como hechas cuyo plazo ya
    /// venció según <paramref name="completedAt"/> (hash → cuándo se marcó), y calcula qué
    /// registros de <paramref name="completedAt"/> ya no corresponden a ninguna tarea marcada en el
    /// texto resultante — porque se borró, se editó (cambia el hash) o se desmarcó a mano por otro
    /// camino distinto del toggle normal. Una tarea marcada sin registro en <paramref name="completedAt"/>
    /// no se toca: solo se borra lo que se sabe con certeza que ha vencido.
    /// </summary>
    public static PruneResult Prune(
        string text,
        IReadOnlyDictionary<string, DateTimeOffset> completedAt,
        DateTimeOffset now,
        TimeSpan delay)
    {
        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        bool changed = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (TaskLines.IsTaskLine(line) && TaskLines.IsChecked(line)
                && completedAt.TryGetValue(HashLine(line), out var completedTime)
                && now - completedTime >= delay)
            {
                changed = true;
                continue;
            }
            kept.Add(rawLine);
        }

        var prunedText = changed ? JoinLines(kept) : text;

        var stillChecked = new HashSet<string>();
        foreach (var rawLine in prunedText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (TaskLines.IsTaskLine(line) && TaskLines.IsChecked(line))
            {
                stillChecked.Add(HashLine(line));
            }
        }

        var toClear = completedAt.Keys.Where(hash => !stillChecked.Contains(hash)).ToList();

        return new PruneResult(prunedText, changed, toClear);
    }

    /// <summary>
    /// Reconstruye el texto a partir de los segmentos que quedan tras <c>text.Split('\n')</c>. Un
    /// simple <c>string.Join('\n', kept)</c> no basta cuando se borra la ÚLTIMA línea de una nota en
    /// CRLF: el `\r` de esa línea pertenece al separador de la línea *anterior*, no a la borrada, así
    /// que se queda colgando al final sin su `\n` de pareja (p. ej. "algo\r\n☒ hecho" pierde la
    /// última línea y, sin este arreglo, quedaría "algo\r" en vez de "algo").
    /// </summary>
    private static string JoinLines(IReadOnlyList<string> kept)
    {
        var joined = string.Join('\n', kept);
        return joined.EndsWith('\r') ? joined[..^1] : joined;
    }
}
