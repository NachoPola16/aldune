using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class BulletLinesTests
{
    // --- Reconocer una línea con viñeta ----------------------------------------------------------

    [Theory]
    [InlineData("→ comprar pan", true)]
    [InlineData("    → con sangría", true)]
    [InlineData("\t→ con tabulador", true)]
    [InlineData("comprar pan", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("- comprar pan", false)] // un guion normal NO cuenta -- justo lo que se quiso evitar
    public void IsBulletLine_RecognisesThePrefix(string line, bool expected)
    {
        Assert.Equal(expected, BulletLines.IsBulletLine(line));
    }

    [Fact]
    public void IsBulletLine_GlyphWithTextGluedRightAfterIt_IsStillABullet()
    {
        Assert.True(BulletLines.IsBulletLine("→comprar"));
    }

    [Fact]
    public void IsBulletLine_GlyphAlone_IsABullet()
    {
        Assert.True(BulletLines.IsBulletLine("→"));
    }

    [Fact]
    public void IsBulletLine_GlyphInTheMiddleOfTheLine_IsNotABullet()
    {
        Assert.False(BulletLines.IsBulletLine("mira esta flecha → que raro"));
    }

    [Fact]
    public void IsBulletLine_ATaskLine_IsNotABullet()
    {
        Assert.False(BulletLines.IsBulletLine("☐ comprar pan"));
    }

    // --- PrefixLength ----------------------------------------------------------------------------

    [Fact]
    public void PrefixLength_WithASpaceAfterTheGlyph_IsTwo()
    {
        Assert.Equal(2, BulletLines.PrefixLength("→ comprar", 0));
    }

    [Fact]
    public void PrefixLength_WithTextGluedRightAfterTheGlyph_IsOne()
    {
        Assert.Equal(1, BulletLines.PrefixLength("→comprar", 0));
    }

    // --- Convertir una línea en viñeta (atajo de teclado) -----------------------------------------

    [Fact]
    public void ToggleBulletLineAt_PlainLineGetsThePrefix_AndTheCaretFollowsTheText()
    {
        var (text, caret) = BulletLines.ToggleBulletLineAt("comprar pan", caret: 7);

        Assert.Equal("→ comprar pan", text);
        Assert.Equal(9, caret); // el cursor sigue justo antes de "pan"
    }

    [Fact]
    public void ToggleBulletLineAt_BulletLineLosesThePrefix()
    {
        var (text, caret) = BulletLines.ToggleBulletLineAt("→ comprar pan", caret: 9);

        Assert.Equal("comprar pan", text);
        Assert.Equal(7, caret);
    }

    [Fact]
    public void ToggleBulletLineAt_KeepsTheIndent()
    {
        var (text, _) = BulletLines.ToggleBulletLineAt("    comprar pan", caret: 4);
        Assert.Equal("    → comprar pan", text);
    }

    [Fact]
    public void ToggleBulletLineAt_OnlyTouchesItsOwnLine()
    {
        var text = "primera\r\nsegunda\r\ntercera";
        int caretInSecond = text.IndexOf("segunda", StringComparison.Ordinal) + 2;

        var (result, _) = BulletLines.ToggleBulletLineAt(text, caretInSecond);

        Assert.Equal("primera\r\n→ segunda\r\ntercera", result);
    }

    [Fact]
    public void ToggleBulletLineAt_OnAnEmptyLine_StillCreatesABullet()
    {
        var (text, caret) = BulletLines.ToggleBulletLineAt("", caret: 0);
        Assert.Equal("→ ", text);
        Assert.Equal(2, caret);
    }

    [Fact]
    public void ToggleBulletLineAt_ATaskLine_ConvertsItToABulletInstead()
    {
        // Pulsar el atajo de viñeta sobre una tarea ya existente la convierte, no la apila.
        var (text, caret) = BulletLines.ToggleBulletLineAt("☐ comprar pan", caret: 9);

        Assert.Equal("→ comprar pan", text);
        Assert.Equal(9, caret);
    }

    [Fact]
    public void ToggleBulletLineAt_ACheckedTaskLine_ConvertsItToABulletToo()
    {
        var (text, _) = BulletLines.ToggleBulletLineAt("☒ hecho", caret: 0);
        Assert.Equal("→ hecho", text);
    }

    [Fact]
    public void ToggleBulletLineAt_ATaskLineWithIndent_KeepsTheIndentAfterConverting()
    {
        var (text, _) = BulletLines.ToggleBulletLineAt("    ☐ sub-tarea", caret: 0);
        Assert.Equal("    → sub-tarea", text);
    }

    // --- Continuar la lista con Enter -------------------------------------------------------------

    [Fact]
    public void EnterContinuation_AtTheEndOfABullet_StartsTheNextOne()
    {
        var text = "→ comprar pan";
        var result = BulletLines.EnterContinuation(text, text.Length);

        Assert.NotNull(result);
        Assert.Equal("→ comprar pan\r\n→ ", result!.Value.Text);
        Assert.Equal(result.Value.Text.Length, result.Value.Caret);
    }

    [Fact]
    public void EnterContinuation_KeepsTheIndentOfTheLineItContinues()
    {
        var text = "    → sub-punto";
        var result = BulletLines.EnterContinuation(text, text.Length);

        Assert.Equal("    → sub-punto\r\n    → ", result!.Value.Text);
    }

    [Fact]
    public void EnterContinuation_OnAnEmptyBullet_EndsTheList()
    {
        var text = "→ comprar pan\r\n→ ";
        var result = BulletLines.EnterContinuation(text, text.Length);

        Assert.NotNull(result);
        Assert.Equal("→ comprar pan\r\n", result!.Value.Text);
        Assert.Equal(result.Value.Text.Length, result.Value.Caret);
    }

    [Fact]
    public void EnterContinuation_OnAPlainLine_ReturnsNull()
    {
        Assert.Null(BulletLines.EnterContinuation("texto normal", 12));
    }

    [Fact]
    public void EnterContinuation_InTheMiddleOfABullet_ReturnsNull()
    {
        Assert.Null(BulletLines.EnterContinuation("→ comprar pan", 6));
    }

    // --- IsEmptyBulletLine --------------------------------------------------------------------------

    [Theory]
    [InlineData("→ ", true)]
    [InlineData("→", true)]
    [InlineData("→   ", true)]
    [InlineData("→ comprar pan", false)]
    [InlineData("→comprar", false)]
    [InlineData("comprar pan", false)]
    [InlineData("", false)]
    public void IsEmptyBulletLine_RecognisesABulletWithNoRealContent(string line, bool expected)
    {
        Assert.Equal(expected, BulletLines.IsEmptyBulletLine(line));
    }
}
