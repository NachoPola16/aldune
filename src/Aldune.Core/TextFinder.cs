namespace Aldune.Core;

/// <summary>
/// Búsqueda dentro de UN texto (Ctrl+F en una nota), distinta de <see cref="NoteSearch"/>, que filtra
/// notas. Compara sin distinguir mayúsculas y de forma ordinal: así cada resultado mide exactamente
/// lo que la consulta, y la ventana puede seleccionarlo sin calcular longitudes por cultura.
/// </summary>
public static class TextFinder
{
    /// <summary>Dónde empieza cada coincidencia, sin solaparse. La consulta no se recorta: un espacio
    /// al final puede ser lo que se busca. Vacía no encuentra nada (no hay barra "mostrar todo").</summary>
    public static IReadOnlyList<int> FindAll(string text, string query)
    {
        var found = new List<int>();
        if (query.Length == 0) return found;

        int at = 0;
        while ((at = text.IndexOf(query, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            found.Add(at);
            at += query.Length;
        }
        return found;
    }

    /// <summary>
    /// Índice (en <paramref name="starts"/>) del resultado al que ir desde <paramref name="position"/>:
    /// hacia delante, el primero que empieza en el cursor o después; hacia atrás, el último que empieza
    /// antes. Al acabar da la vuelta. -1 si no hay resultados.
    /// </summary>
    public static int NextIndex(IReadOnlyList<int> starts, int position, bool forward)
    {
        if (starts.Count == 0) return -1;

        if (forward)
        {
            for (int i = 0; i < starts.Count; i++)
                if (starts[i] >= position) return i;
            return 0;
        }

        for (int i = starts.Count - 1; i >= 0; i--)
            if (starts[i] < position) return i;
        return starts.Count - 1;
    }
}
