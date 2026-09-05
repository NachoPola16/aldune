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
    /// Hueco entre pestañas <b>cuando caben todas</b>. En cuanto hay que solaparlas (ver
    /// <see cref="PitchFor"/>) desaparece, y con él los huecos de la región.
    /// </summary>
    public const double TabGap = 8;

    /// <summary>Paso entre pestañas cuando caben todas sin solaparse.</summary>
    public const double NaturalPitch = TabHeight + TabGap; // 108

    /// <summary>
    /// Paso mínimo al que pueden llegar a solaparse. Es lo que queda visible de cada pestaña, y
    /// por tanto cuánta etiqueta se llega a leer: por debajo de esto el abanico deja de decirte
    /// cuál es cuál, que es el único trabajo de una pestaña.
    /// </summary>
    public const double MinPitch = 34;

    /// <summary>
    /// Fracción del alto útil del monitor que el abanico desplegado puede ocupar.
    /// </summary>
    public const double MaxScreenFraction = 0.7;

    /// <summary>
    /// Tope absoluto de longitud del abanico, además de la fracción de pantalla.
    ///
    /// Sin él, en un monitor de 2560px de alto el presupuesto salían ~1900px: ocho notas cabían
    /// sin solaparse y el abanico ocupaba media pantalla, que es justo lo que el solape venía a
    /// evitar. La fracción sola no basta porque un monitor muy alto no significa que quieras un
    /// dock muy alto — el dock debe seguir siendo un objeto compacto en el canto.
    /// </summary>
    public const double MaxFanLength = 560;

    /// <summary>
    /// Paso real entre pestañas: se encoge conforme hay más notas, de modo que el abanico ocupa
    /// aproximadamente el mismo sitio tanto con cuatro notas como con veinte.
    ///
    /// Antes era una constante y la longitud se topaba, lo que dejaba las pestañas sobrantes
    /// <b>fuera del viewport del ScrollViewer y sin dibujar al desplegar</b> — se veían en reposo
    /// (donde el desplazamiento las trae a la tira) y desaparecían al pasar el ratón, sin ninguna
    /// barra de scroll que avisara de que había más. Bug reportado por el usuario al crear la
    /// quinta nota.
    ///
    /// Consecuencia asumida: al solaparse, la región (que es una unión, RGN_OR) deja de tener
    /// huecos y el abanico pasa de peine a losa continua. La separación entre pestañas la dan
    /// entonces el filete de 1px y la esquina redondeada de cada una, no el escritorio de por
    /// medio.
    /// </summary>
    public static double PitchFor(WorkingArea area, EdgePosition edge, int noteCount)
    {
        if (noteCount <= 1) return NaturalPitch;

        double available = edge is EdgePosition.Left or EdgePosition.Right ? area.Height : area.Width;
        double budget = Math.Min(available * MaxScreenFraction, MaxFanLength) - FooterLength;

        // Lo que ocupan sin solaparse: la última se ve entera, las demás aportan un paso.
        double natural = (noteCount - 1) * NaturalPitch + TabHeight;
        if (natural <= budget) return NaturalPitch;

        return Math.Max((budget - TabHeight) / (noteCount - 1), MinPitch);
    }

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

    /// <summary>
    /// Ancho del guión de color en reposo. Estrecho y alargado a propósito: con 18x26 salían casi
    /// cuadrados y la tira se leía como un selector de color, no como el canto de unas fichas.
    /// Comprobado renderizando tres proporciones en aislamiento sobre fondo claro antes de elegir.
    /// </summary>
    public const double RestDashWidth = 12;

    /// <summary>
    /// Ancho del contenedor que agrupa los guiones en reposo. Más ancho que el guión, para que
    /// enmarque cada uno con unos píxeles de fondo a cada lado.
    ///
    /// Este contenedor es lo que hace que la tira se lea como un objeto: sin él, cuatro pasteles
    /// claros sueltos sobre un escritorio claro desaparecen (comprobado en la app real). El vídeo
    /// de referencia lo tiene, aunque no se aprecia en su landing.
    /// </summary>
    public const double RestContainerWidth = 20;

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
    public const double RestClipLeft = TabWidth - RestDashInset - RestDashWidth; // 84

    /// <summary>Zona sensible al ratón en reposo: la del contenedor.</summary>
    public const double RestSliverWidth = RestContainerWidth + RestContainerInset; // 24

    /// <summary>Alto del guión de color de cada nota en reposo.</summary>
    public const double RestDashLength = 34;

    /// <summary>Hueco entre guiones en reposo. Es lo que deja ver el fondo del contenedor.</summary>
    public const double RestGap = 5;

    /// <summary>Paso entre guiones en reposo.</summary>
    public const double RestPitch = RestDashLength + RestGap; // 39

    // --- Ventana ------------------------------------------------------------------------------

    /// <summary>
    /// Grosor (perpendicular al borde) de la ventana del dock. Solo necesita cubrir la pestaña; el
    /// resto se recorta con la región.
    /// </summary>
    public const double WindowThickness = TabWidth + 8; // 112

    /// <summary>Longitud mínima de la ventana, para que con 0-1 notas siga siendo un objetivo de
    /// ratón razonable.</summary>
    public const double MinContentLength = NaturalPitch;

    /// <summary>
    /// Alto de la fila fija de botones ("+" y engranaje), fuera del ScrollViewer para que siempre
    /// se puedan pulsar sin scrollear. Sale del presupuesto de longitud <b>además</b> de lo que
    /// necesitan las pestañas; si no se reserva, la última queda a caballo del borde del scroll.
    /// </summary>
    public const double FooterLength = 80;

    /// <summary>A ras del borde físico. Sin sombra DWM (la región la descarta) no hay nada que
    /// reservar fuera de la ventana, y las fichas de un fichero van pegadas al canto.</summary>
    public const double EdgeMargin = 0;

    /// <summary>Longitud que ocupan las pestañas desplegadas: un paso por cada una salvo la
    /// última, que se ve entera.</summary>
    public static double TabStripLength(WorkingArea area, EdgePosition edge, int noteCount)
    {
        if (noteCount <= 0) return 0;
        return (noteCount - 1) * PitchFor(area, edge, noteCount) + TabHeight;
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

    /// <summary>Longitud total de la ventana: el abanico + la fila de botones.</summary>
    public static double WindowLength(WorkingArea area, EdgePosition edge, int noteCount) =>
        Math.Max(TabStripLength(area, edge, noteCount), MinContentLength) + FooterLength;

    /// <summary>
    /// Desplazamiento a lo largo del eje de longitud, dentro de la ventana, donde empieza la tira
    /// de guiones en reposo. Centrada respecto a la ventana, de modo que el dock en reposo queda
    /// centrado en el borde de la pantalla igual que el desplegado.
    /// </summary>
    public static double RestStripStart(WorkingArea area, EdgePosition edge, int noteCount) =>
        // Nunca negativo: con muchísimas notas la tira es más larga que la ventana, y un inicio
        // negativo recortaría las PRIMERAS notas contra el borde superior. Pegándola arriba, las
        // que sobran son las últimas, que es la degradación menos sorprendente.
        Math.Max(0, (WindowLength(area, edge, noteCount) - RestStripLength(noteCount)) / 2);

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

    public static double RestOffsetFor(WorkingArea area, EdgePosition edge, int index, int noteCount)
    {
        double dashCenter = RestStripStart(area, edge, noteCount) + index * RestPitch + RestDashLength / 2;
        double tabCenter = index * PitchFor(area, edge, noteCount) + TabHeight / 2;
        return dashCenter - tabCenter;
    }

    /// <summary>
    /// El rectángulo de la ventana del dock, idéntico en reposo y desplegado. Anclado por su
    /// centro sobre el eje del borde.
    /// </summary>
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
        double start = RestStripStart(area, edge, noteCount);
        // Acotada a lo que queda de ventana: la zona sensible no puede prometer más de lo que
        // realmente se ve, porque lo que cae fuera de la ventana lo recorta Windows.
        double length = Math.Min(
            RestStripLength(noteCount), WindowLength(area, edge, noteCount) - start);

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
