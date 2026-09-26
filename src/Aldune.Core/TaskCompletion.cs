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

    /// <summary>
    /// Resultado de podar un texto: el texto que queda, si cambió algo, los hashes cuyo registro de
    /// <c>TaskCompletion</c> ya no corresponde a ninguna tarea marcada (para que el llamante los borre),
    /// los de las tareas marcadas que aún no tienen registro (para que empiece a contar su plazo) y cómo
    /// trasladar el cursor al texto nuevo (<see cref="MapIndex"/>).
    /// </summary>
    public sealed record PruneResult(
        string Text,
        bool Changed,
        IReadOnlyCollection<string> HashesToClear,
        IReadOnlyCollection<string> HashesToStart,
        TextEdit Edit)
    {
        public int MapIndex(int oldIndex) => Edit.MapIndex(oldIndex);
    }

    /// <summary>
    /// Quita de <paramref name="text"/> las líneas de tarea marcadas como hechas cuyo plazo ya
    /// venció según <paramref name="completedAt"/> (hash → cuándo se marcó), y calcula qué
    /// registros de <paramref name="completedAt"/> ya no corresponden a ninguna tarea marcada en el
    /// texto resultante — porque se borró, se editó (cambia el hash) o se desmarcó a mano por otro
    /// camino distinto del toggle normal.
    ///
    /// Una tarea marcada sin registro no se borra en esta pasada: se devuelve en
    /// <see cref="PruneResult.HashesToStart"/> para que su plazo empiece a contar ahora. Antes se
    /// quedaba sin borrar para siempre (marcada con el ajuste apagado, pegada, escrita a mano,
    /// recuperada con Ctrl+Z o gemela de otra con el mismo texto que se desmarcó).
    /// </summary>
    public static PruneResult Prune(
        string text,
        IReadOnlyDictionary<string, DateTimeOffset> completedAt,
        DateTimeOffset now,
        TimeSpan delay)
    {
        var lines = TextEdit.Lines.Parse(text);
        var removed = new HashSet<int>();
        var stillChecked = new HashSet<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines.Contents[i];
            if (!TaskLines.IsTaskLine(line) || !TaskLines.IsChecked(line)) continue;

            var hash = HashLine(line);
            if (completedAt.TryGetValue(hash, out var completedTime) && now - completedTime >= delay)
            {
                removed.Add(i);
            }
            else
            {
                stillChecked.Add(hash);
            }
        }

        var edit = TextEdit.RemoveLines(text, removed);
        var toClear = completedAt.Keys.Where(hash => !stillChecked.Contains(hash)).ToList();
        var toStart = stillChecked.Where(hash => !completedAt.ContainsKey(hash)).ToList();

        return new PruneResult(edit.Text, edit.Changed, toClear, toStart, edit);
    }
}
