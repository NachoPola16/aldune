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
        double length = EdgeGeometry.WindowLength(Area, edge, 3);

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

    // --- Solape: el abanico ocupa lo mismo haya las notas que haya -------------------------------

    private static readonly WorkingArea Vertical = new(-1440, -541, 1440, 2560);

    [Fact]
    public void Pitch_WithFewNotes_LeavesThemFullySeparated()
    {
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 4));
    }

    [Fact]
    public void Pitch_OverlapsOnceThereAreEnoughNotes_EvenOnATallMonitor()
    {
        // El bug original: con el presupuesto atado solo a la fraccion de pantalla, en un monitor
        // de 2560px de alto las notas cabian sin solaparse y el abanico ocupaba media pantalla —
        // justo lo que el solape venia a evitar. El tope absoluto es lo que lo corrige.
        //
        // Con etiqueta horizontal la pestana bajo de 100 a 52px (dos lineas: titulo y vista
        // previa), asi que caben 7 notas sin solapar en vez de 4. La octava ya solapa, y muy poco:
        // la degradacion es gradual, no un salto.
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 7));
        Assert.True(EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 8) < EdgeGeometry.NaturalPitch,
            "con ocho notas ya deberian solaparse, por alto que sea el monitor");
        Assert.True(EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 8) > EdgeGeometry.TabHeight,
            "y con ocho el solape tiene que ser leve, no un salto");
    }

    [Fact]
    public void TabStrip_NeverExceedsTheAbsoluteCap_UntilTheLegibilityFloorBites()
    {
        for (int n = 1; n <= 14; n++)
        {
            double strip = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, n);
            Assert.True(strip <= EdgeGeometry.MaxFanLength + 1,
                $"con {n} notas el abanico mide {strip}, tope {EdgeGeometry.MaxFanLength}");
        }
    }

    [Fact]
    public void Pitch_ShrinksAsNotesPileUp()
    {
        double few = EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 6);
        double many = EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 12);
        Assert.True(many < few, $"con 12 notas el paso ({many}) deberia ser menor que con 6 ({few})");
        Assert.True(few <= EdgeGeometry.NaturalPitch);
    }

    [Fact]
    public void Pitch_NeverGoesBelowTheLegibilityFloor()
    {
        // Lo que queda visible de cada pestana es un paso, y por tanto cuanta etiqueta se lee.
        Assert.Equal(EdgeGeometry.MinPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 1000));
    }

    [Fact]
    public void Pitch_WithASingleNote_IsNatural()
    {
        Assert.Equal(EdgeGeometry.NaturalPitch, EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 1));
    }

    [Fact]
    public void TabStrip_StaysWithinTheScreenBudget_UntilTheFloorBites()
    {
        // El punto del solape: mientras el paso pueda encogerse, el abanico ocupa lo mismo haya 5
        // notas o 12, en vez de crecer sin parar o de dejar las sobrantes sin dibujar.
        double budget = Math.Min(Vertical.Height * EdgeGeometry.MaxScreenFraction,
            EdgeGeometry.MaxFanLength) - EdgeGeometry.FooterLength;

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
                // Pasado el suelo de legibilidad manda el suelo, no el presupuesto: preferimos
                // que el abanico se pase de largo (y scrollee) antes que dejar las pestanas tan
                // juntas que no se lea cual es cual.
                Assert.Equal(EdgeGeometry.MinPitch, pitch);
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
        foreach (int n in new[] { 0, 1, 4, 12, 40 })
        {
            double window = EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, n);
            double strip = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, n);
            Assert.True(window - strip >= EdgeGeometry.FooterLength - 0.001,
                $"con {n} notas quedan {window - strip} para el footer");
        }
    }

    [Fact]
    public void WindowLength_WithNoNotes_StillHasAMinimumTarget()
    {
        // Incluye el aire de sombra: sin region que recorte, la sombra se dibuja fuera de la
        // pestana y necesita sitio dentro de la ventana o saldria cortada por el borde del HWND.
        Assert.Equal(
            EdgeGeometry.MinContentLength + EdgeGeometry.FooterLength + EdgeGeometry.ShadowMargin * 2,
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
        // es un paso, y MinPitch (24) da de sobra para una linea de texto. Una etiqueta vertical
        // necesitaba ~90px, asi que solo funcionaba sin solapar.
        Assert.True(EdgeGeometry.MinPitch >= 20);
        Assert.True(EdgeGeometry.MinPitch < EdgeGeometry.TabHeight);
    }

    [Fact]
    public void Preview_IsShownWhileTabsAreNotSquashed_AndHiddenOnceTheyAre()
    {
        // Media linea de vista previa asomando por debajo de la pestana siguiente parece un fallo
        // de render, no una decision: pasado ese punto se esconde entera y queda solo el titulo.
        Assert.True(EdgeGeometry.ShowsPreview(Vertical, EdgePosition.Right, 5));
        Assert.False(EdgeGeometry.ShowsPreview(Vertical, EdgePosition.Right, 40));
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

    // --- Origen del deslizamiento al abrir una nota ---------------------------------------------

    [Fact]
    public void SlideOrigin_NeverPutsTheNoteOnTheNextMonitor()
    {
        // El bug reportado: el monitor vertical del usuario ocupa x -1440..0, asi que su canto
        // derecho linda con el principal. Arrancar en la X de la pestana (-104) con una nota de
        // 348 la dibujaba de x=0 a 244, encima de la otra pantalla.
        var vertical = new WorkingArea(-1440, -541, 1440, 2560);
        double origin = EdgeGeometry.SlideOriginFor(vertical, tabX: -104, noteLeft: -444, noteWidth: 348);

        Assert.True(origin + 348 <= vertical.X + vertical.Width,
            $"la nota arranca en {origin} y su borde derecho cae en {origin + 348}");
    }

    [Fact]
    public void SlideOrigin_StaysToTheRightOfTheDestination()
    {
        // La nota se desliza hacia la izquierda: un origen por detras del destino la haria ir al
        // reves. Caso limite: una nota mas ancha que el area de trabajo.
        var narrow = new WorkingArea(0, 0, 300, 1000);
        double origin = EdgeGeometry.SlideOriginFor(narrow, tabX: 260, noteLeft: 100, noteWidth: 348);
        Assert.Equal(100, origin);
    }

    [Fact]
    public void SlideOrigin_UsesTheTabWhenThereIsRoomForIt()
    {
        var wide = new WorkingArea(0, 0, 2560, 1440);
        double origin = EdgeGeometry.SlideOriginFor(wide, tabX: 1800, noteLeft: 1500, noteWidth: 348);
        Assert.Equal(1800, origin);
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
