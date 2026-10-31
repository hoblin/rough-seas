using RoughSeas.Shipyard;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RoughSeas.Tests.Shipyard;

public class LayingTests
{
    private static readonly AssetLocation Laths = new("roughseas:lath-*");
    private static readonly FramingStage Frame = new() { Group = "Frame", Work = FramingWork.Lath, Pieces = 2 };

    private readonly TreeAttribute tree = new();
    private readonly Framing canoe;
    private readonly LathLaying laying = new(Laths);

    public LayingTests()
    {
        canoe = new Framing(() => tree, () => { }, [Frame]);
    }

    [Fact]
    public void ADryLathWillNotBend() => Assert.Equal("shipyard-soak-laths", laying.Refusal(Lath("oak", "dry"), canoe));

    [Fact]
    public void AWetLathGoesOnABareSheet() => Assert.Null(laying.Refusal(Lath("oak", "wet"), canoe));

    [Fact]
    public void HerFrameTakesLathsOfTheWoodItWasBegunInOnly()
    {
        canoe.Lay("oak", 0);

        Assert.Null(laying.Refusal(Lath("oak", "wet"), canoe));
        Assert.Equal("shipyard-laths-one-wood", laying.Refusal(Lath("maple", "wet"), canoe));
    }

    [Fact]
    public void OnlyItsOwnLathsAreLaths()
    {
        Assert.True(laying.Takes(Lath("oak", "wet")));
        Assert.False(laying.Takes(new DummySlot(new ItemStack(new Item { Code = new AssetLocation("game:stick") }))));
    }

    private static DummySlot Lath(string wood, string state) => new(new ItemStack(new Item
    {
        Code = new AssetLocation($"roughseas:lath-{wood}-{state}"),
        Variant = new RelaxedReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["wood"] = wood, ["state"] = state })
    }));
}
