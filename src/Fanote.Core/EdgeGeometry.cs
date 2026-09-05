namespace Fanote.Core;

/// <summary>
/// Geometría del dock.
///
/// Dos invariantes que vienen de rondas anteriores y conviene no romper:
///
/// 1. <b>La ventana no cambia de tamaño nunca</b>: siempre ocupa <see cref="WindowRect"/>. Animar
///    Left/Top/Width/Height de un HWND obliga a WPF a rehacer el layout en cada frame intermedio,
///    y de ahí salía toda la familia de fallos que documenta docs/STATUS.md. Con un rectángulo
///    fijo, el layout se mide una vez a tamaño final y nunca en uno intermedio.
///
/// 2. <b>El abanico ocupa aproximadamente lo mismo haya las notas que haya</b>: las pestañas se
///    solapan conforme se acumulan (ver <see cref="PitchFor"/>), en vez de crecer sin parar o de
///    toparse dejando las sobrantes sin dibujar.
///
/// Las etiquetas son <b>horizontales</b>. Con solape, la franja visible de cada pestaña es corta:
/// una etiqueta horizontal necesita ~18px de alto y una vertical ~90px, así que la vertical solo
/// funcionaba sin solapar. Horizontal deja leer el título entero siempre.
/// </summary>
public static class EdgeGeometry
{
    // --- Pestañas -----------------------------------------------------------------------------

    /// <summary>
    /// Alto de cada pestaña: dos líneas — el título y, debajo, las primeras palabras del cuerpo.
    ///
    /// Con una sola línea sobraban ~110px vacíos a la derecha del título. El hueco no era un
    /// problema de tamaño sino capacidad sin usar: la segunda línea convierte la pestaña en algo
    /// que dice qué hay dentro de la nota, no solo cómo se llama.
    /// </summary>
    public const double TabHeight = 52;

    /// <summary>
    /// Ancho de todas las pestañas. Uniforme: la pestaña viaja con la nota al abrirla y se
    /// convierte en su cabecera, así que anchos distintos darían cabeceras distintas.
    /// </summary>
    public const double TabWidth = 208;

    /// <summary>Hueco entre pestañas cuando caben todas sin solaparse.</summary>
    public const double TabGap = 8;

    /// <summary>Paso entre pestañas cuando caben todas.</summary>
    public const double NaturalPitch = TabHeight + TabGap; // 60

    /// <summary>
    /// Paso por debajo del cual la vista previa se esconde entera en vez de quedar cortada por la
    /// pestaña siguiente. Media línea de texto asomando parece un fallo de render, no una
    /// decisión: mejor enseñar solo el título, que es lo que no puede faltar.
    /// </summary>
    public const double PreviewVisiblePitch = TabHeight - 4;

    /// <summary>Si a este paso cabe la vista previa completa.</summary>
    public static bool ShowsPreview(WorkingArea area, EdgePosition edge, int noteCount) =>
        PitchFor(area, edge, noteCount) >= PreviewVisiblePitch;

    /// <summary>
    /// Paso mínimo al solaparse. Es lo que queda visible de cada pestaña, y por tanto si se lee o
    /// no su título: por debajo de esto el abanico deja de decirte cuál es cuál, que es su único
    /// trabajo. Con etiqueta horizontal basta con ~26px —lo justo para la línea del título, que es
    /// lo que no puede faltar—, frente a los ~90 que exigía la vertical.
    /// </summary>
    public const double MinPitch = 26;

    /// <summary>Fracción del alto útil del monitor que el abanico desplegado puede ocupar.</summary>
    public const double MaxScreenFraction = 0.7;

    /// <summary>
    /// Tope absoluto de longitud del abanico, además de la fracción de pantalla. Un monitor muy
    /// alto no significa que quieras un dock muy alto: el dock debe seguir siendo un objeto
    /// compacto en el canto.
    /// </summary>
    public const double MaxFanLength = 520;

    /// <summary>Paso real entre pestañas: se encoge conforme hay más notas.</summary>
    public static double PitchFor(WorkingArea area, EdgePosition edge, int noteCount)
    {
        if (noteCount <= 1) return NaturalPitch;

        double available = edge is EdgePosition.Left or EdgePosition.Right ? area.Height : area.Width;
        double budget = Math.Min(available * MaxScreenFraction, MaxFanLength) - FooterLength;

        // Sin solapar: la última se ve entera, las demás aportan un paso.
        double natural = (noteCount - 1) * NaturalPitch + TabHeight;
        if (natural <= budget) return NaturalPitch;

        return Math.Max((budget - TabHeight) / (noteCount - 1), MinPitch);
    }

    /// <summary>Longitud del abanico desplegado.</summary>
    public static double TabStripLength(WorkingArea area, EdgePosition edge, int noteCount)
    {
        if (noteCount <= 0) return 0;
        return (noteCount - 1) * PitchFor(area, edge, noteCount) + TabHeight;
    }

    // --- Reposo -------------------------------------------------------------------------------

    /// <summary>Ancho del guión de color de cada nota en reposo.</summary>
    public const double RestDashWidth = 12;

    /// <summary>Alto del guión de color de cada nota en reposo.</summary>
    public const double RestDashLength = 26;

