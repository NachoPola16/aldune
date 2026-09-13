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
        Assert.Equal(EdgeGeometry.HorizontalWindowWidth(area, EdgePosition.Top, 3), rect.Width);
        Assert.Equal(EdgeGeometry.WindowLength(area, EdgePosition.Top, 3), rect.Height);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public void WindowRect_Bottom_IsInsetFromBottomEdgeByMargin(WorkingArea area)
    {
        var rect = EdgeGeometry.WindowRect(area, EdgePosition.Bottom, noteCount: 3);
        Assert.Equal(area.Y + area.Height - EdgeGeometry.WindowLength(area, EdgePosition.Bottom, 3) - EdgeGeometry.EdgeMargin, rect.Y);
        Assert.Equal(EdgeGeometry.HorizontalWindowWidth(area, EdgePosition.Bottom, 3), rect.Width);
        Assert.Equal(EdgeGeometry.WindowLength(area, EdgePosition.Bottom, 3), rect.Height);
    }

    [Fact]
    public void TopAndBottomUseTheSameVerticalPreviewGeometryAsTheSideDock()
    {
        Assert.Equal(EdgeGeometry.TabHeight, EdgeGeometry.TabStripLength(Area, EdgePosition.Top, 1));
        Assert.Equal(
            2 * EdgeGeometry.TabHeight + EdgeGeometry.TabGap,
            EdgeGeometry.TabStripLength(Area, EdgePosition.Bottom, 2));
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Area, EdgePosition.Top, 2));
    }

    [Theory]
    [MemberData(nameof(Edges))]
    public void WindowRect_IsCenteredOnTheEdgesLengthAxis(EdgePosition edge)
    {
        var rect = EdgeGeometry.WindowRect(Area, edge, noteCount: 3);
        double length = EdgeGeometry.WindowLength(Area, edge, 3);

        if (edge is EdgePosition.Top or EdgePosition.Bottom)
        {
            double width = EdgeGeometry.HorizontalWindowWidth(Area, edge, 3);
            Assert.Equal(Area.X + (Area.Width - width) / 2, rect.X);
            Assert.Equal(width, rect.Width);
            Assert.Equal(length, rect.Height);
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

    // --- Solape: el abanico ocupa lo mismo haya las notas que haya -------------------------------

    private static readonly WorkingArea Vertical = new(-1440, -541, 1440, 2560);

    [Fact]
    public void Pitch_WithFewNotes_LeavesThemFullySeparated()
    {
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 4));
    }

    [Fact]
    public void Pitch_OverlapsOnceTheTallMonitorBudgetIsReached()
    {
        // El bug original: con el presupuesto atado solo a la fraccion de pantalla, en un monitor
        // de 2560px de alto las notas cabian sin solaparse y el abanico ocupaba media pantalla —
        // justo lo que el solape venia a evitar. El tope absoluto es lo que lo corrige.
        //
        // Con etiqueta horizontal la pestana bajo de 100 a 52px (dos lineas: titulo y vista
        // previa), asi que caben 7 notas sin solapar en vez de 4. La octava ya solapa, y muy poco:
        // la degradacion es gradual, no un salto.
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 20));
        Assert.True(EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 40) < EdgeGeometry.NaturalPitch,
            "al alcanzar el presupuesto del monitor alto las notas deben solaparse");
        Assert.True(EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 40) > EdgeGeometry.TabHeight,
            "el solape tiene que ser gradual, no un salto");
    }

    [Fact]
    public void Pitch_ShrinksAsNotesPileUp()
    {
        double few = EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 6);
        double many = EdgeGeometry.PitchFor(Area, EdgePosition.Right, 20);
        Assert.True(many < few, $"con 12 notas el paso ({many}) deberia ser menor que con 6 ({few})");
        Assert.True(few <= EdgeGeometry.NaturalPitch);
    }

    [Fact]
    public void Pitch_UsesNaturalCardsOnceTheLegibilityFloorWouldBeReached()
    {
        // Cuando el solape ya no puede conservar una franja legible, el exceso pasa al scroll y
        // las tarjetas vuelven a separarse como en el diseño normal.
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 1000));
    }

    [Fact]
    public void Pitch_WithASingleNote_IsNatural()
    {
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 1));
    }

    [Fact]
    public void TabStrip_StaysWithinTheScreenBudget_UntilScrollTakesOver()
    {
        // El punto del solape: mientras el paso pueda encogerse, el abanico ocupa lo mismo haya 5
        // notas o 12, en vez de crecer sin parar o de dejar las sobrantes sin dibujar.
        double budget = EdgeGeometry.FanBudget(Vertical, EdgePosition.Right);

        for (int n = 5; n <= 40; n++)
        {
            double pitch = EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, n);
            double strip = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, n);

            if (pitch > EdgeGeometry.MinPitch)
            {
                Assert.True(strip <= budget + 1,
                    $"con {n} notas el abanico mide {strip}, presupuesto {budget}");
            }
            else
            {
                // Al llegar al suelo de legibilidad, las tarjetas recuperan su paso natural y el
                // exceso se desplaza dentro del ScrollViewer.
                Assert.Equal(EdgeGeometry.NaturalPitch, pitch);
                Assert.True(strip > budget,
                    $"con {n} notas el abanico debe pasar al scroll");
            }
        }
    }

    [Fact]
    public void TabStrip_GrowsWithNoteCount_ButSublinearly()
    {
        double five = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, 5);
        double fifty = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, 50);
        Assert.True(fifty > five);
        Assert.True(fifty < five * 10, "solapando, 10 veces mas notas no pueden ocupar 10 veces mas");
    }

    [Fact]
    public void TabStrip_WithOneNote_IsExactlyOneTab()
    {
        Assert.Equal(EdgeGeometry.TabHeight, EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, 1));
    }

    [Fact]
    public void TabStrip_WithNoNotes_IsZero()
    {
        Assert.Equal(0, EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, 0));
    }

    [Fact]
    public void WindowLength_AlwaysLeavesRoomForTheFooter()
    {
        // Contra el abanico VISIBLE, no contra el total: desde que la ventana tiene tope, lo que
        // pasa del presupuesto vive fuera de la vista y se alcanza con scroll (ver FanBudget), asi
        // que comparar contra el total mediria algo que ya no esta dentro de la ventana.
        foreach (int n in new[] { 0, 1, 4, 12, 40 })
        {
            double window = EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n);
            double visible = EdgeGeometry.VisibleStripLength(Vertical, EdgePosition.Right, n);
            Assert.True(window - visible >= EdgeGeometry.FooterLength - 0.001,
                $"con {n} notas quedan {window - visible} para el footer");
        }
    }

    [Fact]
    public void WindowLength_WithNoNotes_StillHasAMinimumTarget()
    {
        // Incluye el aire de sombra: sin region que recorte, la sombra se dibuja fuera de la
        // pestana y necesita sitio dentro de la ventana o saldria cortada por el borde del HWND.
        Assert.Equal(
            EdgeGeometry.MinContentLength + EdgeGeometry.TabShadowHeadroom
                + EdgeGeometry.FooterLength + EdgeGeometry.ShadowMargin * 2,
            EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, 0));
    }

    // --- Ancho uniforme --------------------------------------------------------------------------

    [Fact]
    public void TabWidth_IsUniform_SoEveryNoteOpensWithTheSameHeader()
    {
        // La pestana viaja con la nota al abrirla y se convierte en su cabecera: anchos distintos
        // darian cabeceras distintas.
        Assert.True(EdgeGeometry.TabWidth > 0);
        Assert.True(EdgeGeometry.TabWidth < EdgeGeometry.WindowThickness);
    }

    [Fact]
    public void HorizontalLabel_NeedsFarLessBandThanAVerticalOne()
    {
        // El motivo de pasar a etiquetas horizontales: con solape la franja visible de cada pestana
        // es un paso, y MinPitch (32) da de sobra para una linea de texto. Una etiqueta vertical
        // necesitaba ~90px, asi que solo funcionaba sin solapar.
        Assert.True(EdgeGeometry.MinPitch >= 30);
        Assert.True(EdgeGeometry.MinPitch < EdgeGeometry.TabHeight);
    }

    [Fact]
    public void Preview_IsShownWhileTabsAreNotSquashed_AndHiddenOnceTheyAre()
    {
        // Media linea de vista previa asomando por debajo de la pestana siguiente parece un fallo
        // de render, no una decision: pasado ese punto se esconde entera y queda solo el titulo.
        Assert.True(EdgeGeometry.ShowsPreview(Vertical, EdgePosition.Right, 5));
        Assert.False(EdgeGeometry.ShowsPreview(Area, EdgePosition.Right, 20));
    }

    [Fact]
    public void PreviewThreshold_IsBelowTheTabHeight()
    {
        // Si fuera mayor, la vista previa se escondería incluso sin solape ninguno.
        Assert.True(EdgeGeometry.PreviewVisiblePitch < EdgeGeometry.NaturalPitch);
        Assert.True(EdgeGeometry.PreviewVisiblePitch > EdgeGeometry.MinPitch);
    }

    // --- Tira en reposo --------------------------------------------------------------------------

    [Fact]
    public void RestStrip_IsFarShorterThanTheFan()
    {
        const int noteCount = 6;
        Assert.True(EdgeGeometry.RestStripLength(noteCount)
            < EdgeGeometry.TabStripLength(Area, EdgePosition.Right, noteCount),
            "en reposo el dock insinua que hay notas, no ocupa lo que el abanico");
    }

    [Fact]
    public void RestDash_IsElongated_NotASquareChip()
    {
        // Con guiones casi cuadrados la tira se leia como un selector de color y no como el canto
        // de unas fichas. Comprobado renderizando tres proporciones en aislamiento.
        Assert.True(EdgeGeometry.RestDashLength > EdgeGeometry.RestDashWidth * 2);
    }

    [Fact]
    public void RestingVisibleRect_HugsTheOuterEdgeAndFitsInsideTheWindow()
    {
        const int noteCount = 6;
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Right, noteCount);
        var resting = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, noteCount);

        Assert.Equal(window.X + window.Width, resting.X + resting.Width);
        Assert.True(resting.Y >= window.Y);
        Assert.True(resting.Y + resting.Height <= window.Y + window.Height + 0.001);
        Assert.Equal(EdgeGeometry.RestSliverWidth, resting.Width);
    }

    [Fact]
    public void RestStripLength_HasNoTrailingGap()
    {
        Assert.Equal(3 * EdgeGeometry.RestPitch - EdgeGeometry.RestGap, EdgeGeometry.RestStripLength(3));
    }

    [Fact]
    public void RestStripLength_WithNoNotes_IsZero()
    {
        Assert.Equal(0, EdgeGeometry.RestStripLength(0));
    }

    // --- Desplazamiento reposo -> desplegado -----------------------------------------------------

    // --- Tope del abanico y de la tira de reposo ------------------------------------------------

    [Fact]
    public void WindowLength_StopsGrowing_OnceTheFanHitsItsBudget()
    {
        // El bug: el suelo de legibilidad (MinPitch) manda sobre el reparto, asi que con muchas
        // notas el abanico se pasaba del presupuesto y la ventana crecia con el — el dock dejaba de
        // ser compacto y podia salirse de la pantalla. Ahora se acota y lo que sobra se scrollea.
        double budget = EdgeGeometry.FanBudget(Vertical, EdgePosition.Right);
        double cap = budget + EdgeGeometry.TabShadowHeadroom + EdgeGeometry.FooterLength
            + EdgeGeometry.ShadowMargin * 2;

        foreach (int n in new[] { 20, 40, 100, 500 })
        {
            Assert.True(EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n) <= cap + 0.001,
                $"con {n} notas la ventana mide {EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n)}, tope {cap}");
        }
    }

    [Fact]
    public void WindowLength_WithFewNotes_StillFitsTheWholeFan()
    {
        // El tope no debe recortar cuando no hace falta: con pocas notas se sigue viendo entero.
        foreach (int n in new[] { 1, 2, 4 })
        {
            double strip = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, n);
            Assert.Equal(strip, EdgeGeometry.VisibleStripLength(Vertical, EdgePosition.Right, n));
        }
    }

    [Fact]
    public void RestDashes_NeverExceedWhatFitsInTheWindow()
    {
        // La tira de reposo no hace scroll: los guiones que no caben se recortarian contra el borde
        // de la ventana, y ya paso una vez que uno se veia pero caia fuera de la zona sensible.
        foreach (int n in new[] { 1, 5, 20, 60, 200 })
        {
            int dashes = EdgeGeometry.VisibleRestDashes(Vertical, EdgePosition.Right, n);
            double drawn = EdgeGeometry.RestStripLength(dashes) + EdgeGeometry.RestContainerPad * 2;

            Assert.True(dashes <= n, $"con {n} notas se dibujan {dashes} guiones");
            Assert.True(drawn <= EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n) + 0.001,
                $"con {n} notas la tira mide {drawn} en una ventana de {EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n)}");
        }
    }

    [Fact]
    public void RestDashes_NeverExceedTheSideContentViewport()
    {
        // En los laterales la tira está dentro de ContentGrid, que deja ShadowMargin arriba y
        // abajo. La ventana completa no representa el espacio que realmente puede pintar el rail.
        foreach (int n in new[] { 20, 40, 100, 200 })
        {
            int dashes = EdgeGeometry.VisibleRestDashes(Vertical, EdgePosition.Right, n);
            double drawn = EdgeGeometry.RestStripLength(dashes) + EdgeGeometry.RestContainerPad * 2;
            double viewport = EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n)
                - EdgeGeometry.ShadowMargin * 2;

            Assert.True(drawn <= viewport + 0.001,
                $"con {n} notas la tira mide {drawn} y el viewport real {viewport}");
        }
    }

    [Fact]
    public void RestDashes_WithFewNotes_ShowsThemAll()
    {
        foreach (int n in new[] { 0, 1, 4, 8 })
        {
            Assert.Equal(n, EdgeGeometry.VisibleRestDashes(Vertical, EdgePosition.Right, n));
        }
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void HorizontalRestStrip_FitsTheDashesItAdvertises(EdgePosition edge)
    {
        const int noteCount = 7;
        int dashes = EdgeGeometry.VisibleRestDashes(Area, edge, noteCount);
        double drawn = EdgeGeometry.RestStripLength(edge, dashes) + EdgeGeometry.RestContainerPad * 2;

        Assert.Equal(noteCount, dashes);
        Assert.True(drawn <= EdgeGeometry.WindowThickness);
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void HorizontalRestStrip_ShowsTenNotesWithoutClipping(EdgePosition edge)
    {
        Assert.Equal(10, EdgeGeometry.VisibleRestDashes(Area, edge, 10));
        Assert.True(
            EdgeGeometry.RestStripLength(edge, 10) + EdgeGeometry.RestContainerPad * 2
                <= EdgeGeometry.HorizontalWindowWidth(Area, edge, 10));
    }

    [Theory]
    [InlineData(EdgePosition.Top)]
    [InlineData(EdgePosition.Bottom)]
    public void HorizontalDockWidthGrowsWithTheNumberOfNotes(EdgePosition edge)
    {
        double expectedThree = Math.Max(
            EdgeGeometry.WindowThickness,
            EdgeGeometry.RestStripLength(edge, 3) + 2 * EdgeGeometry.RestContainerPad);
        Assert.Equal(expectedThree, EdgeGeometry.HorizontalWindowWidth(Area, edge, 3));
        Assert.True(
            EdgeGeometry.HorizontalWindowWidth(Area, edge, 10)
                > EdgeGeometry.HorizontalWindowWidth(Area, edge, 3));
    }

    [Fact]
    public void RestingVisibleRect_MatchesTheDashesActuallyDrawn()
    {
        // La zona sensible tiene que coincidir con lo que se ve: ni franja muerta que responde al
        // raton, ni guion visible que no responde.
        int dashes = EdgeGeometry.VisibleRestDashes(Vertical, EdgePosition.Right, 200);
        double expected = EdgeGeometry.RestStripLength(dashes) + EdgeGeometry.RestContainerPad * 2;

        var rect = EdgeGeometry.RestingVisibleRect(Vertical, EdgePosition.Right, 200);

        Assert.Equal(expected, rect.Height, 3);
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

}
