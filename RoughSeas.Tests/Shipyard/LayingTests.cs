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

    [Fact]
    public void HerRigTakesPolesOfTheWoodItWasBegunInOnly()
    {
        var poles = new PoleLaying(new AssetLocation("roughseas:pole-*"));
        canoe.Lay("oak", 0);

        Assert.True(poles.Takes(Pole("maple")));
        Assert.Null(poles.Refusal(Pole("oak"), canoe));
        Assert.Equal("shipyard-poles-one-wood", poles.Refusal(Pole("maple"), canoe));
    }

    [Fact]
    public void WashstrakesTakePlanksOfTheWoodTheyWereBegunInOnly()
    {
        var strakes = new FramingStage { Group = "Strakes", Work = FramingWork.Plank, Part = "strakes", Pieces = 2 };
        var outrigger = new Framing(() => tree, () => { }, [strakes]);
        var planks = new PlankLaying(new AssetLocation("*:plank-*"));
        outrigger.Lay(planks.WoodOf(Plank("game", "pine")), 0);

        Assert.Equal("pine", outrigger.WoodOf(strakes));
        Assert.Null(outrigger.Wood);
        Assert.Null(planks.Refusal(Plank("game", "pine"), outrigger));
        Assert.Equal("shipyard-planks-one-wood", planks.Refusal(Plank("game", "oak"), outrigger));
    }

    [Fact]
    public void AModdedPlankKeepsItsDomainsWood() =>
        Assert.Equal("wildcrafttree-ekki", new PlankLaying(new AssetLocation("*:plank-*")).WoodOf(Plank("wildcrafttree", "ekki")));

    [Fact]
    public void AnyAxeHewsAndNothingElse()
    {
        var hewing = new HewLaying();

        Assert.True(hewing.Takes(new DummySlot(new ItemStack(new Item { Code = new AssetLocation("game:axe-felling-flint"), Tool = EnumTool.Axe }))));
        Assert.False(hewing.Takes(Pole("oak")));
    }

    private static DummySlot Pole(string wood) => new(new ItemStack(new Item
    {
        Code = new AssetLocation($"roughseas:pole-{wood}"),
        Variant = new RelaxedReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["wood"] = wood })
    }));

    private static DummySlot Plank(string domain, string wood) => new(new ItemStack(new Item
    {
        Code = new AssetLocation(domain, $"plank-{wood}"),
        Variant = new RelaxedReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["wood"] = wood })
    }));

    private static DummySlot Lath(string wood, string state) => new(new ItemStack(new Item
    {
        Code = new AssetLocation($"roughseas:lath-{wood}-{state}"),
        Variant = new RelaxedReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["wood"] = wood, ["state"] = state })
    }));
}
