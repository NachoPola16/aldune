using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ShareQrTests
{
    private const string Prefix = "aldune-profile-v2:";
    private static string CodeOf(int payloadChars) => Prefix + new string('a', payloadChars);

    [Fact]
    public void Build_ARealSizedCode_GivesASquareMatrixWithQuietZone()
    {
        var result = ShareQr.Build(CodeOf(230));

        Assert.False(result.TooLarge);
        var m = result.Modules!;
        Assert.Equal(m.GetLength(0), m.GetLength(1));
        Assert.Equal(result.Size, m.GetLength(0));
        Assert.True(result.Size >= 21 + 8);
        // La zona de silencio (4 módulos) es blanca y el primer módulo del localizador (esquina superior izquierda) es negro.
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < result.Size; j++)
                Assert.False(m[i, j]);
        Assert.True(m[4, 4]);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = ShareQr.Build(CodeOf(300)).Modules!;
        var b = ShareQr.Build(CodeOf(300)).Modules!;
        Assert.Equal(a.Length, b.Length);
        for (int y = 0; y < a.GetLength(0); y++)
            for (int x = 0; x < a.GetLength(1); x++)
                Assert.Equal(a[y, x], b[y, x]);
    }

    [Fact]
    public void Build_AProfileWithManyChosenNotes_StillFitsInOneQr()
    {
        // ~40 notas elegidas ≈ 700 caracteres de más: no puede salir «demasiado grande».
        var result = ShareQr.Build(CodeOf(250 + 40 * 50));
        Assert.False(result.TooLarge);
    }

    [Fact]
    public void Build_AHugeCode_ReportsTooLargeInsteadOfThrowing()
    {
        var result = ShareQr.Build(CodeOf(3000));
        Assert.True(result.TooLarge);
        Assert.Null(result.Modules);
        Assert.Equal(0, result.Size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com")]
    [InlineData("aldune-profile-v1:abc")]
    public void Build_ANonProfileCode_Throws(string text)
    {
        Assert.Throws<ArgumentException>(() => ShareQr.Build(text));
    }

    [Fact]
    public void Build_NullThrows() => Assert.Throws<ArgumentNullException>(() => ShareQr.Build(null!));

    [Fact]
    public void Build_TrimsAndIgnoresPrefixCase()
    {
        var plain = ShareQr.Build(CodeOf(100)).Modules!;
        var padded = ShareQr.Build("  " + CodeOf(100) + "\n").Modules!;
        Assert.Equal(plain.GetLength(0), padded.GetLength(0));
        Assert.NotNull(ShareQr.Build("ALDUNE-PROFILE-V2:" + new string('a', 100)).Modules);
    }
}
