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
    /// 100 y no 80: con 80 la etiqueta girada no llega ni para "NUEVA NOTA" (la que sale por
    /// defecto al crear una nota) y salía cortada. El alto de la pestaña ES el ancho disponible
    /// para el texto, porque la rotación con LayoutTransform intercambia los ejes de medida.
    public const double TabHeight = 100;

    /// <summary>
    /// Hueco entre pestañas. Tiene que ser &gt; 0: la región se une con CombineRgn/RGN_OR, así que
    /// dos pestañas solapadas se funden en una sola mancha y el abanico desaparece.
    /// </summary>
    public const double TabGap = 8;

    /// <summary>Paso real de una pestaña a la siguiente. El presupuesto de longitud se calcula con
    /// esto, así que layout y geometría no pueden discrepar.</summary>
    public const double TabPitch = TabHeight + TabGap; // 108

    /// <summary>
    /// Ancho de <b>todas</b> las pestañas. Uniforme a propósito — ver el resumen de la clase.
    /// </summary>
    public const double TabWidth = 104;

    /// <summary>
    /// Distancia desde el borde derecho de la pestaña hasta la línea de troquelado. Todo lo que
    /// queda a la izquierda de esa línea es el lomo (etiqueta incluida); lo que queda a la derecha
    /// es el trocito de cuerpo que asoma.
    /// </summary>
    public const double PerforationInset = 16;

    /// <summary>Ancho del lomo: la parte de la pestaña que viaja con la nota al abrirla.</summary>
    public const double SpineWidth = TabWidth - PerforationInset; // 88

    // --- Reposo -------------------------------------------------------------------------------

    /// <summary>Ancho del guión de color de cada nota en reposo.</summary>
    public const double RestDashWidth = 18;

    /// <summary>
    /// Ancho del contenedor que agrupa los guiones en reposo. Más ancho que el guión, para que
    /// enmarque cada uno con unos píxeles de fondo a cada lado.
    ///
    /// Este contenedor es lo que hace que la tira se lea como un objeto: sin él, cuatro pasteles
    /// claros sueltos sobre un escritorio claro desaparecen (comprobado en la app real). El vídeo
    /// de referencia lo tiene, aunque no se aprecia en su landing.
    /// </summary>
    public const double RestContainerWidth = 26;

    /// <summary>Separación del contenedor respecto al canto de la pantalla. En reposo la tira va
    /// despegada del borde (como en la referencia); las pestañas desplegadas sí van a ras.</summary>
    public const double RestContainerInset = 4;

    /// <summary>Separación del guión respecto al canto, dentro del contenedor.</summary>
    public const double RestDashInset = RestContainerInset + 4; // 8

    /// <summary>
    /// Cuánto sobresale el contenedor por arriba y por abajo del primer y último guión. Sin este
    /// margen, el guión de arriba empieza exactamente en el borde redondeado del contenedor y la
    /// curva se lo come: el contenedor deja de leerse como algo que los contiene.
    /// </summary>
    public const double RestContainerPad = 5;

    /// <summary>
    /// Borde izquierdo del recorte de una pestaña en reposo, en coordenadas de la propia pestaña.
    ///
    /// El recorte horizontal tiene que hacerlo la pestaña (con <c>UIElement.Clip</c>, que no toca
    /// el layout), no la región: en reposo la región <b>es</b> el contenedor, así que una pestaña
    /// sin recortar pinta sus 104px enteros por detrás y el color llena la pastilla de lado a
    /// lado, sin dejar ver el marco de fondo que la enmarca.
    ///
    /// No se puede resolver con un ScaleX: aplastaría también la etiqueta, que en reposo está
    /// fuera de la zona visible precisamente porque vive en el extremo izquierdo de la pestaña.
    /// </summary>
    public const double RestClipLeft = TabWidth - RestDashInset - RestDashWidth; // 78

    /// <summary>Zona sensible al ratón en reposo: la del contenedor.</summary>
    public const double RestSliverWidth = RestContainerWidth + RestContainerInset; // 30

    /// <summary>Alto del guión de color de cada nota en reposo.</summary>
    public const double RestDashLength = 26;

    /// <summary>Hueco entre guiones en reposo. Es lo que deja ver el fondo del contenedor.</summary>
    public const double RestGap = 6;

    /// <summary>Paso entre guiones en reposo.</summary>
    public const double RestPitch = RestDashLength + RestGap; // 32

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
    public const double MaxContentLength = 4 * TabPitch; // 432

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
    /// <summary>
    /// Escala vertical de una pestaña en reposo.
    ///
    /// Hace falta porque el desplazamiento por sí solo no basta: una pestaña mide
    /// <see cref="TabHeight"/> (100) y el paso en reposo es <see cref="RestPitch"/> (32), así que
    /// con solo desplazarlas se solaparían 70px y taparían el fondo del contenedor — no se verían
    /// guiones separados, sino una mancha continua. Escalándolas se quedan en su banda y el fondo
    /// asoma por los huecos. Como en reposo solo se ve el extremo derecho de la pestaña, el
    /// aplastamiento de la etiqueta (que vive en el extremo izquierdo) no llega a verse.
    /// </summary>
    public static double RestScaleFor() => RestDashLength / TabHeight;

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
    /// Desde qué X debe empezar a deslizarse la ventana de una nota que se acaba de abrir.
    ///
    /// Lo natural sería arrancar en la X de su pestaña, con el cuerpo saliéndose por la derecha y
    /// entrando al deslizarse. Pero eso da por hecho que a la derecha del dock no hay nada, y con
    /// varios monitores es falso: el canto derecho de un monitor linda con el siguiente, así que
    /// la nota arrancaba dibujándose <b>encima de la otra pantalla</b> (reportado por el usuario
    /// con un juego a pantalla completa en ella). Acotando el origen al área de trabajo, el
    /// deslizamiento arranca siempre dentro del propio monitor, y de paso desaparecen los frames
    /// con media nota fuera de pantalla incluso con un solo monitor.
    ///
    /// Nunca devuelve algo a la izquierda de <paramref name="noteLeft"/>: la nota se desliza hacia
    /// la izquierda, así que el origen tiene que estar a su derecha o coincidir (sin animación).
    /// </summary>
    public static double SlideOriginFor(WorkingArea area, double tabX, double noteLeft, double noteWidth)
    {
        double furthestRight = area.X + area.Width - noteWidth;
        return Math.Max(Math.Min(tabX, furthestRight), noteLeft);
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
