using System.Windows;
using System.Windows.Media;

namespace Aldune.Windowing;

/// <summary>
/// El cuadrado de «maximizar» (o los dos cuadrados de «restaurar») dibujado, no escrito. Como carácter de una fuente de iconos su
/// posición dependía de la línea base de cada aspecto y del escalado del texto, y quedaba descentrado en el botón. Se dibuja en
/// píxeles físicos enteros (un cuadro de 10 DIP redondeado, p. ej. 13 px a 125 %) con trazos de un píxel, así que a 100 %, 125 %,
/// 150 % o 200 % todas las líneas caen exactas y del mismo grosor. La tinta es la del botón que lo contiene.
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

    /// <summary>Lado nominal, en DIP.</summary>
    private const double Size = 10;

    public MaximizeGlyph()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        IsHitTestVisible = false;
    }

    /// <summary>Lado del icono en píxeles físicos para una escala dada: el nominal redondeado, nunca menor de 8.</summary>
    public static int PixelSize(double pixelsPerDip) => Math.Max(8, (int)Math.Round(Size * pixelsPerDip));

    // The box is a whole number of physical pixels, so it never straddles a pixel boundary inside the button.
    protected override Size MeasureOverride(Size availableSize)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double side = PixelSize(dpi) / dpi;
        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.PushTransform(new ScaleTransform(1 / dpi, 1 / dpi));
        Draw(dc, PixelSize(dpi), IsRestore, Ink);
        dc.Pop();
    }

    /// <summary>Draws the glyph in physical pixels (1 unit = 1 px) inside an <paramref name="n"/> × <paramref name="n"/> box at the origin.</summary>
    public static void Draw(DrawingContext dc, int n, bool restore, Brush ink)
    {
        // A one-pixel stroke centred on the pixel (the .5) so it is not shared between two pixels.
        var pen = new Pen(ink, 1);
        pen.Freeze();
        if (!restore)
        {
            dc.DrawRectangle(null, pen, new Rect(0.5, 0.5, n - 1, n - 1));
            return;
        }

        // Restore: the front square at the bottom left and the one behind it showing at the top right, joined to the front one by its two stubs
        // (the way Windows draws it), so it reads as two windows and not as an open corner.
        int side = n - (int)Math.Round(n * 0.3);   // 7 of 10
        int off = n - side;                        // 3 of 10
        dc.DrawRectangle(null, pen, new Rect(0.5, off + 0.5, side - 1, side - 1));
        var back = new PathGeometry(new[]
        {
            new PathFigure(new Point(off + 0.5, off + 0.5), new PathSegment[]
            {
                new LineSegment(new Point(off + 0.5, 0.5), true),
                new LineSegment(new Point(n - 0.5, 0.5), true),
                new LineSegment(new Point(n - 0.5, side - 0.5), true),
                new LineSegment(new Point(side, side - 0.5), true),
            }, false)
        });
        back.Freeze();
        dc.DrawGeometry(null, pen, back);
    }
}
