namespace Aldune.Core;

/// <summary>
/// Cambiar de vista del dock girando la rueda (sobre el engranaje, o sobre las pestañas cuando caben todas). El orden es
/// Activas → cada etiqueta con notas → Archivadas → Papelera → Todas → Activas. Solo se saltan las etiquetas sin notas: Archivadas, Papelera y
/// Todas son paradas fijas aunque estén vacías (si no, la rueda no las alcanzaba y solo se podían elegir con el menú del engranaje). Lógica pura: la ventana solo aplica el resultado.
/// </summary>
public static class DockViewCycle
{
    /// <summary>Cuántas notas hay en cada vista; <paramref name="Tags"/> son las etiquetas con sus notas ACTIVAS.</summary>
    public sealed record Counts(int Active, int Archived, int Trashed, IReadOnlyDictionary<string, int> Tags, IReadOnlyList<string>? CustomTagOrder = null);

    public static (DockViewKind View, string? Tag) Next(DockViewKind view, string? tag, Counts counts, int direction)
    {
        var steps = Steps(counts);
        int index = IndexOf(steps, view, tag);
        if (index < 0)
        {
            // Una etiqueta que ya no tiene notas: se sigue desde el hueco que dejaría entre las etiquetas, es decir, desde Activas hacia delante
            // o desde Archivadas hacia atrás.
            index = steps.FindIndex(s => s.View == DockViewKind.Archived);
            index = index < 0 ? 0 : index;
            index = direction >= 0 ? index - 1 : index;
        }
        int next = ((index + (direction >= 0 ? 1 : -1)) % steps.Count + steps.Count) % steps.Count;
        return steps[next];
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

    private static List<(DockViewKind View, string? Tag)> Steps(Counts counts)
    {
        var steps = new List<(DockViewKind, string?)> { (DockViewKind.Active, null) };
        // The tags in the order the user sees them (alphabetical unless they set their own, see TagOrder).
        foreach (var name in TagOrder.Apply(counts.Tags.Keys, counts.CustomTagOrder ?? []))
            if (counts.Tags[name] > 0) steps.Add((DockViewKind.Tag, name));
        steps.Add((DockViewKind.Archived, null));
        steps.Add((DockViewKind.Trashed, null));
        steps.Add((DockViewKind.All, null));
        return steps;
    }

    private static int IndexOf(List<(DockViewKind View, string? Tag)> steps, DockViewKind view, string? tag) =>
        steps.FindIndex(s => s.View == view && (view != DockViewKind.Tag || string.Equals(s.Tag, tag, StringComparison.OrdinalIgnoreCase)));
}
