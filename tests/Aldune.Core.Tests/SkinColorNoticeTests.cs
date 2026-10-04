using Xunit;

namespace Aldune.Core.Tests;

public class SkinColorNoticeTests
{
    [Fact]
    public void Filled_ShowsEveryColor() =>
        Assert.Equal(SkinColorNoticeKind.None, SkinColorNotice.For(SkinCard.Filled, "#2E2E2E", null));

    [Fact]
    public void Mono_ShowsNoColor() =>
        Assert.Equal(SkinColorNoticeKind.ColorsNotShown, SkinColorNotice.For(SkinCard.Mono, "#472525", null));

    [Theory]
    [InlineData("#472525", SkinColorNoticeKind.None)]
    [InlineData("#2E2E2E", SkinColorNoticeKind.GrayStripe)]
    [InlineData("#F1F4F8", SkinColorNoticeKind.GrayStripe)]
    public void Stripe_ShowsTheHue_ButAGreyStaysGrey(string color, SkinColorNoticeKind expected) =>
        Assert.Equal(expected, SkinColorNotice.For(SkinCard.Stripe, color, null));

    [Fact]
    public void Stripe_UsesTheUniformColorWhenThereIsOne() =>
        Assert.Equal(SkinColorNoticeKind.GrayStripe, SkinColorNotice.For(SkinCard.Stripe, "#472525", "#2E2E2E"));

    [Fact]
    public void Tinted_IgnoresTheNoteColor_UnlessThereIsAUniformOne()
    {
        Assert.Equal(SkinColorNoticeKind.ChannelColors, SkinColorNotice.For(SkinCard.Tinted, "#472525", null));
        Assert.Equal(SkinColorNoticeKind.None, SkinColorNotice.For(SkinCard.Tinted, "#472525", "#472525"));
        Assert.Equal(SkinColorNoticeKind.GrayStripe, SkinColorNotice.For(SkinCard.Tinted, "#472525", "#2E2E2E"));
    }
}
