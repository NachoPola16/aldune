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
    public void Pitch_ShrinksAsNotesPileUp()
    {
        double few = EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 20);
        double many = EdgeGeometry.PitchFor(Vertical, EdgePosition.Right, 40);
        Assert.True(many < few, $"con 40 notas el paso ({many}) deberia ser menor que con 20 ({few})");
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
        // El punto del solape: entre 5 y 20 notas el abanico ocupa aproximadamente lo mismo, en vez
        // de crecer sin parar o de dejar las sobrantes sin dibujar.
        double budget = Vertical.Height * EdgeGeometry.MaxScreenFraction - EdgeGeometry.FooterLength;
        for (int n = 5; n <= 20; n++)
        {
            double strip = EdgeGeometry.TabStripLength(Vertical, EdgePosition.Right, n);
            Assert.True(strip <= budget + 1, $"con {n} notas el abanico mide {strip}, presupuesto {budget}");
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
        Assert.Equal(EdgeGeometry.MinContentLength + EdgeGeometry.FooterLength,
            EdgeGeometry.WindowLength(Vertical, EdgePosition.Right, 0));
    }

    // --- Ancho uniforme y lomo ------------------------------------------------------------------

    [Fact]
    public void TabWidth_IsUniform_SoEveryNoteOpensWithTheSameSpine()
    {
        // Era 32 + indice*14: sin acotar (a partir de ~14 notas la pestana era mas ancha que la
        // ventana) y, sobre todo, incoherente con el modelo nuevo — la pestana viaja con la nota
        // como lomo al abrirla, asi que una escalera daria a cada nota un lomo distinto.
        Assert.True(EdgeGeometry.TabWidth <= EdgeGeometry.WindowThickness);
    }

    [Fact]
    public void SpineWidth_IsTheTabMinusThePartThatStaysShowingAtRest()
    {
        Assert.Equal(EdgeGeometry.TabWidth - EdgeGeometry.PerforationInset, EdgeGeometry.SpineWidth);
    }

    [Fact]
    public void RestClip_LeavesGroundOnBothSidesOfTheDash()
    {
        // El recorte horizontal lo hace la pestana (UIElement.Clip), no la region: en reposo la
        // region ES el contenedor, asi que una pestana sin recortar pinta sus 104px enteros por
        // detras y el color llena la pastilla de lado a lado, sin marco. Fue exactamente lo que
        // se vio en la app real.
        double dashLeft = EdgeGeometry.RestClipLeft;
        double dashRight = dashLeft + EdgeGeometry.RestDashWidth;

        // En coordenadas de la propia pestana, el canto de la ventana cae en TabWidth.
        Assert.Equal(EdgeGeometry.TabWidth - EdgeGeometry.RestDashInset, dashRight);
        Assert.True(dashLeft > 0, "el recorte tiene que dejar fuera la etiqueta, que vive a la izquierda");
    }

    [Fact]
    public void RestDash_SitsInsideItsContainer_OnBothSides()
    {
        // El contenedor tiene que enmarcar el guion, no coincidir con el: esos pocos pixeles de
        // fondo a cada lado son lo que hace que la tira se lea como un objeto y no como manchas.
        Assert.True(EdgeGeometry.RestContainerWidth > EdgeGeometry.RestDashWidth);
        Assert.True(EdgeGeometry.RestDashInset > EdgeGeometry.RestContainerInset);
    }

    [Fact]
    public void RestScale_SquashesTabsEnoughToNotOverlapInTheStrip()
    {
        // Sin escalar, una pestana de 100px con paso de reposo 32 se solapa con la siguiente y
        // tapa el fondo del contenedor: no habria guiones separados, sino una mancha continua.
        double rendered = EdgeGeometry.TabHeight * EdgeGeometry.RestScaleFor();
        Assert.Equal(EdgeGeometry.RestDashLength, rendered, precision: 9);
        Assert.True(rendered < EdgeGeometry.RestPitch,
            $"una pestana renderizada mide {rendered} y el paso es {EdgeGeometry.RestPitch}");
    }

    [Fact]
    public void TabHeight_LeavesRoomForTheDefaultNoteTitle()
    {
        // "NUEVA NOTA" son 10 caracteres, y el alto de la pestana ES el ancho disponible para la
        // etiqueta girada. Con 80 no cabia y salia cortada; esta cota lo deja anclado.
        Assert.True(EdgeGeometry.TabHeight >= 100);
    }

    // --- Tira en reposo --------------------------------------------------------------------------

    [Fact]
    public void RestStripLength_IsFarShorterThanTheExpandedStrip()
    {
        // El punto entero del cambio: en reposo el dock insinua que hay notas en vez de ocupar el
        // borde entero de la pantalla.
        const int noteCount = 4;
        Assert.True(EdgeGeometry.RestStripLength(noteCount) < EdgeGeometry.TabStripLength(Area, EdgePosition.Right, noteCount) / 2,
            $"reposo {EdgeGeometry.RestStripLength(noteCount)} frente a desplegado {EdgeGeometry.TabStripLength(Area, EdgePosition.Right, noteCount)}");
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

    [Fact]
    public void RestStripLength_HasOneDashPerNote_EvenBeyondTheScrollCap()
    {
        // El abanico desplegado solapa las pestanas para que quepan; en reposo no hay
        // scroll: el desplazamiento de reposo trae todas las notas a la tira, asi que todas tienen
        // guion. Aplicar aqui aquel tope dejaba los guiones sobrantes visibles pero fuera de la
        // zona sensible al raton — se veian y no se podian pulsar (bug real, encontrado sondeando
        // la region de la app con 5 notas).
        const int beyondCap = 12;
        Assert.Equal(beyondCap * EdgeGeometry.RestPitch - EdgeGeometry.RestGap,
            EdgeGeometry.RestStripLength(beyondCap));
    }

    [Fact]
    public void RestingVisibleRect_CoversEveryDash_EvenBeyondTheScrollCap()
    {
        // La comprobacion que de verdad importa: el ultimo guion tiene que caer dentro de la zona
        // sensible, o se ve y no responde.
        const int beyondCap = 12;
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Right, beyondCap);
        var resting = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, beyondCap);

        double lastDashBottom = window.Y + EdgeGeometry.RestStripStart(Area, EdgePosition.Right, beyondCap)
            + (beyondCap - 1) * EdgeGeometry.RestPitch + EdgeGeometry.RestDashLength;

        Assert.True(lastDashBottom <= resting.Y + resting.Height,
            $"el ultimo guion acaba en {lastDashBottom}, la zona sensible en {resting.Y + resting.Height}");
    }

    [Fact]
    public void RestStripStart_IsNeverNegative()
    {
        // Un inicio negativo recortaria las PRIMERAS notas contra el borde superior de la ventana.
        Assert.True(EdgeGeometry.RestStripStart(Area, EdgePosition.Right, 1000) >= 0);
    }

    [Fact]
    public void RestStrip_IsCenteredWithinTheWindow()
    {
        // Solo mientras quepa: si la tira es mas larga que la ventana se pega arriba a proposito.
        const int noteCount = 4;
        double start = EdgeGeometry.RestStripStart(Area, EdgePosition.Right, noteCount);
        double end = start + EdgeGeometry.RestStripLength(noteCount);
        double window = EdgeGeometry.WindowLength(Area, EdgePosition.Right, noteCount);
        Assert.Equal(start, window - end, precision: 9);
    }

    // --- Desplazamiento reposo -> desplegado -----------------------------------------------------

    [Fact]
    public void RestOffset_PutsEachTabsCentreOnItsOwnDash()
    {
        // Sin esto, la banda que la region deja ver para la nota 2 caeria sobre pixeles de la
        // nota 1 y los colores saldrian cambiados: reposo y desplegado usan pasos distintos.
        const int noteCount = 5;
        for (int i = 0; i < noteCount; i++)
        {
            double tabCentre = i * EdgeGeometry.PitchFor(Area, EdgePosition.Right, noteCount)
                + EdgeGeometry.TabHeight / 2;
            double dashCentre = EdgeGeometry.RestStripStart(Area, EdgePosition.Right, noteCount)
                + i * EdgeGeometry.RestPitch + EdgeGeometry.RestDashLength / 2;
            Assert.Equal(dashCentre,
                tabCentre + EdgeGeometry.RestOffsetFor(Area, EdgePosition.Right, i, noteCount), precision: 9);
        }
    }

    [Fact]
    public void RestOffset_KeepsTheDashInsideItsOwnTab()
    {
        // La region deja ver una banda centrada en la pestana ya desplazada. Si el desplazamiento
        // sacara esa banda fuera del alto de la pestana, se verian pixeles del fondo o de la nota
        // vecina en lugar del color propio.
        const int noteCount = 4;
        for (int i = 0; i < noteCount; i++)
        {
            double offset = EdgeGeometry.RestOffsetFor(Area, EdgePosition.Right, i, noteCount);
            double tabTop = i * EdgeGeometry.PitchFor(Area, EdgePosition.Right, noteCount) + offset;
            double tabCentre = tabTop + EdgeGeometry.TabHeight / 2;
            double dashTop = tabCentre - EdgeGeometry.RestDashLength / 2;
            double dashBottom = tabCentre + EdgeGeometry.RestDashLength / 2;

            Assert.True(dashTop >= tabTop, $"nota {i}: el guion empieza por encima de su pestana");
            Assert.True(dashBottom <= tabTop + EdgeGeometry.TabHeight,
                $"nota {i}: el guion acaba por debajo de su pestana");
        }
    }

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

    [Theory]
    [MemberData(nameof(Edges))]
    public void RestingVisibleRect_StartsWithTheWindowButCoversOnlyTheTabStrip(EdgePosition edge)
    {
        const int noteCount = 4;
        var window = EdgeGeometry.WindowRect(Area, edge, noteCount);
        var resting = EdgeGeometry.RestingVisibleRect(Area, edge, noteCount);
        double strip = EdgeGeometry.RestStripLength(noteCount);
        double start = EdgeGeometry.RestStripStart(Area, edge, noteCount);

        if (edge is EdgePosition.Top or EdgePosition.Bottom)
        {
            Assert.Equal(window.X + start, resting.X);
            Assert.Equal(strip, resting.Width);
        }
        else
        {
            Assert.Equal(window.Y + start, resting.Y);
            Assert.Equal(strip, resting.Height);
        }
    }

    [Fact]
    public void RestingVisibleRect_IsInsetAtBothEndsOfTheWindow()
    {
        // La tira de reposo va centrada y es mucho mas corta que la ventana, asi que no puede
        // tocar ninguno de los dos extremos — ni la banda del footer, que en reposo no se dibuja.
        const int noteCount = 4;
        var window = EdgeGeometry.WindowRect(Area, EdgePosition.Right, noteCount);
        var resting = EdgeGeometry.RestingVisibleRect(Area, EdgePosition.Right, noteCount);

        Assert.True(resting.Y > window.Y);
        Assert.True(resting.Y + resting.Height < window.Y + window.Height);
    }

}
