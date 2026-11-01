using System.Windows;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// El cuadrado de «maximizar» (o los dos cuadrados de «restaurar») dibujado, no escrito. Como carácter de una fuente de iconos su
/// posición dependía de la línea base de cada aspecto y del escalado del texto, y quedaba descentrado en el botón; en un cuadro fijo de
/// 10×10 px con el trazo alineado al píxel queda en el mismo sitio en todos. La tinta es la del botón que lo contiene.
/// </summary>
public sealed class MaximizeGlyph : FrameworkElement
{
    public static readonly DependencyProperty IsRestoreProperty = DependencyProperty.Register(
        nameof(IsRestore), typeof(bool), typeof(MaximizeGlyph),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
        nameof(Ink), typeof(Brush), typeof(MaximizeGlyph),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Dos cuadrados superpuestos (ventana maximizada) en vez de uno.</summary>
    public bool IsRestore { get => (bool)GetValue(IsRestoreProperty); set => SetValue(IsRestoreProperty, value); }

    public Brush Ink { get => (Brush)GetValue(InkProperty); set => SetValue(InkProperty, value); }

    private const double Size = 10;

    public MaximizeGlyph()
    {
        Width = Size;
        Height = Size;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        // Un trazo de un píxel físico, centrado en el píxel: el 0.5 evita que se reparta entre dos y salga borroso.
        double t = 1 / dpi, half = t / 2;
        var pen = new Pen(Ink, t);
        pen.Freeze();
        if (!IsRestore)
        {
            dc.DrawRectangle(null, pen, new Rect(half, half, Size - t, Size - t));
            return;
        }

        // Restaurar: el cuadrado de delante abajo a la izquierda y el de detrás asomando arriba a la derecha.
        double s = 7.5;
        dc.DrawRectangle(null, pen, new Rect(half, Size - s + half, s - t, s - t));
        var back = new PathGeometry(new[]
        {
            new PathFigure(new Point(2.5 + half, half), new PathSegment[]
            {
                new LineSegment(new Point(Size - half, half), true),
                new LineSegment(new Point(Size - half, s - 1 - half), true),
                new LineSegment(new Point(s - 0.5, s - 1 - half), true),
            }, false)
        });
        back.Freeze();
        dc.DrawGeometry(null, pen, back);
    }
}
