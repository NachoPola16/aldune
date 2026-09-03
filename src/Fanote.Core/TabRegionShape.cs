namespace Fanote.Core;

public readonly record struct RegionPiece(Rect Bounds, double CornerRadius);

// Deliberadamente sin combinar ni deduplicar rects — cada pieza se pasa tal cual al interop
// Win32 (NativeMethods.SetTabFanRegion), que ya sabe unirlas con CombineRgn sin que a esta
// función pura le importe cómo se combinan a nivel de sistema operativo.
public static class TabRegionShape
{
    public static IReadOnlyList<RegionPiece> BuildRegion(
        IReadOnlyList<Rect> tabRects, Rect footerRect, double cornerRadius)
    {
        var pieces = new List<RegionPiece>(tabRects.Count + 1);
        foreach (var tab in tabRects)
        {
            pieces.Add(new RegionPiece(tab, cornerRadius));
        }
        pieces.Add(new RegionPiece(footerRect, 0)); // footer: rectángulo plano, ver spec "Alcance"
        return pieces;
    }
}
