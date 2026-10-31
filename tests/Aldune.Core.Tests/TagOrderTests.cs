using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

/// <summary>Twin of <c>TagOrderTest</c> in <c>android/domain</c>: both platforms order and move tags the same way.</summary>
public class TagOrderTests
{
    [Fact]
    public void WithoutACustomOrder_ItIsAlphabeticalIgnoringCase() =>
        Assert.Equal(["Casa", "ideas", "Zeta"], TagOrder.Apply(["Zeta", "ideas", "Casa"], []));

    [Fact]
    public void TheCustomOrderComesFirstAndTheRestStaysAlphabetical() =>
        Assert.Equal(["zeta", "ideas", "casa", "otra"], TagOrder.Apply(["casa", "otra", "zeta", "ideas"], ["zeta", "ideas"]));

    [Fact]
    public void ATagOfTheCustomOrderThatNoLongerExistsIsIgnoredAndCaseDoesNotMatter() =>
        Assert.Equal(["Ideas", "casa"], TagOrder.Apply(["casa", "Ideas"], ["borrada", "IDEAS"]));

    [Fact]
    public void Moving_StartsFromTheVisibleOrderAndReturnsTheWholeOrderToSave()
    {
        string[] visible = ["a", "b", "c"];
        Assert.Equal(["a", "c", "b"], TagOrder.Move(visible, "b", +1));
        Assert.Equal(["b", "a", "c"], TagOrder.Move(visible, "b", -1));
        Assert.Equal(visible, TagOrder.Move(visible, "a", -1));
        Assert.Equal(visible, TagOrder.Move(visible, "c", +1));
        Assert.Equal(visible, TagOrder.Move(visible, "nope", +1));
    }

    [Fact]
    public void Move_IsCaseInsensitive() => Assert.Equal(["a", "c", "B"], TagOrder.Move(["a", "B", "c"], "b", +1));
}
