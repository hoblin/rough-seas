using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Vessels;

public class ShellTests
{
    private const double Hole = 0.0025;
    private const double CutShare = 0.25;

    private static readonly SkinShell Skin = new()
    {
        Material = "hide", Metres = 0.005, Backing = new RigidShell { Material = "wood-ash", Metres = 0.02 }, BackedShare = 0.3, BottomBackedShare = 0.7
    };

    private readonly MaterialTable materials = new(new Dictionary<string, Material>
    {
        ["wood"] = new() { Toughness = 60000 },
        ["wood-oak"] = new() { Toughness = 75000 },
        ["wood-ash"] = new() { Toughness = 68000 },
        ["hide"] = new() { CutToughness = 17000 },
        ["bark"] = new() { Toughness = 700, CutToughness = 700 }
    }, new CountingLogger());

    [Fact]
    public void PicksTheShellByItsKind()
    {
        Assert.IsType<SkinShell>(Shell.Of(new JsonObject(JToken.Parse("""{"kind": "skin", "material": "hide", "metres": 0.005}"""))));
        Assert.IsType<RigidShell>(Shell.Of(new JsonObject(JToken.Parse("""{"material": "wood-oak", "metres": 0.02}"""))));
    }

    [Fact]
    public void AHullWithNoShellHasNone()
    {
        Assert.Null(Shell.Of(new JsonObject(null)));
    }

    [Fact]
    public void APlankIsHoledByItsImpactToughnessAndCutByAShareOfIt()
    {
        var resists = new RigidShell { Material = "wood-oak", Metres = 0.02 }.Resists(30000, materials, Hole, CutShare);

        Assert.Equal(Resistance.Holing(75000, 0.02, Hole), resists.Against(Blow.Point, backed: false), 6);
        Assert.Equal(Resistance.Holing(75000, 0.02, Hole) * CutShare, resists.Against(Blow.Cut, backed: false), 6);
    }

    [Fact]
    public void ASkinSpringsBackFromABlowWhereNoLathBacksIt()
    {
        Assert.Equal(double.PositiveInfinity, Skin.Resists(3000, materials, Hole, CutShare).Against(Blow.Point, backed: false));
    }

    [Fact]
    public void ABlowOnASkinOverALathBreaksTheLath()
    {
        Assert.Equal(Resistance.Holing(68000, 0.02, Hole), Skin.Resists(3000, materials, Hole, CutShare).Against(Blow.Point, backed: true), 6);
    }

    [Fact]
    public void AnEdgeCutsASkinByItsTearingAndTheLathUnderItToo()
    {
        var resists = Skin.Resists(3000, materials, Hole, CutShare);

        Assert.Equal(Resistance.Holing(17000, 0.005, Hole), resists.Against(Blow.Cut, backed: false), 6);
        Assert.Equal(Resistance.Holing(17000, 0.005, Hole) + Resistance.Holing(68000, 0.02, Hole) * CutShare, resists.Against(Blow.Cut, backed: true), 6);
    }

    [Fact]
    public void LathsBackTheSidesAndTheBottomEachByTheirOwnShare()
    {
        var resists = Skin.Resists(3000, materials, Hole, CutShare);

        Assert.True(resists.Backed(HullPlace.PortSide, 0.29));
        Assert.False(resists.Backed(HullPlace.PortSide, 0.31));
        Assert.True(resists.Backed(HullPlace.Bottom, 0.69));
    }

    [Fact]
    public void BarkSplitsTogetherWithTheSheathingThatLinesIt()
    {
        var canoe = new RigidShell { Material = "bark", Metres = 0.005, Backing = new RigidShell { Material = "wood-oak", Metres = 0.003 }, BackedShare = 1 };

        Assert.Equal(Resistance.Holing(700, 0.005, Hole) + Resistance.Holing(75000, 0.003, Hole),
            canoe.Resists(2400, materials, Hole, CutShare).Against(Blow.Point, backed: true), 6);
    }

    [Fact]
    public void AHullWithNoShellResistsEveryBlowWithItsWholeFrame()
    {
        var whole = Resistance.Whole(3000);

        Assert.All(Enum.GetValues<Blow>(), blow => Assert.Equal(3000, whole.Against(blow, backed: false)));
    }
}
