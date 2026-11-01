namespace Aldune.Core;

/// <summary>
/// Cambiar de vista del dock girando la rueda (sobre el engranaje, o sobre las pestañas cuando caben todas). El orden es
/// Activas → cada etiqueta → Archivadas → Papelera → Todas → Activas, pero solo paran las vistas que tienen algo que enseñar: una etiqueta,
/// Archivadas o Papelera vacías se saltan, y «Todas» solo para si hay algo archivado o en la papelera (si no, sería igual que Activas).
/// El usuario puede además quitar Archivadas, Papelera o Todas de la rueda (<see cref="Stops"/>): siguen estando en el menú del engranaje
/// y se pueden elegir expresamente. Desde una vista que no es parada (una que se eligió con un clic, o una etiqueta que se quedó sin notas)
/// la rueda sigue en el orden de siempre hasta la siguiente parada. Lógica pura: la ventana solo aplica el resultado.
/// </summary>
public static class DockViewCycle
{
    /// <summary>Cuántas notas hay en cada vista; <paramref name="Tags"/> son las etiquetas con sus notas ACTIVAS.</summary>
    public sealed record Counts(int Active, int Archived, int Trashed, IReadOnlyDictionary<string, int> Tags, IReadOnlyList<string>? CustomTagOrder = null);

    /// <summary>Qué vistas entran en la rueda cuando tienen notas: Archivadas, Papelera, Todas y las etiquetas. Activas siempre entra.</summary>
    public sealed record Stops(bool Archived = true, bool Trash = true, bool All = true, bool Tags = true);

    public static (DockViewKind View, string? Tag) Next(DockViewKind view, string? tag, Counts counts, int direction, Stops? stops = null)
    {
        stops ??= new Stops();
        var order = Canonical(counts);
        int index = IndexOf(order, view, tag);
        if (index < 0)
        {
            // Una etiqueta que ya no existe: se sigue desde el hueco que dejaría entre las etiquetas, es decir, desde Activas hacia delante
            // o desde Archivadas hacia atrás.
            index = order.FindIndex(s => s.View == DockViewKind.Archived);
            index = direction >= 0 ? index - 1 : index;
        }

        int step = direction >= 0 ? 1 : -1;
        for (int n = 1; n <= order.Count; n++)
        {
            var candidate = order[((index + step * n) % order.Count + order.Count) % order.Count];
            if (IsStop(candidate, counts, stops)) return candidate;
        }

        return (DockViewKind.Active, null);
    }

    /// <summary>El rótulo breve de la vista a la que se ha llegado (la etiqueta con su #).</summary>
    public static string Label(DockViewKind view, string? tag, string active, string archived, string trash, string all) => view switch
    {
        DockViewKind.Tag when !string.IsNullOrWhiteSpace(tag) => "#" + tag,
        DockViewKind.Archived => archived,
        DockViewKind.Trashed => trash,
        DockViewKind.All => all,
        _ => active
    };

    /// <summary>Todas las vistas en el orden de la rueda, vacías incluidas (para saber por dónde seguir desde una que no es parada).</summary>
    private static List<(DockViewKind View, string? Tag)> Canonical(Counts counts)
    {
        var order = new List<(DockViewKind, string?)> { (DockViewKind.Active, null) };
        // The tags in the order the user sees them (alphabetical unless they set their own, see TagOrder).
        foreach (var name in TagOrder.Apply(counts.Tags.Keys, counts.CustomTagOrder ?? []))
            order.Add((DockViewKind.Tag, name));
        order.Add((DockViewKind.Archived, null));
        order.Add((DockViewKind.Trashed, null));
        order.Add((DockViewKind.All, null));
        return order;
    }

    private static bool IsStop((DockViewKind View, string? Tag) step, Counts counts, Stops stops) => step.View switch
    {
        DockViewKind.Tag => stops.Tags && counts.Tags.TryGetValue(step.Tag!, out var n) && n > 0,
        DockViewKind.Archived => stops.Archived && counts.Archived > 0,
        DockViewKind.Trashed => stops.Trash && counts.Trashed > 0,
        DockViewKind.All => stops.All && counts.Archived + counts.Trashed > 0,
        _ => true // Active: always, so the wheel can always come back
    };

    private static int IndexOf(List<(DockViewKind View, string? Tag)> steps, DockViewKind view, string? tag) =>
        steps.FindIndex(s => s.View == view && (view != DockViewKind.Tag || string.Equals(s.Tag, tag, StringComparison.OrdinalIgnoreCase)));
}
