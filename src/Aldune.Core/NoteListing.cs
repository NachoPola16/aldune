namespace Aldune.Core;

/// <summary>Orden de la lista de "Gestionar notas". Se escribe como número, como el resto de enums.</summary>
public enum NoteListOrder
{
    /// <summary>El del mazo del dock, tal como llega del repositorio.</summary>
    Dock = 0,
    Newest = 1,
    Oldest = 2,
    Title = 3
}

/// <summary>Cómo se enseña la fecha de una nota: hora si es de hoy, día y mes si es de este año,
/// fecha completa si es anterior (lo mismo que hace cualquier bandeja de correo).</summary>
public enum NoteDateKind
{
    Today,
    ThisYear,
    Older
}

public static class NoteListing
{
    public static IEnumerable<T> Sort<T>(IEnumerable<T> items, Func<T, Note> noteOf, NoteListOrder order) => order switch
    {
        NoteListOrder.Newest => items.OrderByDescending(item => noteOf(item).UpdatedAt),
        NoteListOrder.Oldest => items.OrderBy(item => noteOf(item).UpdatedAt),
        // Las protegidas no enseñan su título: al final, en vez de ordenarlas por un texto que no se ve.
        NoteListOrder.Title => items
            .OrderBy(item => noteOf(item).IsProtected)
            .ThenBy(item => NoteTitleHelper.GetTitle(noteOf(item).Text), StringComparer.CurrentCultureIgnoreCase),
        _ => items
    };

    /// <summary>Por día de calendario de quien mira (el desplazamiento de <paramref name="now"/>), no
    /// por horas transcurridas: una nota de las 23:30 de ayer es "de ayer" aunque haga una hora.</summary>
    public static NoteDateKind DateKind(DateTimeOffset updatedAt, DateTimeOffset now)
    {
        var local = updatedAt.ToOffset(now.Offset);
        if (local.Date == now.Date) return NoteDateKind.Today;
        return local.Year == now.Year ? NoteDateKind.ThisYear : NoteDateKind.Older;
    }
}
