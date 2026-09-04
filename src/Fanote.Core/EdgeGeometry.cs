namespace Fanote.Core;

/// <summary>
/// Geometría del dock. La ventana del dock <b>no cambia de tamaño nunca</b>: siempre ocupa
/// <see cref="WindowRect"/>, y lo que cambia entre reposo y desplegado es únicamente la región
/// recortada (SetWindowRgn) dentro de ese rectángulo fijo.
///
/// El motivo es que animar Left/Top/Width/Height de un HWND obliga a WPF a rehacer el layout en
/// cada frame intermedio, y de ahí salía toda la familia de fallos que documenta docs/STATUS.md.
/// Con un rectángulo fijo, el layout se mide una sola vez a tamaño final y nunca en un tamaño
/// intermedio, así que esa clase de fallo deja de ser posible por construcción.
///
/// El modelo mental, tomado del vídeo de Hold My Notes: <b>cada pestaña es su nota, con casi todo
/// el cuerpo fuera de pantalla</b>. La línea de troquelado (<see cref="PerforationInset"/>) marca
/// dónde acaba el lomo y empieza el cuerpo; al abrir la nota, ese mismo lomo viaja con ella y se
/// convierte en su borde izquierdo. Por eso todas las pestañas miden lo mismo: si fueran una
/// escalera, cada nota se abriría con un lomo de grosor distinto.
/// </summary>
public static class EdgeGeometry
{
    // --- Pestañas -----------------------------------------------------------------------------

    /// <summary>
    /// Alto de cada pestaña. La etiqueta va rotada -90° con LayoutTransform, que intercambia los
    /// ejes de medida: este alto es el <b>ancho</b> disponible para el texto.
    /// </summary>
    public const double TabHeight = 80;

    /// <summary>
    /// Hueco entre pestañas. Tiene que ser &gt; 0: la región se une con CombineRgn/RGN_OR, así que
    /// dos pestañas solapadas se funden en una sola mancha y el abanico desaparece.
    /// </summary>
    public const double TabGap = 8;

    /// <summary>Paso real de una pestaña a la siguiente. El presupuesto de longitud se calcula con
    /// esto, así que layout y geometría no pueden discrepar.</summary>
    public const double TabPitch = TabHeight + TabGap; // 88

    /// <summary>
    /// Ancho de <b>todas</b> las pestañas. Uniforme a propósito — ver el resumen de la clase.
    /// </summary>
    public const double TabWidth = 104;

    /// <summary>
    /// Distancia desde el borde derecho de la pestaña hasta la línea de troquelado. Todo lo que
    /// queda a la izquierda de esa línea es el lomo (etiqueta incluida); lo que queda a la derecha
    /// es el trocito de cuerpo que asoma. Coincide con <see cref="RestSliverWidth"/> para que en
    /// reposo el borde izquierdo del guión de color <b>sea</b> exactamente el troquelado.
    /// </summary>
    public const double PerforationInset = 16;

    /// <summary>Ancho del lomo: la parte de la pestaña que viaja con la nota al abrirla.</summary>
    public const double SpineWidth = TabWidth - PerforationInset; // 88

    // --- Reposo -------------------------------------------------------------------------------

    /// <summary>Lo que asoma de cada pestaña con el dock en reposo.</summary>
    public const double RestSliverWidth = PerforationInset; // 16

    /// <summary>Alto del guión de color de cada nota en reposo.</summary>
    public const double RestDashLength = 24;

    /// <summary>Hueco entre guiones en reposo.</summary>
    public const double RestGap = 6;

    /// <summary>Paso entre guiones en reposo.</summary>
    public const double RestPitch = RestDashLength + RestGap; // 30

    // --- Ventana ------------------------------------------------------------------------------

    /// <summary>
    /// Grosor (perpendicular al borde) de la ventana del dock. Solo necesita cubrir la pestaña; el
    /// resto se recorta con la región.
    /// </summary>
    public const double WindowThickness = TabWidth + 8; // 112

    /// <summary>Longitud mínima de la ventana, para que con 0-1 notas siga siendo un objetivo de
    /// ratón razonable.</summary>
    public const double MinContentLength = TabPitch;

    /// <summary>
    /// Máxima longitud dedicada a pestañas antes de que la lista pase a hacer scroll. Múltiplo
    /// exacto de <see cref="TabPitch"/> a propósito: si no lo fuera, la vista inicial sin
    /// scrollear cortaría la última pestaña por la mitad desde el primer hover.
    /// </summary>
    public const double MaxContentLength = 4 * TabPitch; // 352

