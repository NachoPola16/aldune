using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteColorAssignerTests
{
    private static readonly NoteTheme Theme = new()
    {
        Id = "t", Name = "T",
        DarkColors = ["#262F47", "#26323E", "#462527"],
        LightColors = ["#EBE6D9", "#DEE8F0", "#DEEADE"],
    };

    private static string Assign(NoteColorAssignment rule, NoteTone tone, params string[] dock) =>
        NoteColorAssigner.Assign(Theme, tone, rule, fixedColor: null, dock);

    [Fact]
    public void EmptyDock_GivesTheFirstColorOfTheTone()
    {
        Assert.Equal("#EBE6D9", Assign(NoteColorAssignment.Rotate, NoteTone.Light));
        Assert.Equal("#262F47", Assign(NoteColorAssignment.RotateAvoidNeighbors, NoteTone.Dark));
        Assert.Equal("#262F47", Assign(NoteColorAssignment.MostDistinct, NoteTone.Dark));
    }

    [Fact]
    public void Rotate_FollowsTheLastColorOfThatToneInTheDock()
    {
        Assert.Equal("#DEEADE", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#EBE6D9", "#DEE8F0"));
        Assert.Equal("#EBE6D9", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#DEEADE"));
    }

    [Fact]
    public void Rotate_IgnoresColorsFromOutsideTheTheme()
    {
        Assert.Equal("#DEE8F0", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#EBE6D9", "#123456"));
    }

    [Fact]
    public void Rotate_ComparesHexWithoutCase()
    {
        Assert.Equal("#DEE8F0", Assign(NoteColorAssignment.Rotate, NoteTone.Light, "#ebe6d9"));
    }

    [Fact]
    public void AvoidNeighbors_SkipsTheColorsOfTheLastTwoNotes()
    {
        // Rotar daría #DEE8F0 (tras #EBE6D9), pero es la penúltima: salta a #DEEADE.
        Assert.Equal("#DEEADE",
            Assign(NoteColorAssignment.RotateAvoidNeighbors, NoteTone.Light, "#DEE8F0", "#EBE6D9"));
    }

    [Fact]
    public void AvoidNeighbors_AfterDeletingNotes_DoesNotRepeatTheOneNextToIt()
    {
        // Antes: existing % Colors.Length. Con 3 notas y la del medio borrada, la cuenta daba el
        // mismo color que la última. Ahora manda el color de las vecinas, no el número de notas.
        Assert.NotEqual("#DEE8F0",
            Assign(NoteColorAssignment.RotateAvoidNeighbors, NoteTone.Light, "#EBE6D9", "#DEE8F0"));
    }

    [Fact]
    public void AvoidNeighbors_WithTooFewColors_FallsBackToRotateWithoutLooping()
    {
        var tiny = new NoteTheme { Id = "x", Name = "X", LightColors = ["#EBE6D9", "#DEE8F0"] };

        var color = NoteColorAssigner.Assign(tiny, NoteTone.Light, NoteColorAssignment.RotateAvoidNeighbors,
            null, ["#DEE8F0", "#EBE6D9"]);

        Assert.Equal("#DEE8F0", color);
    }

    [Fact]
    public void MostDistinct_PicksTheCandidateFarthestFromEverythingInTheDock()
    {
        // Con Tinta y Pizarra (fríos) en el dock, Burdeos (cálido) es el más distinto.
        Assert.Equal("#462527",
            Assign(NoteColorAssignment.MostDistinct, NoteTone.Dark, "#262F47", "#26323E"));
    }

    [Fact]
    public void Fixed_UsesTheFixedColorOrTheFirstCandidate()
    {
        Assert.Equal("#DEEADE", NoteColorAssigner.Assign(Theme, NoteTone.Light, NoteColorAssignment.Fixed, "#deeade", []));
        Assert.Equal("#EBE6D9", NoteColorAssigner.Assign(Theme, NoteTone.Light, NoteColorAssignment.Fixed, null, []));
        Assert.Equal("#EBE6D9", NoteColorAssigner.Assign(Theme, NoteTone.Light, NoteColorAssignment.Fixed, "bad", []));
    }

    [Fact]
    public void Fixed_WithAColorFromAnotherTheme_FallsBackToTheFirstCandidate()
    {
        // Se eligió Tinta como color fijo en Sereno y luego se cambió a un tema sin ella: seguir
        // creando notas Tinta sería un color de un tema que el usuario acaba de dejar.
        var graphite = NoteThemes.Resolve(NoteThemes.GraphiteId, null);

        var color = NoteColorAssigner.Assign(graphite, NoteTone.Dark, NoteColorAssignment.Fixed, "#262F47", []);

        Assert.Equal("#2F2D2C", color);
    }

    [Fact]
    public void Fixed_WithAColorOfTheTheme_KeepsItRegardlessOfCase()
    {
        Assert.Equal("#DEE8F0", NoteColorAssigner.Assign(Theme, NoteTone.Dark, NoteColorAssignment.Fixed, "#dee8f0", []));
    }

    [Fact]
    public void Both_AlternatesWithTheLastNote_StartingDark()
    {
        Assert.True(NoteColorDerivation.IsDark(Assign(NoteColorAssignment.Rotate, NoteTone.Both)));
        Assert.False(NoteColorDerivation.IsDark(Assign(NoteColorAssignment.Rotate, NoteTone.Both, "#262F47")));
        Assert.True(NoteColorDerivation.IsDark(Assign(NoteColorAssignment.Rotate, NoteTone.Both, "#EBE6D9")));
    }

    [Fact]
    public void MissingTone_UsesTheOtherList()
    {
        var classic = NoteThemes.Resolve(NoteThemes.ClassicId, null);

        var color = NoteColorAssigner.Assign(classic, NoteTone.Dark, NoteColorAssignment.Rotate, null, []);

        Assert.Equal("#EBD38B", color);
    }
}
