namespace Fanote.Core;

/// <summary>
/// Geometría del dock. Desde el rediseño de movimiento (rama dock-motion-shape) la ventana del
/// dock <b>no cambia de tamaño nunca</b>: siempre ocupa <see cref="WindowRect"/> (el rectángulo
/// "expandido"), y lo que cambia entre reposo y desplegado es únicamente la región recortada
/// (SetWindowRgn) dentro de ese rectángulo fijo.
///
/// El motivo es que animar Left/Top/Width/Height de un HWND obliga a WPF a rehacer el layout en
/// cada frame intermedio, y de ahí salía toda la familia de fallos que documenta docs/STATUS.md:
/// contenido encajado en anchos que no le caben, la Height que terminaba su animación sin que la
/// ventana real se redimensionara, y el WM_MOUSELEAVE espurio que obligó a sondear el cursor.
/// Con un rectángulo fijo, el layout se mide una sola vez a tamaño final y nunca en un tamaño
/// intermedio, así que esa clase de fallo deja de ser posible por construcción.
/// </summary>
public static class EdgeGeometry
{
    // --- Pestañas -----------------------------------------------------------------------------

    /// <summary>
    /// Alto de cada pestaña. La etiqueta va rotada -90° con LayoutTransform, que intercambia los
    /// ejes de medida: este alto es el <b>ancho</b> disponible para el texto. 80px deja ~68px de
    /// texto tras el padding, que es lo mínimo para que un título corto no salga elidido a dos
    /// caracteres.
    /// </summary>
    public const double TabHeight = 80;

    /// <summary>
    /// Hueco entre pestañas. Tiene que ser &gt; 0: la región se construye con CombineRgn/RGN_OR,
    /// así que dos pestañas que se solapen se funden en una sola mancha y el abanico desaparece.
    /// El diseño anterior usaba Margin=-28 (solape) contra un presupuesto de 88px por nota — las
    /// dos cifras se contradecían, y el resultado unido por RGN_OR era una columna irregular, no
    /// un abanico.
    /// </summary>
    public const double TabGap = 8;

    /// <summary>Paso real de una pestaña a la siguiente. El presupuesto de longitud se calcula
    /// con esto, así que layout y geometría no pueden volver a discrepar.</summary>
    public const double TabPitch = TabHeight + TabGap; // 88

    /// <summary>
    /// Ancho de la pestaña más corta (la primera) y de la más larga (la última). El abanico
    /// interpola entre ambos <b>en función de la fracción índice/(total-1)</b>, no sumando un
    /// incremento fijo por índice como antes (32 + i*14): aquella fórmula no estaba acotada y con
    /// 14 notas la pestaña ya era más ancha que la propia ventana.
    /// </summary>
    public const double TabMinWidth = 72;
    public const double TabMaxWidth = 128;

    /// <summary>Lo que asoma de cada pestaña cuando el dock está en reposo — la "tira" de color
    /// que sustituye a las antiguas pastillas (PillSwatches) del pill.</summary>
    public const double RestSliverWidth = 20;

    // --- Ventana ------------------------------------------------------------------------------

    /// <summary>
    /// Grosor (perpendicular al borde) de la ventana del dock. Solo necesita cubrir la pestaña
    /// más ancha; el resto se recorta con la región. Antes eran 220px para pestañas de 32-74px,
    /// es decir tres veces el espacio que se usaba.
    /// </summary>
    public const double WindowThickness = TabMaxWidth + 12; // 140

    /// <summary>Longitud mínima de la ventana, para que con 0-1 notas el dock siga siendo un
    /// objetivo de ratón razonable.</summary>
    public const double MinContentLength = TabPitch;

    /// <summary>
    /// Máxima longitud dedicada a pestañas antes de que la lista pase a hacer scroll. Múltiplo
    /// exacto de <see cref="TabPitch"/> a propósito: si no lo fuera, la vista inicial sin
    /// scrollear cortaría la última pestaña por la mitad desde el primer hover. 352 = 4 * 88.
    /// </summary>
    public const double MaxContentLength = 4 * TabPitch; // 352

    /// <summary>
    /// Alto de la fila fija de botones ("+" y engranaje), fuera del ScrollViewer para que siempre
    /// se puedan pulsar sin scrollear. Sale del presupuesto de longitud de la ventana <b>además</b>
    /// de lo que necesitan las pestañas; si no se reserva, la última pestaña queda a caballo del
    /// borde del scroll. Dos botones de 32px con 4+4 de margen = 80.
    /// </summary>
    public const double FooterLength = 80;

    /// <summary>Separación del borde físico de la pantalla. Menor que antes (6): sin sombra DWM
    /// activa —la región la desactiva— ya no hace falta reservarle sitio para renderizar.</summary>
    public const double EdgeMargin = 2;

    /// <summary>Ancho de la pestaña de índice <paramref name="index"/> dentro de un abanico de
    /// <paramref name="noteCount"/> notas. Acotado siempre entre TabMinWidth y TabMaxWidth.</summary>
    public static double TabWidth(int index, int noteCount)
    {
        if (noteCount <= 1) return TabMinWidth;
        double t = Math.Clamp((double)index / (noteCount - 1), 0, 1);
        return TabMinWidth + t * (TabMaxWidth - TabMinWidth);
    }

    /// <summary>Longitud que ocupan las pestañas, acotada. Sin el hueco sobrante de la última:
    /// el paso incluye un hueco por pestaña, pero después de la última no hay nada que separar.</summary>
    public static double TabStripLength(int noteCount)
    {
        if (noteCount <= 0) return 0;
        return Math.Min(noteCount * TabPitch, MaxContentLength) - TabGap;
    }

    /// <summary>Longitud total de la ventana: pestañas (acotadas) + la fila de botones.</summary>
    public static double WindowLength(int noteCount) =>
        Math.Clamp(noteCount * TabPitch, MinContentLength, MaxContentLength) + FooterLength;

    /// <summary>
    /// El rectángulo de la ventana del dock, idéntico en reposo y desplegado. Se ancla por su
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
    /// dock en reposo: solo la tira de <see cref="RestSliverWidth"/> pegada al borde físico.
    /// El sondeo de hover tiene que probar contra esto y no contra la ventana entera — la ventana
    /// ahora es siempre ancha, pero sus zonas recortadas son transparentes al clic, así que
    /// desplegarse al entrar en ellas sería desplegarse "por la nada".
    /// </summary>
    public static Rect RestingVisibleRect(WorkingArea area, EdgePosition edge, int noteCount)
    {
        var window = WindowRect(area, edge, noteCount);

        // Solo lo que ocupan las pestañas, no la ventana entera: en reposo el footer no se dibuja
        // (su barrido vale 0), así que incluirlo aquí daría una banda muerta al final de la tira
        // donde el ratón desplegaría el dock sin haber nada visible bajo el cursor.
        double length = TabStripLength(noteCount);

        return edge switch
        {
            EdgePosition.Top => new Rect(window.X, window.Y, length, RestSliverWidth),
            EdgePosition.Bottom => new Rect(window.X, window.Y + window.Height - RestSliverWidth, length, RestSliverWidth),
            EdgePosition.Left => new Rect(window.X, window.Y, RestSliverWidth, length),
            EdgePosition.Right => new Rect(window.X + window.Width - RestSliverWidth, window.Y, RestSliverWidth, length),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }
}
