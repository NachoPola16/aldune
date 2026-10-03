using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteDisplayColorTests
{
    [Fact]
    public void Resolve_Disabled_KeepsTheNoteColor()
    {
        Assert.Equal("#EBD38B", NoteDisplayColor.Resolve("#EBD38B", null));
    }

    [Fact]
    public void Resolve_WithAUniformColor_UsesItForEveryNote()
    {
        Assert.Equal("#33363A", NoteDisplayColor.Resolve("#EBD38B", "#33363a"));
        Assert.Equal("#33363A", NoteDisplayColor.Resolve("#C2D4FF", "#33363A"));
    }

    [Theory]
    [InlineData("rojo")]
    [InlineData("")]
    [InlineData("#12345")]
    public void Resolve_InvalidUniformColor_IsIgnored(string uniform)
    {
        // Un settings.json editado a mano no puede dejar las notas sin color.
        Assert.Equal("#EBD38B", NoteDisplayColor.Resolve("#EBD38B", uniform));
    }
}
