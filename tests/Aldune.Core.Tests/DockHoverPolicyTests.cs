using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class DockHoverPolicyTests
{
    // Una muestra por sondeo del dock (cada 50 ms), como en la ventana.
    private const int Tick = 50;

    private static DockHoverSample Sample(
        int ms,
        bool expanded,
        bool rest = false,
        bool zone = false,
        bool target = false,
        bool edge = false,
        bool button = false,
        double x = 0,
        double y = 0) =>
        new(TimeSpan.FromMilliseconds(ms), x, y, expanded, rest, zone, target, edge, button);

    /// <summary>Alimenta muestras cada <see cref="Tick"/> ms y devuelve cuándo decide algo.</summary>
    private static (DockHoverDecision decision, int at) Run(
        DockHoverPolicy policy, int from, int to, Func<int, DockHoverSample> sample)
    {
        for (int t = from; t <= to; t += Tick)
        {
            var decision = policy.Update(sample(t));
            if (decision != DockHoverDecision.None) return (decision, t);
        }
        return (DockHoverDecision.None, -1);
    }

    // --- Abrir ----------------------------------------------------------------------------------

    [Fact]
    public void PressingAgainstPhysicalEdge_OpensAtOnce()
    {
        var policy = new DockHoverPolicy(DockHoverTuning.PhysicalEdge);
        Assert.Equal(DockHoverDecision.Open, policy.Update(Sample(0, expanded: false, rest: true, edge: true)));
    }

    [Fact]
    public void RestingInStrip_OpensOnlyAfterDwell()
    {
        var policy = new DockHoverPolicy(DockHoverTuning.PhysicalEdge);
        var (decision, at) = Run(policy, 0, 1000, t => Sample(t, expanded: false, rest: true, x: 10, y: 100));
        Assert.Equal(DockHoverDecision.Open, decision);
        Assert.Equal(DockHoverTuning.PhysicalEdge.RestDwell.TotalMilliseconds, at);
    }

    [Fact]
    public void SharedEdge_WaitsLongerAndIgnoresEdgePress()
    {
        var policy = new DockHoverPolicy(DockHoverTuning.SharedEdge);
        var (decision, at) = Run(policy, 0, 1000, t => Sample(t, expanded: false, rest: true, edge: true, x: 10, y: 100));
        Assert.Equal(DockHoverDecision.Open, decision);
        Assert.Equal(DockHoverTuning.SharedEdge.RestDwell.TotalMilliseconds, at);
        Assert.True(DockHoverTuning.SharedEdge.RestDwell > DockHoverTuning.PhysicalEdge.RestDwell);
    }

    [Fact]
    public void BriefPassThroughStrip_DoesNotOpen()
    {
        // Cruzar hacia la otra pantalla o ir a la barra de scroll: dos sondeos dentro y fuera.
        var policy = new DockHoverPolicy(DockHoverTuning.PhysicalEdge);
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(0, false, rest: true, y: 100)));
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(50, false, rest: true, y: 102)));
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(100, false, rest: false, y: 104)));
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(150, false, rest: true, y: 104)));
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(200, false, rest: true, y: 104)));
    }

    [Fact]
    public void TravellingAlongTheStripFast_DoesNotOpen()
    {
        // Recorrer el canto de arriba abajo sin detenerse: está dentro todo el rato, pero de paso.
        var policy = new DockHoverPolicy(DockHoverTuning.PhysicalEdge);
        var (decision, _) = Run(policy, 0, 500, t => Sample(t, false, rest: true, x: 10, y: t * 2));
        Assert.Equal(DockHoverDecision.None, decision);
    }

    [Fact]
    public void SlowingDownInTheStrip_StartsTheDwellThere()
    {
        var policy = new DockHoverPolicy(DockHoverTuning.PhysicalEdge);
        Run(policy, 0, 200, t => Sample(t, false, rest: true, x: 10, y: t * 2));
        var (decision, at) = Run(policy, 250, 1000, t => Sample(t, false, rest: true, x: 10, y: 400));
        Assert.Equal(DockHoverDecision.Open, decision);
        Assert.True(at >= 250 + DockHoverTuning.PhysicalEdge.RestDwell.TotalMilliseconds - Tick);
    }

    [Fact]
    public void ButtonHeld_NeverOpens()
    {
        // Arrastrar otra cosa (la barra de scroll de una ventana) por el mismo canto.
        var policy = new DockHoverPolicy(DockHoverTuning.PhysicalEdge);
        var (decision, _) = Run(policy, 0, 1000, t => Sample(t, false, rest: true, edge: true, button: true));
        Assert.Equal(DockHoverDecision.None, decision);
    }

    // --- Cerrar ---------------------------------------------------------------------------------

    private static DockHoverPolicy Opened(DockHoverTuning tuning, bool engaged)
    {
        var policy = new DockHoverPolicy(tuning);
        policy.Update(Sample(0, false, rest: true, edge: true));
        policy.Update(Sample(50, true, zone: true, target: engaged));
        return policy;
    }

    [Fact]
    public void OpenedButNeverUsed_ClosesQuickly()
    {
        // Una apertura sin querer no se vuelve más pegajosa por la espera larga.
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: false);
        var (decision, at) = Run(policy, 100, 2000, t => Sample(t, true));
        Assert.Equal(DockHoverDecision.Close, decision);
        Assert.Equal(100 + DockHoverTuning.QuickCloseDelay.TotalMilliseconds, at);
    }

    [Fact]
    public void AfterUsingIt_WaitsLongerToClose()
    {
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: true);
        var (decision, at) = Run(policy, 100, 2000, t => Sample(t, true));
        Assert.Equal(DockHoverDecision.Close, decision);
        Assert.Equal(100 + DockHoverTuning.EngagedCloseDelay.TotalMilliseconds, at);
    }

    [Fact]
    public void OvershootAndComeBack_StaysOpen()
    {
        // Pasarse 15 px durante 250 ms y volver a la pestaña (S2 de la sonda).
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: true);
        Assert.Equal(DockHoverDecision.None, Run(policy, 100, 350, t => Sample(t, true)).decision);
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(400, true, zone: true, target: true)));
        Assert.Equal(DockHoverDecision.None, Run(policy, 450, 800, t => Sample(t, true)).decision);
    }

    [Fact]
    public void ComingBackShortlyAfterClosing_ReopensWithoutGoingBackToTheEdge()
    {
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: true);
        var (_, closedAt) = Run(policy, 100, 2000, t => Sample(t, true));
        var reopen = policy.Update(Sample(closedAt + 300, false, zone: true));
        Assert.Equal(DockHoverDecision.Open, reopen);
    }

    [Fact]
    public void ComingBackLongAfterClosing_NeedsTheStripAgain()
    {
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: true);
        var (_, closedAt) = Run(policy, 100, 2000, t => Sample(t, true));
        int late = closedAt + (int)DockHoverTuning.RecoverWindow.TotalMilliseconds + Tick;
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(late, false, zone: true)));
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(late + Tick, false, zone: true)));
    }

    [Fact]
    public void AnAccidentalOpening_IsNotRecoverable()
    {
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: false);
        var (_, closedAt) = Run(policy, 100, 2000, t => Sample(t, true));
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(closedAt + 100, false, zone: true)));
    }

    [Fact]
    public void Hold_CountsAsUseAndRestartsTheWait()
    {
        // Tras un menú, un margen de cortesía o crear una nota: al soltarlo, espera la de "usado".
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: false);
        policy.Hold();
        var (decision, at) = Run(policy, 1000, 3000, t => Sample(t, true));
        Assert.Equal(DockHoverDecision.Close, decision);
        Assert.Equal(1000 + DockHoverTuning.EngagedCloseDelay.TotalMilliseconds, at);
        Assert.True(policy.IsPointerInside is false);
    }

    [Fact]
    public void ButtonPressedInside_KeepsItOpenWhileDraggingOut()
    {
        // Arrastrar la barra de scroll del propio dock y salirse de la zona con el botón pulsado.
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: true);
        policy.Update(Sample(100, true, zone: true, button: true));
        var (decision, _) = Run(policy, 150, 3000, t => Sample(t, true, button: true));
        Assert.Equal(DockHoverDecision.None, decision);
    }

    [Fact]
    public void ButtonPressedOutside_DoesNotHoldItOpen()
    {
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: false);
        var (decision, _) = Run(policy, 100, 2000, t => Sample(t, true, button: true));
        Assert.Equal(DockHoverDecision.Close, decision);
    }

    [Fact]
    public void ExternallyCollapsed_ForgetsUseAndRecovery()
    {
        var policy = Opened(DockHoverTuning.PhysicalEdge, engaged: true);
        policy.Collapsed();
        Assert.False(policy.IsPointerInside);
        Assert.Equal(DockHoverDecision.None, policy.Update(Sample(200, false, zone: true)));
    }

    [Fact]
    public void Tuning_KeepsTheSensibleOrder()
    {
        // Lo mínimo que tiene que seguir siendo cierto si alguien retoca los números.
        Assert.True(DockHoverTuning.QuickCloseDelay < DockHoverTuning.EngagedCloseDelay);
        Assert.True(DockHoverTuning.PhysicalEdge.RestDwell <= TimeSpan.FromMilliseconds(200));
        Assert.True(DockHoverTuning.PhysicalEdge.EdgePressOpens);
        Assert.False(DockHoverTuning.SharedEdge.EdgePressOpens);
    }
}
