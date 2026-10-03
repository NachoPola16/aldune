using System.Globalization;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteLabelsTests
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    [Fact]
    public void None_IsTheSpacedCapitalsOfToday()
    {
        // Lo que hacía NoteTabLabelConverter: mayúsculas, espacios duros y un espacio fino entre letras.
        Assert.Equal("H\u200AO\u200AY\u200A\u00A0\u200AY", NoteLabels.Dock("  hoy y ", SkinTitleAdornment.None, 0, Es));
    }

    [Theory]
    [InlineData(SkinTitleAdornment.Channel, 1, "Tareas universidad", "CH2 TAREAS_UNIVERSIDAD")]
    [InlineData(SkinTitleAdornment.Channel, 5, "Hoy", "CH2 HOY")]
    [InlineData(SkinTitleAdornment.Folder, 0, " Tareas   universidad ", "tareas_universidad/")]
    [InlineData(SkinTitleAdornment.Uppercase, 0, "Gestión", "GESTIÓN")]
    public void Adornments(SkinTitleAdornment adornment, int channel, string title, string expected)
    {
        Assert.Equal(expected, NoteLabels.Dock(title, adornment, channel, Es));
    }

    [Theory]
    [InlineData(SkinTitleAdornment.None)]
    [InlineData(SkinTitleAdornment.Channel)]
    [InlineData(SkinTitleAdornment.Folder)]
    [InlineData(SkinTitleAdornment.Uppercase)]
    public void BlankTitle_StaysBlank(SkinTitleAdornment adornment)
    {
        Assert.Equal("", NoteLabels.Dock("   ", adornment, 0, Es));
    }

    [Fact]
    public void Folder_KeepsAccentsAndEmoji()
    {
        Assert.Equal("compra_🛒_sábado/", NoteLabels.Dock("Compra 🛒 sábado", SkinTitleAdornment.Folder, 0, Es));
    }

    [Fact]
    public void Prompt_IsAShellLine()
    {
        var prompt = NoteLabels.Prompt("Nacho Pola", "notas", "Hoy", Es);

        Assert.Equal("nacho_pola@aldune", prompt.User);
        Assert.Equal("~/notas", prompt.Path);
        Assert.Equal("$ cat hoy", prompt.Command);
    }

    [Fact]
    public void Prompt_WithoutTitleOrUser_StillReadsAsAPrompt()
    {
        var prompt = NoteLabels.Prompt("  ", "notas", "", Es);

        Assert.Equal("user@aldune", prompt.User);
        Assert.Equal("$ ", prompt.Command);
    }
}
