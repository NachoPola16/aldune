namespace Aldune.Core;

/// <summary>
/// El orden manual de las notas en el mazo.
///
/// La posición es un <c>double</c>, no un índice entero, **a propósito**: así mover una nota entre
/// otras dos es escribir un solo número (el punto medio de sus vecinas) en vez de renumerar la lista
/// entera en cada arrastre. Se guarda en su propia tabla (<c>NoteOrder</c>) y no como columna de
/// <c>Note</c>, igual que <c>NotePlacement</c>: la tabla de notas tiene los datos de verdad del
/// usuario y esta app no tiene sistema de migraciones.
/// </summary>
public static class NoteOrdering
{
    /// <summary>Separación entre posiciones al numerar de cero. Amplia, para que quepan inserciones.</summary>
    public const double Gap = 1024;

    /// <summary>
    /// La posición que le toca a una nota soltada entre <paramref name="before"/> y
    /// <paramref name="after"/> (nulos si se suelta al principio o al final de la lista).
    ///
    /// Devuelve <c>null</c> cuando las dos vecinas están tan juntas que ya no cabe un número entre
    /// ellas — puede pasar tras muchísimas inserciones en el mismo hueco, porque cada una parte el
    /// intervalo por la mitad. El llamante responde renumerando toda la lista, que es barato y pasa
    /// prácticamente nunca.
    /// </summary>
    public static double? Between(double? before, double? after)
    {
        if (before is null && after is null) return 0;
        if (before is null) return after!.Value - Gap;
        if (after is null) return before.Value + Gap;

        double middle = before.Value + (after.Value - before.Value) / 2;

        // Sin sitio: el punto medio coincide con alguno de los extremos por precisión del double.
        if (middle <= before.Value || middle >= after.Value) return null;

        return middle;
    }

    /// <summary>
    /// Posiciones limpias y espaciadas para una lista ya ordenada. Se usa al numerar por primera vez
    /// las notas que aún no tienen orden manual, y al renumerar si <see cref="Between"/> se queda sin
    /// sitio.
    /// </summary>
    public static IReadOnlyList<double> Spaced(int count)
    {
        var positions = new double[Math.Max(count, 0)];
        for (int i = 0; i < positions.Length; i++) positions[i] = i * Gap;
        return positions;
    }

    /// <summary>
    /// Qué fracción de un hueco hay que arrastrar para que la nota cambie de sitio.
    ///
    /// No es 0.5 —el redondeo "natural"— porque las pestañas del abanico **se solapan**: el paso
    /// entre huecos es de 26px mientras la pestaña mide 52, así que redondear al hueco más cercano
    /// significaba que moverla 13px ya la recolocaba. Correcto sobre el papel y desagradable en la
    /// mano: parecía que se movía sola sin haberla puesto sobre ninguna otra. Con 0.75 hay que
    /// arrastrar la mayor parte de un hueco, y un temblor al pulsar no reordena nada.
    /// </summary>
    public const double SlotHysteresis = 0.75;

    /// <summary>
    /// Cuántos huecos se ha desplazado la nota, dado lo que se ha arrastrado en píxeles. Devuelve 0
    /// mientras no se haya arrastrado lo suficiente (ver <see cref="SlotHysteresis"/>), que es lo que
    /// hace que un arrastre corto deje la nota donde estaba.
    /// </summary>
    public static int SlotShift(double delta, double pitch, double hysteresis = SlotHysteresis)
    {
        if (pitch <= 0) return 0;

        double slots = delta / pitch;
        int whole = (int)Math.Truncate(slots);
        double fraction = slots - whole;

        if (fraction >= hysteresis) whole += 1;
        else if (fraction <= -hysteresis) whole -= 1;

        return whole;
    }

    /// <summary>
    /// El índice que ocupará la nota al soltarla: el que tenía más lo que se haya desplazado, acotado
    /// a la lista.
    /// </summary>
    public static int TargetIndex(int originalIndex, double delta, double pitch, int count)
    {
        if (count <= 0) return 0;
        return Math.Clamp(originalIndex + SlotShift(delta, pitch), 0, count - 1);
    }
}
