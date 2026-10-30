using System.Windows;
using System.Windows.Media;
using Aldune.Core;
using Rect = System.Windows.Rect;

namespace Aldune.Windowing;

/// <summary>
/// El papel de las libretas, dibujado bajo el texto de la nota (el equivalente de <c>paperTexture</c> en Android): pliegues y grano en la libreta de
/// viaje; renglones y margen rojo en el cuaderno de campo. Todo con muy poca opacidad (se nota al mirar, nunca compite con las
/// letras) y con pocas líneas y puntos: un dibujo congelado del tamaño de la nota, que se rehace solo cuando cambia el tamaño.
/// </summary>
internal static class NotePaper
{
    /// <summary>El pincel del papel, o <c>null</c> si el aspecto no lleva. <paramref name="light"/>: si el papel es claro (las líneas van más apagadas en oscuro).</summary>
    public static Brush? Create(SkinPaper paper, Color ink, bool light, double width, double height, double lineHeight, double firstRule)
    {
        if (paper == SkinPaper.None || width < 1 || height < 1) return null;

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Un rectángulo transparente del tamaño entero: el dibujo no se recorta al contenido, y el pincel mide lo mismo que el elemento.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
            if (paper == SkinPaper.Folds) DrawFolds(dc, ink, light, width, height);
            else if (paper == SkinPaper.Grid) DrawGrid(dc, light, width, height, lineHeight, firstRule);
            else DrawRuled(dc, light, width, height, lineHeight, firstRule);
        }
        group.Freeze();
        var brush = new DrawingBrush(group) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, width, height) };
        brush.Freeze();
        return brush;
    }

    private static Pen Line(Color color, double alpha, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B)), thickness);
        pen.Freeze();
        return pen;
    }

    private static void DrawFolds(DrawingContext dc, Color ink, bool light, double w, double h)
    {
        var line = Line(ink, light ? 0.07 : 0.10, 1);
        var edge = Line(Colors.White, light ? 0.35 : 0.06, 1);
        // Dos dobleces verticales a los tercios, uno horizontal casi por la mitad y una arruga en diagonal: un papel doblado en cuatro y estirado.
        foreach (var fraction in new[] { 0.33, 0.66 })
        {
            double x = w * fraction;
            dc.DrawLine(line, new Point(x, 0), new Point(x + w * 0.015, h));
            dc.DrawLine(edge, new Point(x + 1.5, 0), new Point(x + 1.5 + w * 0.015, h));
        }
        dc.DrawLine(line, new Point(0, h * 0.48), new Point(w, h * 0.5));
        dc.DrawLine(Line(ink, (light ? 0.07 : 0.10) * 0.7, 1), new Point(w * 0.1, h * 0.9), new Point(w * 0.55, h * 0.62));
        // El grano: unos puntos en posiciones fijas (siempre los mismos).
        var grain = new SolidColorBrush(Color.FromArgb((byte)Math.Round((light ? 0.05 : 0.07) * 255), ink.R, ink.G, ink.B));
        grain.Freeze();
        uint seed = 12345;
        for (int i = 0; i < 50; i++)
        {
            seed = unchecked(seed * 1103515245 + 12345);
            double x = ((seed >> 8) & 0xFFFF) / 65535.0 * w;
            seed = unchecked(seed * 1103515245 + 12345);
            double y = ((seed >> 8) & 0xFFFF) / 65535.0 * h;
            dc.DrawEllipse(grain, null, new Point(x, y), 0.7, 0.7);
        }
    }

    // Cuadros de cuaderno: líneas azul grisáceo cada `lineHeight` (el alto de línea, así el texto cae sobre un renglón de cuadros) y margen rojo
    // a la izquierda, como el cuaderno de campo. Las horizontales parten de la primera línea del texto para que la letra apoye en un renglón.
    private static void DrawGrid(DrawingContext dc, bool light, double w, double h, double cell, double firstRule)
    {
        var grid = Line(light ? Color.FromRgb(0x4A, 0x78, 0xB0) : Color.FromRgb(0x9C, 0xBC, 0xE0), light ? 0.15 : 0.10, 1);
        for (double y = firstRule; y < h; y += cell) dc.DrawLine(grid, new Point(0, y), new Point(w, y));
        for (double x = 10 + cell; x < w; x += cell) dc.DrawLine(grid, new Point(x, 0), new Point(x, h));
        dc.DrawLine(Line(Color.FromRgb(0xBE, 0x3C, 0x32), light ? 0.40 : 0.30, 1.5), new Point(10, 0), new Point(10, h));
    }

    private static void DrawRuled(DrawingContext dc, bool light, double w, double h, double lineHeight, double firstRule)
    {
        var rule = Line(light ? Color.FromRgb(0x46, 0x6E, 0x96) : Color.FromRgb(0xA0, 0xBE, 0xDC), light ? 0.16 : 0.10, 1);
        for (double y = firstRule; y < h; y += lineHeight) dc.DrawLine(rule, new Point(0, y), new Point(w, y));
        dc.DrawLine(Line(Color.FromRgb(0xBE, 0x3C, 0x32), light ? 0.40 : 0.30, 1.5), new Point(10, 0), new Point(10, h));
    }
}
