namespace Aldune.Core;

/// <summary>
/// Unión de las dos versiones de una nota en conflicto. No hay versión base guardada, así que no cabe una
/// fusión de tres vías (saber qué lado borró y qué lado añadió): se hace lo seguro, que es no perder nada de
/// ninguna. La versión activa se queda entera y en su orden; cada línea que solo tiene la otra se inserta
/// detrás de la última línea compartida que la precede. Una línea editada queda por duplicado (la vieja y la
/// nueva) y el usuario borra la que sobra.
/// </summary>
public static class ConflictMerge
{
    public static string Combine(string activeText, string otherText)
    {
        var merged = activeText.Replace("\r\n", "\n").Split('\n').ToList();
        var other = otherText.Replace("\r\n", "\n").Split('\n');

        var known = new HashSet<string>(merged.Where(line => line.Trim().Length > 0).Select(line => line.TrimEnd()));
        int anchor = -1; // posición en `merged` tras la que va lo siguiente que falte; -1 = al principio
        foreach (var line in other)
        {
            if (line.Trim().Length == 0) continue; // las líneas en blanco son maquetación, no contenido

            var key = line.TrimEnd();
            if (known.Contains(key))
            {
                // Compartida: pasa a ser el ancla, pero solo si está por delante de la actual (el orden de la activa manda).
                int at = merged.FindIndex(anchor + 1, candidate => candidate.TrimEnd() == key);
                if (at >= 0) anchor = at;
                continue;
            }

            merged.Insert(++anchor, line);
            known.Add(key);
        }
        return string.Join("\n", merged);
    }
}
