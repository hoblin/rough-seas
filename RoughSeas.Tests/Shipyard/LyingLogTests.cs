using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Shipyard;

public class LyingLogTests
{
    [Theory]
    [InlineData("game:log-placed-oak-we", EnumAxis.X)]
    [InlineData("game:logsection-placed-redwood-ne-ns", EnumAxis.Z)]
    public void ALogOnItsSideLiesAlongItsRotation(string code, EnumAxis axis)
    {
        var log = LyingLog.Of(Block(code, ("wood", code.Split('-')[2]), ("rotation", code.Split('-')[^1])));

        Assert.Equal(new LyingLog(new Wood("game", code.Split('-')[2]), axis), log);
    }

    [Theory]
    [InlineData("game:log-placed-oak-ud")]
    [InlineData("game:log-grown-oak-we")]
    [InlineData("game:plankstairs-oak-we")]
    public void AnUprightLogAStandingTrunkOrAnyOtherWoodIsNoLyingLog(string code) =>
        Assert.Null(LyingLog.Of(Block(code, ("wood", "oak"), ("rotation", code.Split('-')[^1]))));

    private static Block Block(string code, params (string Key, string Value)[] variant) => new()
    {
        Code = new AssetLocation(code),
        Variant = new RelaxedReadOnlyDictionary<string, string>(variant.ToDictionary(pair => pair.Key, pair => pair.Value))
    };
}
