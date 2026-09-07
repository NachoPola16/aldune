using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class NoteOrderingTests
{
    // --- Posición al soltar entre dos vecinas ---------------------------------------------------

    [Fact]
    public void Between_TwoNeighbours_IsTheMidpoint()
    {
        Assert.Equal(1500, NoteOrdering.Between(1000, 2000));
    }

    [Fact]
    public void Between_AtTheStart_GoesBeforeTheFirst()
    {
        var position = NoteOrdering.Between(null, 1000);
        Assert.NotNull(position);
        Assert.True(position < 1000);
    }

    [Fact]
    public void Between_AtTheEnd_GoesAfterTheLast()
    {
        var position = NoteOrdering.Between(2000, null);
        Assert.NotNull(position);
        Assert.True(position > 2000);
    }

    [Fact]
    public void Between_OnAnEmptyList_IsZero()
    {
        Assert.Equal(0, NoteOrdering.Between(null, null));
    }

    [Fact]
    public void Between_KeepsTheOrderStrict()
    {
        // Lo que de verdad importa: la posicion nueva cae ESTRICTAMENTE entre sus vecinas, o la lista
        // dejaria de tener un orden definido.
        double before = 0, after = NoteOrdering.Gap;
        for (int i = 0; i < 30; i++)
        {
            var middle = NoteOrdering.Between(before, after);
            if (middle is null) break; // se quedo sin sitio, que es un final legitimo

            Assert.True(middle > before && middle < after,
                $"insercion {i}: {middle} no cae entre {before} y {after}");
            after = middle.Value; // siempre en el mismo hueco: el caso mas exigente
        }
    }

    [Fact]
    public void Between_WithNoRoomLeft_ReturnsNull()
    {
        // Dos doubles consecutivos: no existe ningun numero entre ellos.
        double before = 1000;
        double after = Math.BitIncrement(before);

        Assert.Null(NoteOrdering.Between(before, after));
    }

    // --- Numeración inicial ---------------------------------------------------------------------

    [Fact]
    public void Spaced_IsStrictlyIncreasing_AndLeavesRoom()
    {
        var positions = NoteOrdering.Spaced(5);

        Assert.Equal(5, positions.Count);
        for (int i = 1; i < positions.Count; i++)
        {
            Assert.True(positions[i] > positions[i - 1]);
            // Con hueco de sobra: tiene que caber una insercion entre dos cualesquiera.
            Assert.NotNull(NoteOrdering.Between(positions[i - 1], positions[i]));
        }
    }

    [Fact]
    public void Spaced_WithNoNotes_IsEmpty()
    {
        Assert.Empty(NoteOrdering.Spaced(0));
    }

    // --- Cuánto hay que arrastrar para que se mueva ---------------------------------------------

    [Theory]
    [InlineData(0)]     // sin mover
    [InlineData(5)]     // un temblor al pulsar
    [InlineData(13)]    // medio hueco: con el redondeo natural ya se habria movido, y no debe
    [InlineData(-13)]
    [InlineData(19)]    // casi un hueco entero, pero aun por debajo del umbral (26 * 0.75 = 19.5)
    public void SlotShift_ShortDrags_DoNotMoveTheNote(double delta)
    {
        Assert.Equal(0, NoteOrdering.SlotShift(delta, pitch: 26));
    }

    [Theory]
    [InlineData(20, 1)]     // pasado el umbral: un hueco
    [InlineData(26, 1)]     // un hueco justo
    [InlineData(-20, -1)]   // hacia arriba
    [InlineData(-26, -1)]
    [InlineData(52, 2)]     // dos huecos
    [InlineData(78, 3)]
    public void SlotShift_LongerDrags_MoveByWholeSlots(double delta, int expected)
    {
        Assert.Equal(expected, NoteOrdering.SlotShift(delta, pitch: 26));
    }

    [Fact]
    public void SlotShift_WithAZeroPitch_DoesNotDivideByZero()
    {
        Assert.Equal(0, NoteOrdering.SlotShift(200, pitch: 0));
    }

    // --- Índice de destino ----------------------------------------------------------------------

    [Fact]
    public void TargetIndex_WithoutEnoughDrag_KeepsItsPlace()
    {
        Assert.Equal(2, NoteOrdering.TargetIndex(originalIndex: 2, delta: 10, pitch: 26, count: 5));
    }

    [Fact]
    public void TargetIndex_MovesFromWhereItWas()
    {
        Assert.Equal(4, NoteOrdering.TargetIndex(originalIndex: 2, delta: 52, pitch: 26, count: 5));
        Assert.Equal(0, NoteOrdering.TargetIndex(originalIndex: 2, delta: -52, pitch: 26, count: 5));
    }

    [Fact]
    public void TargetIndex_IsClampedToTheList()
    {
        Assert.Equal(4, NoteOrdering.TargetIndex(originalIndex: 2, delta: 5000, pitch: 26, count: 5));
        Assert.Equal(0, NoteOrdering.TargetIndex(originalIndex: 2, delta: -5000, pitch: 26, count: 5));
    }

    [Fact]
    public void TargetIndex_WithNoNotes_IsZero()
    {
        Assert.Equal(0, NoteOrdering.TargetIndex(originalIndex: 0, delta: 200, pitch: 26, count: 0));
    }
}
