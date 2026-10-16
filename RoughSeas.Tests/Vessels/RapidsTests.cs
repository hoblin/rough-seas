using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class RapidsTests
{
    private readonly Rapids rapids = new() { Block = new AssetLocation("game:rapidwater-*"), JoulesPerSecond = 300 };

    [Theory]
    [InlineData("game:rapidwater-n-7")]
    [InlineData("game:rapidwater-still-1")]
    public void HoldsEveryRapidWaterBlock(string code)
    {
        Assert.True(rapids.Holds(new Block { Code = new AssetLocation(code) }));
    }

    [Theory]
    [InlineData("game:water-n-7")]
    [InlineData("game:saltwater-still-7")]
    public void LeavesOtherWaterAlone(string code)
    {
        Assert.False(rapids.Holds(new Block { Code = new AssetLocation(code) }));
    }
}
