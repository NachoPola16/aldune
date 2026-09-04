namespace Fanote.Core;

/// <summary>
/// Una pieza de la forma recortada. <paramref name="SquareRightSide"/> distingue las pestañas
/// (redondeadas solo por la izquierda, porque su lado derecho va a ras del canto de la pantalla)
/// de las piezas que sí son pastillas o círculos completos: el contenedor de reposo y los botones
/// del footer, que van despegados del canto.
/// </summary>
public readonly record struct RegionPiece(Rect Bounds, double CornerRadius, bool SquareRightSide = true);

/// <summary>
/// Cálculo puro de la forma recortada del dock. Deliberadamente sin combinar ni deduplicar rects:
/// cada pieza se pasa tal cual al interop Win32 (NativeMethods.SetTabFanRegion), que ya sabe
/// unirlas con CombineRgn sin que a estas funciones les importe cómo se combinan a nivel de SO.
/// </summary>
public static class TabRegionShape
{
    /// <summary>Duración total del despliegue, en milisegundos.</summary>
    public const double ExpandDurationMs = 280;

    /// <summary>Duración del barrido de una pestaña concreta.</summary>
    public const double TabSweepMs = 220;

    /// <summary>Desfase entre el arranque de una pestaña y el de la siguiente.</summary>
    public const double TabStaggerMs = 26;

    /// <summary>
    /// Tope del desfase acumulado. Sin él, el escalonado crece sin límite con el número de notas:
    /// el diseño anterior (55ms por índice, más 190ms de espera antes de empezar a mostrar nada)
    /// dejaba la última pestaña sin asentarse hasta ~760ms con 6 notas. Con este tope, la última
    /// pestaña arranca como muy tarde en 130ms y termina en 350ms pase lo que pase.
    /// </summary>
    public const double MaxTotalStaggerMs = 130;

    /// <summary>Retardo de arranque de la pestaña <paramref name="index"/>.</summary>
    public static double StaggerDelayMs(int index) =>
        Math.Min(index * TabStaggerMs, MaxTotalStaggerMs);

    /// <summary>
    /// Curva ease-out quíntica. Las leyes de diseño piden salidas exponenciales (quart/quint/expo);
    /// la QuadraticEase que había es la ease-out más débil que existe y apenas se lee como un
    /// asentamiento, que es justo por lo que alargar la duración a 320ms no arregló la sensación.
    /// </summary>
    public static double EaseOutQuintic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        double inv = 1 - t;
        return 1 - inv * inv * inv * inv * inv;
    }

    /// <summary>
    /// Progreso individual (0..1, ya suavizado) de la pestaña <paramref name="index"/> en el
    /// instante <paramref name="elapsedMs"/> de un despliegue.
    /// </summary>
    public static double TabProgress(int index, double elapsedMs)
    {
        double local = (elapsedMs - StaggerDelayMs(index)) / TabSweepMs;
        return EaseOutQuintic(local);
    }

    /// <summary>
    /// Progreso individual durante un <b>repliegue</b>. No es simplemente 1 menos el de ida: al
    /// replegar interesa que las pestañas se vayan en orden inverso (la más larga primero), lo que
    /// mantiene la sensación de que el abanico se cierra sobre sí mismo en vez de deshacerse por
    /// donde se hizo.
    /// </summary>
    public static double TabCollapseProgress(int index, int noteCount, double elapsedMs)
    {
        int reversed = Math.Max(0, noteCount - 1 - index);
        double local = (elapsedMs - StaggerDelayMs(reversed)) / TabSweepMs;
        return 1 - EaseOutQuintic(local);
    }

    /// <summary>Duración total real de una transición con <paramref name="noteCount"/> notas.</summary>
    public static double TotalDurationMs(int noteCount) =>
        StaggerDelayMs(Math.Max(0, noteCount - 1)) + TabSweepMs;

    /// <summary>
    /// Interpola linealmente entre el valor en reposo y el desplegado con el progreso ya
    /// suavizado. Se usa para las tres cosas que cambian a la vez en una pestaña — ancho, alto y
    /// el desplazamiento vertical que la lleva de su hueco en la tira a su hueco en el abanico.
    /// </summary>
    public static double Sweep(double atRest, double expanded, double progress) =>
        atRest + (expanded - atRest) * Math.Clamp(progress, 0, 1);

    /// <summary>Ancho visible de una pestaña a un progreso dado.</summary>
    public static double SweptWidth(double restWidth, double fullWidth, double progress) =>
        Sweep(restWidth, fullWidth, progress);

    /// <summary>
    /// Progreso del contenedor de reposo, derivado del de la primera pestaña. Se va bastante antes
    /// que ella (multiplicador 2.5): en cuanto el abanico empieza a abrirse, las pestañas se
    /// reparten por toda la longitud de la ventana mientras el contenedor solo cubre la tira corta
    /// de reposo, así que dejarlo puesto lo convertiría en una barra oscura suelta en medio del
    /// abanico. Al replegar pasa lo simétrico: reaparece solo al final, cuando todo ha vuelto.
    /// </summary>
    public static double ContainerProgress(double firstTabProgress) =>
        Math.Clamp(firstTabProgress * 2.5, 0, 1);

    /// <summary>
    /// Construye las piezas de la región a partir de los rects <b>ya calculados</b>.
    ///
    /// <paramref name="restContainer"/> es el contenedor oscuro que agrupa los guiones en reposo.
    /// Sin él, cuatro pasteles claros sueltos sobre un escritorio claro desaparecen — es lo que
    /// hace que la tira se lea como un objeto y no como manchas. Se omite (ancho 0) en cuanto la
    /// transición arranca.
    ///
    /// <paramref name="circleRects"/> son los botones del footer, como círculos completos en vez
    /// de una caja rectangular oscura.
    /// </summary>
    public static IReadOnlyList<RegionPiece> BuildRegion(
        IReadOnlyList<Rect> tabRects,
        IReadOnlyList<Rect> circleRects,
        Rect? restContainer,
        double cornerRadius)
    {
        var pieces = new List<RegionPiece>(tabRects.Count + circleRects.Count + 1);

        if (restContainer is { } container && container.Width > 0 && container.Height > 0)
        {
            // Pastilla completa: va despegada del canto, así que se redondea por los cuatro lados.
            pieces.Add(new RegionPiece(container, Math.Min(container.Width, container.Height) / 2,
                SquareRightSide: false));
        }

        foreach (var tab in tabRects)
        {
            pieces.Add(new RegionPiece(tab, cornerRadius));
        }

        foreach (var circle in circleRects)
        {
            pieces.Add(new RegionPiece(circle, Math.Min(circle.Width, circle.Height) / 2,
                SquareRightSide: false));
        }

        return pieces;
    }
}
