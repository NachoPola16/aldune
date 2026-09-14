namespace Aldune.Core;

/// <summary>
/// Búsqueda de notas por contenido: no hay índice ni FTS, es un <c>Contains</c> sobre el texto ya
/// descifrado en memoria — a la escala de una lista de notas personal (decenas, no miles) construir
/// algo más no paga su complejidad.
/// </summary>
public static class NoteSearch
{
    /// <summary>
    /// Si <paramref name="noteText"/> contiene <paramref name="query"/>, sin distinguir mayúsculas
    /// ni acentos según la cultura actual. Una consulta vacía o solo espacios coincide con
    /// cualquier texto: es lo que hace que borrar la caja de búsqueda vuelva a enseñarlo todo sin
    /// que la ventana necesite un camino aparte para "sin búsqueda".
    /// </summary>
    public static bool Matches(string noteText, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return true;

        return noteText.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase);
    }
}
