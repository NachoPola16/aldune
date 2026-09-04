using System.Collections.Generic;
using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class EdgeGeometryTests
{
    private static readonly WorkingArea Area = new(0, 0, 1920, 1040);
    private static readonly WorkingArea SecondaryArea = new(-1920, 100, 1600, 900);

    public static IEnumerable<object[]> Areas()
    {
        yield return new object[] { Area };
        yield return new object[] { SecondaryArea };
    }

    public static IEnumerable<object[]> Edges()
    {
        yield return new object[] { EdgePosition.Top };
        yield return new object[] { EdgePosition.Bottom };
        yield return new object[] { EdgePosition.Left };
        yield return new object[] { EdgePosition.Right };
    }

    // --- Colocación de la ventana --------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Areas))]
    public void WindowRect_Right_IsInsetFromRightEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.WindowRect(area, EdgePosition.Right, noteCount: 3);
        Assert.Equal(area.X + area.Width - EdgeGeometry.WindowThickness - EdgeGeometry.EdgeMargin, rect.X);
        Assert.Equal(EdgeGeometry.WindowThickness, rect.Width);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void WindowRect_Left_IsInsetFromLeftEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.WindowRect(area, EdgePosition.Left, noteCount: 3);
        Assert.Equal(area.X + EdgeGeometry.EdgeMargin, rect.X);
        Assert.Equal(EdgeGeometry.WindowThickness, rect.Width);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void WindowRect_Top_IsInsetFromTopEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.WindowRect(area, EdgePosition.Top, noteCount: 3);
        Assert.Equal(area.Y + EdgeGeometry.EdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.WindowThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void WindowRect_Bottom_IsInsetFromBottomEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.WindowRect(area, EdgePosition.Bottom, noteCount: 3);
        Assert.Equal(area.Y + area.Height - EdgeGeometry.WindowThickness - EdgeGeometry.EdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.WindowThickness, rect.Height);
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void WindowRect_IsCenteredOnTheEdgesLengthAxis(EdgePosition edge)
    {
        var rect = EdgeGeometry.WindowRect(Area, edge, noteCount: 3);
        double length = EdgeGeometry.WindowLength(3);

        if (edge is EdgePosition.Top or EdgePosition.Bottom)
        {
            Assert.Equal(Area.X + (Area.Width - length) / 2, rect.X);
            Assert.Equal(length, rect.Width);
        }
        else
        {
            Assert.Equal(Area.Y + (Area.Height - length) / 2, rect.Y);
            Assert.Equal(length, rect.Height);
        }
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void WindowRect_StaysWithinTheWorkingArea(EdgePosition edge)
    {
        var rect = EdgeGeometry.WindowRect(SecondaryArea, edge, noteCount: 3);
        Assert.True(rect.X >= SecondaryArea.X, $"left {rect.X} < area {SecondaryArea.X}");
        Assert.True(rect.Y >= SecondaryArea.Y, $"top {rect.Y} < area {SecondaryArea.Y}");
        Assert.True(rect.X + rect.Width <= SecondaryArea.X + SecondaryArea.Width);
        Assert.True(rect.Y + rect.Height <= SecondaryArea.Y + SecondaryArea.Height);
    }

    // --- Longitud según el número de notas -----------------------------------------------------

    [Fact]
    public void WindowLength_WithZeroNotes_UsesMinContentPlusFooter()
    {
        Assert.Equal(EdgeGeometry.MinContentLength + EdgeGeometry.FooterLength, EdgeGeometry.WindowLength(0));
    }

    [Fact]
    public void WindowLength_WithFewNotes_GrowsOnePitchPerNote()
    {
        const int noteCount = 3;
        double tabs = noteCount * EdgeGeometry.TabPitch;
        Assert.True(tabs > EdgeGeometry.MinContentLength && tabs < EdgeGeometry.MaxContentLength,
            $"{noteCount} notas ({tabs}) deben caer entre el mínimo y el máximo para que este test pruebe algo");
        Assert.Equal(tabs + EdgeGeometry.FooterLength, EdgeGeometry.WindowLength(noteCount), precision: 3);
    }

    [Fact]
    public void WindowLength_WithManyNotes_CapsAtMaxContent()
    {
        Assert.Equal(EdgeGeometry.MaxContentLength + EdgeGeometry.FooterLength, EdgeGeometry.WindowLength(1000));
    }

    [Fact]
    public void MaxContentLength_IsAWholeNumberOfTabPitches()
    {
        // Si no lo fuera, la última pestaña que entra en el viewport inicial del ScrollViewer
        // quedaría cortada por la mitad desde el primer hover, antes de que nadie scrollee.
        double tabs = EdgeGeometry.MaxContentLength / EdgeGeometry.TabPitch;
        Assert.Equal(Math.Round(tabs), tabs, precision: 9);
    }

    [Fact]
    public void TabPitch_MatchesTheLayoutsOwnHeightPlusGap()
    {
        // El diseño anterior presupuestaba 88px por nota mientras el layout usaba un solape de
        // -28px sobre pestañas de 80 (paso real 52). Esta aserción existe para que geometría y
        // layout no puedan volver a discrepar en silencio.
        Assert.Equal(EdgeGeometry.TabHeight + EdgeGeometry.TabGap, EdgeGeometry.TabPitch);
    }

    [Fact]
    public void TabGap_IsPositive_SoTheRegionDoesNotFuseAdjacentTabs()
    {
        // La región se une con CombineRgn/RGN_OR: dos pestañas solapadas se funden en una sola
        // mancha y el abanico deja de leerse como pestañas separadas.
        Assert.True(EdgeGeometry.TabGap > 0);
    }

    // --- Anchos del abanico --------------------------------------------------------------------

    [Fact]
    public void TabWidth_FirstIsMin_LastIsMax()
    {
        Assert.Equal(EdgeGeometry.TabMinWidth, EdgeGeometry.TabWidth(0, 5));
        Assert.Equal(EdgeGeometry.TabMaxWidth, EdgeGeometry.TabWidth(4, 5));
    }

    [Fact]
    public void TabWidth_IsMonotonicallyIncreasing()
    {
        double previous = double.NegativeInfinity;
        for (int i = 0; i < 6; i++)
        {
            double width = EdgeGeometry.TabWidth(i, 6);
            Assert.True(width > previous, $"la pestaña {i} ({width}) no es más ancha que la anterior ({previous})");
            previous = width;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(30)]
    [InlineData(300)]
    public void TabWidth_NeverExceedsTheWindowThickness_AtAnyNoteCount(int noteCount)
    {
        // El fallo concreto de la fórmula anterior (32 + índice*14): no estaba acotada, así que a
        // partir de ~14 notas la pestaña era más ancha que la propia ventana.
        for (int i = 0; i < noteCount; i++)
        {
            double width = EdgeGeometry.TabWidth(i, noteCount);
            Assert.InRange(width, EdgeGeometry.TabMinWidth, EdgeGeometry.TabMaxWidth);
            Assert.True(width <= EdgeGeometry.WindowThickness,
                $"la pestaña {i} de {noteCount} mide {width}, más que el grosor {EdgeGeometry.WindowThickness}");
        }
    }

    [Fact]
    public void TabWidth_WithASingleNote_IsTheMinimum()
    {
        Assert.Equal(EdgeGeometry.TabMinWidth, EdgeGeometry.TabWidth(0, 1));
    }

    // --- Zona sensible en reposo ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Edges))]
    public void RestingVisibleRect_IsTheSliverFlushAgainstTheScreenEdge(EdgePosition edge)
    {
        var window = EdgeGeometry.WindowRect(Area, edge, noteCount: 4);
        var resting = EdgeGeometry.RestingVisibleRect(Area, edge, noteCount: 4);

        // Siempre dentro de la ventana, y del grosor de una tira.
        Assert.True(resting.X >= window.X && resting.Y >= window.Y);
        Assert.True(resting.X + resting.Width <= window.X + window.Width);
        Assert.True(resting.Y + resting.Height <= window.Y + window.Height);

        double thickness = edge is EdgePosition.Left or EdgePosition.Right ? resting.Width : resting.Height;
        Assert.Equal(EdgeGeometry.RestSliverWidth, thickness);
    }

    [Fact]
    public void RestingVisibleRect_Right_HugsTheOuterEdgeOfTheWindow()
    {
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Right, noteCount: 4);
        var resting = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, noteCount: 4);
        Assert.Equal(window.X + window.Width, resting.X + resting.Width);
    }

    [Fact]
    public void RestingVisibleRect_Left_HugsTheOuterEdgeOfTheWindow()
    {
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Left, noteCount: 4);
        var resting = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Left, noteCount: 4);
        Assert.Equal(window.X, resting.X);
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void RestingVisibleRect_StartsWithTheWindowButCoversOnlyTheTabStrip(EdgePosition edge)
    {
        const int noteCount = 4;
        var window = EdgeGeometry.WindowRect(Area, edge, noteCount);
        var resting = EdgeGeometry.RestingVisibleRect(Area, edge, noteCount);
        double strip = EdgeGeometry.TabStripLength(noteCount);

        if (edge is EdgePosition.Top or EdgePosition.Bottom)
        {
            Assert.Equal(window.X, resting.X);
            Assert.Equal(strip, resting.Width);
        }
        else
        {
            Assert.Equal(window.Y, resting.Y);
            Assert.Equal(strip, resting.Height);
        }
    }

    [Fact]
    public void RestingVisibleRect_DoesNotReachIntoTheFooterBand()
    {
        // En reposo el footer no se dibuja (su barrido vale 0). Si la zona sensible llegara hasta
        // el final de la ventana, habría una banda muerta donde el ratón despliega el dock sin
        // haber nada visible bajo el cursor. Verificado también contra la región real de la app:
        // en reposo termina exactamente donde acaba la última pestaña.
        const int noteCount = 4;
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Right, noteCount);
        var resting = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, noteCount);

        Assert.True(resting.Y + resting.Height < window.Y + window.Height);
        Assert.Equal(EdgeGeometry.FooterLength + EdgeGeometry.TabGap,
            (window.Y + window.Height) - (resting.Y + resting.Height));
    }

    [Fact]
    public void TabStripLength_HasNoTrailingGapAfterTheLastTab()
    {
        Assert.Equal(3 * EdgeGeometry.TabPitch - EdgeGeometry.TabGap, EdgeGeometry.TabStripLength(3));
    }

    [Fact]
    public void TabStripLength_WithNoNotes_IsZero()
    {
        Assert.Equal(0, EdgeGeometry.TabStripLength(0));
    }

    [Fact]
    public void TabStripLength_CapsWithTheScrollableContent()
    {
        Assert.Equal(EdgeGeometry.MaxContentLength - EdgeGeometry.TabGap, EdgeGeometry.TabStripLength(1000));
    }
}
