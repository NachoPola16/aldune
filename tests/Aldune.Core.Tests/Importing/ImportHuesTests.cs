using Aldune.Core.Importing;

namespace Aldune.Core.Tests.Importing;

public sealed class ImportHuesTests
{
    [Theory]
    [InlineData("YELLOW", ImportHue.Yellow)]
    [InlineData("Green", ImportHue.Green)]
    [InlineData("TEAL", ImportHue.Cyan)]
    [InlineData("CERULEAN", ImportHue.Cyan)]
    [InlineData("blue", ImportHue.Blue)]
    [InlineData("PURPLE", ImportHue.Blue)]
    [InlineData("PINK", ImportHue.Pink)]
    [InlineData("RED", ImportHue.Orange)]
    [InlineData("ORANGE", ImportHue.Orange)]
    [InlineData("BROWN", ImportHue.Orange)]
    public void Names_MapToTheClosestFamily(string name, ImportHue expected) =>
        Assert.Equal(expected, ImportHues.FromName(name));

    [Theory]
    [InlineData("DEFAULT")]
    [InlineData("Gray")]
    [InlineData("Charcoal")]
    [InlineData("white")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nonsense")]
    public void Neutrals_AndUnknowns_HaveNoHue(string? name) => Assert.Null(ImportHues.FromName(name));

    [Theory]
    [InlineData("#FFF740", ImportHue.Yellow)]
    [InlineData("#3A7BD5", ImportHue.Blue)]
    [InlineData("#808080", null)]
    [InlineData("not a colour", null)]
    public void Hex_UsesTheHueUnlessItIsGrey(string hex, ImportHue? expected) =>
        Assert.Equal(expected, ImportHues.FromHex(hex));
}
