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

    /// <summary>
    /// Lo máximo que puede medir el abanico desplegado, descontando lo que ocupan la fila de botones
    /// y el aire de la sombra. Es el presupuesto que reparte <see cref="PitchFor"/> **y** el tope al
    /// que se ciñe <see cref="WindowLength"/>: si no lo respetaran los dos, pasa lo que pasaba antes
    /// — el suelo de legibilidad (<see cref="MinPitch"/>) manda sobre el reparto, el abanico se sale
    /// del presupuesto, y como la ventana se dimensionaba al abanico, **crecía la ventana** en vez de
    /// entrar a funcionar el scroll. Con muchas notas el dock dejaba de ser un objeto compacto en el
    /// canto y podía salirse de la pantalla.
    /// </summary>
    public static double FanBudget(WorkingArea area, EdgePosition edge)
    {
        double available = edge is EdgePosition.Left or EdgePosition.Right ? area.Height : area.Width;
        double budget = Math.Min(available * MaxScreenFraction, MaxFanLength)
            - FooterLength - TabShadowHeadroom;

        // Nunca por debajo del mínimo: en una pantalla diminuta el presupuesto podría salir negativo
        // y arrastrar al resto de cálculos.
        return Math.Max(budget, MinContentLength);
    }

    /// <summary>Paso real entre pestañas: se encoge conforme hay más notas.</summary>
    public static double PitchFor(WorkingArea area, EdgePosition edge, int noteCount)
    {
        if (noteCount <= 1) return NaturalPitch;

        double budget = FanBudget(area, edge);

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

    /// <summary>
    /// Zona sensible al ratón en reposo: el contenedor más su separación del canto, más otro tanto
    /// de holgura por el lado libre. Cubre de sobra la tira visible, que es lo que se quiere — un
    /// objetivo de 20px de ancho pegado al borde es incómodo de acertar si la zona no perdona algo.
    /// </summary>
    public const double RestSliverWidth = RestContainerWidth + RestContainerInset * 2;

    /// <summary>Longitud de la tira de guiones en reposo: uno por nota.</summary>
    public static double RestStripLength(int noteCount)
    {
        if (noteCount <= 0) return 0;
        return noteCount * RestPitch - RestGap;
    }

    /// <summary>
    /// Cuántos guiones de reposo caben de verdad dentro de la ventana.
    ///
    /// La tira de reposo dibuja uno por nota y **no hace scroll** (no hay dónde: en reposo el dock
    /// es una tira estrecha en el canto). Antes esto se disimulaba porque la ventana crecía con el
    /// número de notas; en cuanto la ventana tiene tope (ver <see cref="FanBudget"/>), los guiones
    /// sobrantes se dibujarían fuera y quedarían recortados — y ya pasó una vez que un guion se veía
    /// pero caía fuera de la zona sensible al ratón (ver docs/STATUS.md). Mejor enseñar los que caben
    /// y ya está: la tira es un indicador de que hay notas, no un índice.
    /// </summary>
    public static int RestDashCapacity(WorkingArea area, EdgePosition edge, int noteCount)
    {
        double usable = WindowLength(area, edge, noteCount) - RestContainerPad * 2;
        if (usable <= 0) return 0;

        // n guiones ocupan n*RestPitch - RestGap.
        int capacity = (int)Math.Floor((usable + RestGap) / RestPitch);
        return Math.Max(capacity, 0);
    }

    /// <summary>Guiones que se dibujan de verdad: los que hay, acotados a los que caben.</summary>
    public static int VisibleRestDashes(WorkingArea area, EdgePosition edge, int noteCount) =>
        Math.Min(noteCount, RestDashCapacity(area, edge, noteCount));

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

    /// <summary>
    /// Alto de la fila de botones ("+" y engranaje), fuera de la lista con scroll para que siempre
    /// se puedan pulsar. Es el botón mayor (40) más su margen superior (14), el relleno del panel
    /// oscuro que los agrupa (7 arriba y abajo) y aire para la sombra.
    /// </summary>
    public const double FooterLength = 78;

    /// <summary>Separación del borde físico de la pantalla.</summary>
    public const double EdgeMargin = 0;

    /// <summary>
    /// Aire por encima de la primera pestaña, **dentro** de la lista con scroll.
    ///
    /// La sombra de las pestañas se proyecta hacia arriba a propósito (ver <c>CardShadow</c> en
    /// EdgeDockWindow.xaml: así cada una sombrea a la de encima y el abanico se lee como un mazo
    /// escalonado). La primera no tiene ninguna encima, así que su sombra cae fuera del contenido —
    /// y el <c>ScrollViewer</c> recorta a su viewport, de modo que se perdía entera: la pestaña de
    /// arriba se veía como un rectángulo de canto recto, sin fundido, mientras las de abajo sí
    /// tenían su sombra. El hueco tiene que ir dentro del contenido desplazable (un margen en la
    /// lista), no como relleno del ScrollViewer, porque el recorte ocurre justo en ese borde.
    /// </summary>
    public const double TabShadowHeadroom = 14;

    /// <summary>
    /// Cuánto abanico se ve de una vez: el que hay, acotado al presupuesto. Lo que sobra existe y se
    /// alcanza con la rueda del ratón — el <c>ScrollViewer</c> del dock — en vez de estirar la
    /// ventana hasta salirse de la pantalla.
    /// </summary>
    public static double VisibleStripLength(WorkingArea area, EdgePosition edge, int noteCount) =>
        Math.Min(TabStripLength(area, edge, noteCount), FanBudget(area, edge));

    /// <summary>Longitud total de la ventana: el abanico visible + la fila de botones + aire de sombra.</summary>
    public static double WindowLength(WorkingArea area, EdgePosition edge, int noteCount) =>
        Math.Max(VisibleStripLength(area, edge, noteCount), MinContentLength)
        + TabShadowHeadroom + FooterLength + ShadowMargin * 2;

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

        // Contra los guiones que se dibujan de verdad, no contra el número de notas: si hay más
        // notas de las que caben, la zona sensible tiene que coincidir con lo que se ve, o habría
        // una franja que responde al ratón sin nada debajo (o al revés).
        int dashes = VisibleRestDashes(area, edge, noteCount);
        double length = Math.Min(RestStripLength(dashes) + RestContainerPad * 2, windowLength);
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

}
