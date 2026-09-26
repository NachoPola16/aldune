using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class TaskCompletionTests
{
    // --- HashLine --------------------------------------------------------------------------------

    [Fact]
    public void HashLine_SameContent_ProducesTheSameHash()
    {
        Assert.Equal(TaskCompletion.HashLine("☐ comprar pan"), TaskCompletion.HashLine("☐ comprar pan"));
    }

    [Fact]
    public void HashLine_IgnoresCheckedState()
    {
        // Desmarcar y volver a marcar la misma tarea tiene que apuntar al mismo registro.
        Assert.Equal(TaskCompletion.HashLine("☐ comprar pan"), TaskCompletion.HashLine("☒ comprar pan"));
    }

    [Fact]
    public void HashLine_DifferentContent_ProducesDifferentHashes()
    {
        Assert.NotEqual(TaskCompletion.HashLine("☐ comprar pan"), TaskCompletion.HashLine("☐ comprar leche"));
    }

    [Fact]
    public void HashLine_GlyphWithTextGluedRightAfterIt_DoesNotEatTheFirstLetter()
    {
        // Con un +2 fijo (en vez de TaskLines.PrefixLength) esto habria hasheado "omprar pan" en
        // vez de "comprar pan", perdiendo la "c" -- y encima habria cambiado de hash en cada
        // medición según si el usuario dejó o no el espacio, rompiendo el seguimiento.
        Assert.Equal(TaskCompletion.HashLine("☐ comprar pan"), TaskCompletion.HashLine("☐comprar pan"));
    }

    [Fact]
    public void HashLine_IsStableAcrossCalls()
    {
        // No vale string.GetHashCode(): varia entre ejecuciones del proceso a proposito.
        var first = TaskCompletion.HashLine("☒ una tarea cualquiera");
        var second = TaskCompletion.HashLine("☒ una tarea cualquiera");
        Assert.Equal(first, second);
    }

    // --- Prune -----------------------------------------------------------------------------------

    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    [Fact]
    public void Prune_WithNoCompletions_LeavesTextUnchanged()
    {
        var text = "☒ tarea hecha\n☐ tarea pendiente";
        var result = TaskCompletion.Prune(text, new Dictionary<string, DateTimeOffset>(), Now, OneDay);

        Assert.False(result.Changed);
        Assert.Equal(text, result.Text);
        Assert.Empty(result.HashesToClear);
    }

    [Fact]
    public void Prune_TaskCompletedBeforeTheDelay_IsRemoved()
    {
        var line = "☒ tarea vieja";
        var text = $"algo antes\n{line}\nalgo despues";
        var hash = TaskCompletion.HashLine(line);
        var completions = new Dictionary<string, DateTimeOffset> { [hash] = Now - TimeSpan.FromDays(2) };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.True(result.Changed);
        Assert.Equal("algo antes\nalgo despues", result.Text);
        Assert.Contains(hash, result.HashesToClear);
    }

    [Fact]
    public void Prune_TaskCompletedWithinTheDelay_IsKept()
    {
        var line = "☒ tarea reciente";
        var text = $"algo antes\n{line}\nalgo despues";
        var hash = TaskCompletion.HashLine(line);
        var completions = new Dictionary<string, DateTimeOffset> { [hash] = Now - TimeSpan.FromHours(1) };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.False(result.Changed);
        Assert.Equal(text, result.Text);
        Assert.Empty(result.HashesToClear);
    }

    [Fact]
    public void Prune_UncheckedTask_IsNeverRemovedEvenWithAnExpiredRecord()
    {
        // Puede pasar si el usuario desmarco la tarea por otro camino que no paso por
        // NotesRepository.ClearTaskCompletion: el registro queda huerfano, pero una tarea sin
        // marcar no se borra nunca.
        var line = "☐ tarea sin marcar";
        var text = line;
        var hash = TaskCompletion.HashLine(line);
        var completions = new Dictionary<string, DateTimeOffset> { [hash] = Now - TimeSpan.FromDays(10) };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.False(result.Changed);
        Assert.Equal(text, result.Text);
        // Pero el registro huerfano si se reporta para limpiar, porque ya no corresponde a ninguna
        // tarea marcada.
        Assert.Contains(hash, result.HashesToClear);
    }

    [Fact]
    public void Prune_MarkedTaskWithoutARecord_IsNeverRemoved()
    {
        // Solo se borra lo que se sabe con certeza que ha vencido.
        var text = "☒ tarea sin registro de cuando se marco";
        var result = TaskCompletion.Prune(text, new Dictionary<string, DateTimeOffset>(), Now, OneDay);

        Assert.False(result.Changed);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Prune_RemovesSeveralExpiredLinesAtOnce()
    {
        var lineA = "☒ tarea A";
        var lineB = "☒ tarea B";
        var text = $"{lineA}\nen medio\n{lineB}";
        var completions = new Dictionary<string, DateTimeOffset>
        {
            [TaskCompletion.HashLine(lineA)] = Now - TimeSpan.FromDays(3),
            [TaskCompletion.HashLine(lineB)] = Now - TimeSpan.FromDays(5),
        };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.True(result.Changed);
        Assert.Equal("en medio", result.Text);
        Assert.Equal(2, result.HashesToClear.Count);
    }

    [Fact]
    public void Prune_RemovingTheLastCrlfLine_DoesNotLeaveADanglingCarriageReturn()
    {
        // "algo\r\n☒ hecho" -> el \r de "algo\r" pertenece al separador de esa primera linea, no a
        // la que se borra: sin cuidado se queda un \r suelto al final ("algo\r" en vez de "algo").
        var line = "☒ hecho";
        var text = $"algo\r\n{line}";
        var hash = TaskCompletion.HashLine(line);
        var completions = new Dictionary<string, DateTimeOffset> { [hash] = Now - TimeSpan.FromDays(3) };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.True(result.Changed);
        Assert.Equal("algo", result.Text);
    }

    [Fact]
    public void Prune_EditedLine_NoLongerMatchesItsOldHash_AndTheOldRecordIsReportedForCleanup()
    {
        // El texto de la tarea cambio (el hash ya no es el mismo), asi que ya no se sabe que esa
        // linea estaba vencida -- pero el registro viejo se reporta para que no quede huerfano.
        var originalLine = "☒ texto original";
        var oldHash = TaskCompletion.HashLine(originalLine);
        var text = "☒ texto editado";
        var completions = new Dictionary<string, DateTimeOffset> { [oldHash] = Now - TimeSpan.FromDays(3) };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.False(result.Changed);
        Assert.Equal(text, result.Text);
        Assert.Contains(oldHash, result.HashesToClear);
    }
    [Fact]
    public void Prune_CheckedTaskWithoutARecord_IsReportedSoItsClockStartsNow()
    {
        // Marcada con el ajuste apagado, pegada, escrita a mano o recuperada con Ctrl+Z: sin esto no
        // se borraría nunca. Se empieza a contar desde que se ve, no se borra en esta pasada.
        var text = "☒ sin registro\n☒ con registro\n☐ pendiente";
        var completions = new Dictionary<string, DateTimeOffset>
        {
            [TaskCompletion.HashLine("☒ con registro")] = Now
        };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.False(result.Changed);
        Assert.Equal(new[] { TaskCompletion.HashLine("☒ sin registro") }, result.HashesToStart);
    }

    [Fact]
    public void Prune_MapIndex_KeepsTheCaretOnTheSameCharacter()
    {
        // La línea borrada está por encima del cursor: sin mapear, el cursor saltaba tantos
        // caracteres como tenía la línea (reproducido con una sonda, 2026-09-26).
        var text = "☒ hecha\r\nfinal texto aqui";
        var completions = new Dictionary<string, DateTimeOffset>
        {
            [TaskCompletion.HashLine("☒ hecha")] = Now - TimeSpan.FromDays(2)
        };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);
        int mapped = result.MapIndex(text.IndexOf("texto", StringComparison.Ordinal));

        Assert.Equal("final texto aqui", result.Text);
        Assert.Equal("texto", result.Text.Substring(mapped, 5));
    }

    [Fact]
    public void Prune_MapIndex_CaretOnTheRemovedLine_GoesToTheStartOfTheNextLine()
    {
        var text = "a\n☒ hecha\nb";
        var completions = new Dictionary<string, DateTimeOffset>
        {
            [TaskCompletion.HashLine("☒ hecha")] = Now - TimeSpan.FromDays(2)
        };

        var result = TaskCompletion.Prune(text, completions, Now, OneDay);

        Assert.Equal("a\nb", result.Text);
        Assert.Equal(2, result.MapIndex(text.IndexOf("cha", StringComparison.Ordinal)));
        Assert.Equal(result.Text.Length, result.MapIndex(text.Length));
    }
}