    /// <summary>
    /// Alto de la fila fija de botones ("+" y engranaje), fuera del ScrollViewer para que siempre
    /// se puedan pulsar sin scrollear. Sale del presupuesto de longitud <b>además</b> de lo que
    /// necesitan las pestañas; si no se reserva, la última queda a caballo del borde del scroll.
    /// </summary>
    public const double FooterLength = 80;

    /// <summary>A ras del borde físico. Sin sombra DWM (la región la descarta) no hay nada que
    /// reservar fuera de la ventana, y las fichas de un fichero van pegadas al canto.</summary>
    public const double EdgeMargin = 0;

    /// <summary>Longitud que ocupan las pestañas desplegadas, acotada. Sin el hueco sobrante de la
    /// última: el paso incluye un hueco por pestaña, pero tras la última no hay nada que separar.</summary>
    public static double TabStripLength(int noteCount)
    {
        if (noteCount <= 0) return 0;
        return Math.Min(noteCount * TabPitch, MaxContentLength) - TabGap;
    }

    /// <summary>
    /// Longitud de la tira de guiones en reposo. Mucho más corta que la desplegada: es la
    /// diferencia entre insinuar que hay notas y ocupar el borde entero de la pantalla.
    ///
    /// Un guión por nota, <b>sin el tope de <see cref="MaxContentLength"/></b>. Ese tope existe
    /// porque el abanico desplegado hace scroll, pero en reposo no hay scroll ninguno: las notas
    /// que no caben desplegadas sí tienen su guión, porque el desplazamiento de reposo las trae a
    /// la tira. Aplicar aquí el tope del scroll dejaba los guiones sobrantes visibles pero fuera
    /// de la zona sensible al ratón — se veían y no se podían pulsar.
    /// </summary>
    public static double RestStripLength(int noteCount)
    {
        if (noteCount <= 0) return 0;
        return noteCount * RestPitch - RestGap;
    }

    /// <summary>Longitud total de la ventana: pestañas (acotadas) + la fila de botones.</summary>
    public static double WindowLength(int noteCount) =>
        Math.Clamp(noteCount * TabPitch, MinContentLength, MaxContentLength) + FooterLength;

    /// <summary>
    /// Desplazamiento a lo largo del eje de longitud, dentro de la ventana, donde empieza la tira
    /// de guiones en reposo. Centrada respecto a la ventana, de modo que el dock en reposo queda
    /// centrado en el borde de la pantalla igual que el desplegado.
    /// </summary>
    public static double RestStripStart(int noteCount) =>
        // Nunca negativo: con muchísimas notas la tira es más larga que la ventana, y un inicio
        // negativo recortaría las PRIMERAS notas contra el borde superior. Pegándola arriba, las
        // que sobran son las últimas, que es la degradación menos sorprendente.
        Math.Max(0, (WindowLength(noteCount) - RestStripLength(noteCount)) / 2);

    /// <summary>
    /// Cuánto hay que desplazar la pestaña <paramref name="index"/> (sin tocar el layout, con un
    /// RenderTransform) para que en reposo su centro caiga sobre el centro de su guión.
    ///
    /// Existe porque reposo y desplegado usan pasos distintos (30 frente a 88): sin este
    /// desplazamiento, la banda que la región deja ver para la nota 2 caería sobre píxeles de la
    /// nota 1, y los colores saldrían cambiados.
    /// </summary>
    public static double RestOffsetFor(int index, int noteCount)
    {
        double dashCenter = RestStripStart(noteCount) + index * RestPitch + RestDashLength / 2;
        double tabCenter = index * TabPitch + TabHeight / 2;
        return dashCenter - tabCenter;
    }

    /// <summary>
    /// El rectángulo de la ventana del dock, idéntico en reposo y desplegado. Anclado por su
    /// centro sobre el eje del borde.
    /// </summary>
    public static Rect WindowRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        double length = WindowLength(noteCount);
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
    /// La parte de <see cref="WindowRect"/> que es visible (y por tanto sensible al ratón) con el
    /// dock en reposo: la tira de guiones, no la ventana entera. Lo recortado es transparente al
    /// ratón, así que desplegarse al entrar ahí sería desplegarse por pasar el ratón sobre nada.
    /// </summary>
    public static Rect RestingVisibleRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        var window = WindowRect(area, edge, noteCount);
        double start = RestStripStart(noteCount);
        // Acotada a lo que queda de ventana: la zona sensible no puede prometer más de lo que
        // realmente se ve, porque lo que cae fuera de la ventana lo recorta Windows.
        double length = Math.Min(RestStripLength(noteCount), WindowLength(noteCount) - start);

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