    /// <summary>Hueco entre guiones en reposo.</summary>
    public const double RestGap = 5;

    /// <summary>Paso entre guiones en reposo.</summary>
    public const double RestPitch = RestDashLength + RestGap; // 31

    /// <summary>
    /// Ancho del contenedor que agrupa los guiones. Sin él, unos pasteles claros sueltos sobre un
    /// escritorio claro desaparecen: es lo que hace que la tira se lea como un objeto.
    /// </summary>
    public const double RestContainerWidth = 20;

    /// <summary>Separación del contenedor respecto al canto de la pantalla.</summary>
    public const double RestContainerInset = 5;

    /// <summary>Cuánto sobresale el contenedor del primer y último guión.</summary>
    public const double RestContainerPad = 5;

    /// <summary>Zona sensible al ratón en reposo.</summary>
    public const double RestSliverWidth = RestContainerWidth + RestContainerInset * 2;

    /// <summary>Longitud de la tira de guiones en reposo: uno por nota.</summary>
    public static double RestStripLength(int noteCount)
    {
        if (noteCount <= 0) return 0;
        return noteCount * RestPitch - RestGap;
    }

    // --- Ventana ------------------------------------------------------------------------------

    /// <summary>
    /// Aire alrededor del contenido para que quepa su sombra. Con transparencia la ventana ya no se
    /// recorta con una región: la sombra se dibuja fuera de la pestaña y necesita sitio dentro de
    /// la ventana, o saldría cortada por el borde del HWND.
    /// </summary>
    public const double ShadowMargin = 18;

    /// <summary>Grosor de la ventana: la pestaña más el aire de su sombra.</summary>
    public const double WindowThickness = TabWidth + ShadowMargin;

    /// <summary>Longitud mínima, para que con 0-1 notas siga siendo un objetivo razonable.</summary>
    public const double MinContentLength = NaturalPitch;

    /// <summary>Alto de la fila de botones ("+" y engranaje), fuera de la lista con scroll.</summary>
    public const double FooterLength = 56;

    /// <summary>Separación del borde físico de la pantalla.</summary>
    public const double EdgeMargin = 0;

    /// <summary>Longitud total de la ventana: el abanico + la fila de botones + aire de sombra.</summary>
    public static double WindowLength(WorkingArea area, EdgePosition edge, int noteCount) =>
        Math.Max(TabStripLength(area, edge, noteCount), MinContentLength)
        + FooterLength + ShadowMargin * 2;

    /// <summary>El rectángulo de la ventana, idéntico en reposo y desplegado.</summary>
    public static Rect WindowRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        double length = WindowLength(area, edge, noteCount);
        return edge switch
        {
            EdgePosition.Top => new Rect(
                area.X + (area.Width - length) / 2, area.Y + EdgeMargin, length, WindowThickness),
            EdgePosition.Bottom => new Rect(
                area.X + (area.Width - length) / 2, area.Y + area.Height - WindowThickness - EdgeMargin, length, WindowThickness),
            EdgePosition.Left => new Rect(
                area.X + EdgeMargin, area.Y + (area.Height - length) / 2, WindowThickness, length),
            EdgePosition.Right => new Rect(
                area.X + area.Width - WindowThickness - EdgeMargin, area.Y + (area.Height - length) / 2, WindowThickness, length),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    /// <summary>
    /// La parte de <see cref="WindowRect"/> sensible al ratón con el dock en reposo: la tira de
    /// guiones, no la ventana entera. Con transparencia el resto de la ventana ya es transparente
    /// al clic, pero el sondeo de hover tampoco debe desplegar el dock por pasar el ratón sobre una
    /// zona vacía.
    /// </summary>
    public static Rect RestingVisibleRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        var window = WindowRect(area, edge, noteCount);
        double windowLength = WindowLength(area, edge, noteCount);
        double length = Math.Min(RestStripLength(noteCount) + RestContainerPad * 2, windowLength);
        double start = Math.Max(0, (windowLength - length) / 2);

        return edge switch
        {
            EdgePosition.Top => new Rect(window.X + start, window.Y, length, RestSliverWidth),
            EdgePosition.Bottom => new Rect(window.X + start, window.Y + window.Height - RestSliverWidth, length, RestSliverWidth),
            EdgePosition.Left => new Rect(window.X, window.Y + start, RestSliverWidth, length),
            EdgePosition.Right => new Rect(window.X + window.Width - RestSliverWidth, window.Y + start, RestSliverWidth, length),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    /// <summary>
    /// Desde qué X debe empezar a deslizarse la ventana de una nota recién abierta, acotado al área
    /// de trabajo del monitor.
    ///
    /// Lo natural sería arrancar en la X de su pestaña con el cuerpo saliéndose por la derecha,
    /// pero eso da por hecho que a la derecha no hay nada, y con varios monitores es falso: el
    /// canto de uno linda con el siguiente, así que la nota arrancaba dibujándose encima de la otra
    /// pantalla.
    /// </summary>
    public static double SlideOriginFor(WorkingArea area, double tabX, double noteLeft, double noteWidth)
    {
        double furthestRight = area.X + area.Width - noteWidth;
        return Math.Max(Math.Min(tabX, furthestRight), noteLeft);
    }
}
