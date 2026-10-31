using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Vessels;

public class ShellTests
{
    private const double Hole = 0.0025;
    private const double CutShare = 0.25;
    private const double Soaked = 0.5;

    private static readonly HullConfig Hull = new() { CrackSquareCentimetres = 25, SoakedStrengthShare = Soaked };

    private static readonly SkinShell Skin = new()
    {
        Material = "hide", Metres = 0.005, SpanMetres = 0.6, Backing = new RigidShell { Material = "wood-ash", Metres = 0.02 }, BackedShare = 0.3,
        BottomBackedShare = 0.7
    };

    private readonly MaterialTable materials = new(new Dictionary<string, Material>
    {
        ["wood"] = new() { Strength = 90, Toughness = 60000 },
        ["wood-oak"] = new() { Strength = 100, Toughness = 75000 },
        ["wood-ash"] = new() { Strength = 104, Toughness = 68000 },
        ["hide"] = new() { CutToughness = 17000 },
        ["bark"] = new() { Strength = 114, Toughness = 700, CutToughness = 700 },
        ["rope-flax"] = new() { CutToughness = 5700 }
    }, new CountingLogger());

    private static readonly BundleShell Reeds = new()
    {
        Material = "reed", Metres = 0.45, Backing = new RigidShell { Material = "rope-flax", Metres = 0.022 }, BackedShare = 0.17
    };

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
        var resists = new RigidShell { Material = "wood-oak", Metres = 0.02 }.Resists(30000, materials, Hull, CutShare);

        Assert.Equal(Resistance.Holing(75000, 0.02, Hole), resists.Against(Blow.Point, backed: false), 6);
        Assert.Equal(Resistance.Holing(75000, 0.02, Hole) * CutShare, resists.Against(Blow.Cut, backed: false), 6);
    }

    [Fact]
    public void ASkinSpringsBackFromABlowWhereNoLathBacksIt()
    {
        Assert.Equal(double.PositiveInfinity, Skin.Resists(3000, materials, Hull, CutShare).Against(Blow.Point, backed: false));
    }

    [Fact]
    public void ABlowOnASkinOverALathBreaksTheLath()
    {
        Assert.Equal(Resistance.Holing(68000, 0.02, Hole), Skin.Resists(3000, materials, Hull, CutShare).Against(Blow.Point, backed: true), 6);
    }

    [Fact]
    public void AnEdgeCutsASkinByItsTearingAndTheLathUnderItToo()
    {
        var resists = Skin.Resists(3000, materials, Hull, CutShare);

        Assert.Equal(Resistance.Holing(17000, 0.005, Hole), resists.Against(Blow.Cut, backed: false), 6);
        Assert.Equal(Resistance.Holing(17000, 0.005, Hole) + Resistance.Holing(68000, 0.02, Hole) * CutShare, resists.Against(Blow.Cut, backed: true), 6);
    }

    [Fact]
    public void LathsBackTheSidesAndTheBottomEachByTheirOwnShare()
    {
        var resists = Skin.Resists(3000, materials, Hull, CutShare);

        Assert.True(resists.Backed(HullPlace.PortSide, 0.29));
        Assert.False(resists.Backed(HullPlace.PortSide, 0.31));
        Assert.True(resists.Backed(HullPlace.Bottom, 0.69));
    }

    [Fact]
    public void BarkSplitsTogetherWithTheSheathingThatLinesIt()
    {
        var canoe = new RigidShell { Material = "bark", Metres = 0.005, Backing = new RigidShell { Material = "wood-oak", Metres = 0.003 }, BackedShare = 1 };

        Assert.Equal(Resistance.Holing(700, 0.005, Hole) + Resistance.Holing(75000, 0.003, Hole),
            canoe.Resists(2400, materials, Hull, CutShare).Against(Blow.Point, backed: true), 6);
    }

    [Fact]
    public void BarkIsLinedByTheWoodOfItsLathsOnlyWhereTheyAreLaid()
    {
        var canoe = new RigidShell { Material = "bark", Metres = 0.005, Backing = new RigidShell { Material = "wood-elm", Metres = 0.003 }, BackedShare = 1 };
        var half = canoe.Lined("wood-oak", 0.5).Resists(2400, materials, Hull, CutShare);

        Assert.True(half.Backed(HullPlace.PortSide, 0.49));
        Assert.False(half.Backed(HullPlace.PortSide, 0.51));
        Assert.Equal(Resistance.Holing(700, 0.005, Hole) + Resistance.Holing(75000, 0.003, Hole), half.Against(Blow.Point, backed: true), 6);
    }

    [Fact]
    public void BarkWithNoLiningYetIsBackedNowhere()
    {
        var canoe = new RigidShell { Material = "bark", Metres = 0.005, Backing = new RigidShell { Material = "wood-oak", Metres = 0.003 }, BackedShare = 1, SpanMetres = 0.25 };

        Assert.Null(canoe.Lined("wood-oak", 0).Backing);
        Assert.False(canoe.Lined("wood-oak", 0).Resists(2400, materials, Hull, CutShare).Backed(HullPlace.Bottom, 0));
    }

    [Fact]
    public void ABreakerBendsTheLiningOnlyAsFarAsItIsLaid()
    {
        var canoe = new RigidShell { Material = "bark", Metres = 0.005, Backing = new RigidShell { Material = "wood-oak", Metres = 0.003 }, BackedShare = 1, SpanMetres = 0.25 };
        var bark = new RigidShell { Material = "bark", Metres = 0.005, SpanMetres = 0.25 };
        double full = canoe.Slammed(materials), bare = bark.Slammed(materials);

        Assert.Equal(bare + (full - bare) / 2, canoe.Lined("wood-oak", 0.5).Slammed(materials), 6);
    }

    [Fact]
    public void TheUncutWoodOfABlankBacksItsSideByItsThickness()
    {
        var side = new CarvedShell { Material = "wood-oak", Metres = 0.05, SpanMetres = 0.5 };
        var blank = side.Uncut(0.4, 0).Resists(30000, materials, Hull, CutShare);

        Assert.True(blank.Backed(HullPlace.StarboardSide, 0.99));
        Assert.True(blank.Backed(HullPlace.Bottom, 0.99));
        Assert.Equal(Resistance.Holing(75000, 0.05, Hole) + Resistance.Holing(75000, 0.4, Hole), blank.Against(Blow.Point, backed: true), 6);
    }

    [Fact]
    public void AsTheHollowIsCutOnlyTheSideBelowItStaysBackedAndTheBottomUntilTheLast()
    {
        var side = new CarvedShell { Material = "wood-oak", Metres = 0.05, SpanMetres = 0.5 };
        var cut = side.Uncut(0.07, 5.0 / 6).Resists(30000, materials, Hull, CutShare);

        Assert.True(cut.Backed(HullPlace.PortSide, 0.16));
        Assert.False(cut.Backed(HullPlace.PortSide, 0.17));
        Assert.True(cut.Backed(HullPlace.Bottom, 0.99));
        Assert.Same(side, side.Uncut(0, 1));
    }

    [Fact]
    public void ABundleOfStemsSpringsOffEveryBlowLashingsAndAll()
    {
        var resists = Reeds.Resists(3000, materials, Hull, CutShare);

        Assert.Equal(double.PositiveInfinity, resists.Against(Blow.Point, backed: false));
        Assert.Equal(double.PositiveInfinity, resists.Against(Blow.Point, backed: true));
    }

    [Fact]
    public void AnEdgePartsTheStemsItMeetsWithoutHarm()
    {
        Assert.Equal(double.PositiveInfinity, Reeds.Resists(3000, materials, Hull, CutShare).Against(Blow.Cut, backed: false));
    }

    [Fact]
    public void ABundleComesApartWhereAnEdgeCutsItsLashing()
    {
        Assert.Equal(Resistance.Holing(5700, 0.022, Hole), Reeds.Resists(3000, materials, Hull, CutShare).Against(Blow.Cut, backed: true), 6);
    }

    [Fact]
    public void AHullWithNoShellResistsEveryBlowWithItsWholeFrame()
    {
        var whole = Resistance.Whole(3000);

        Assert.All([Blow.Broad, Blow.Point, Blow.Cut], blow => Assert.Equal(3000, whole.Against(blow, backed: false)));
    }

    [Fact]
    public void AHullWithNoShellHasNoPlankingForABreakerToBend() =>
        Assert.Equal(double.PositiveInfinity, Resistance.Whole(3000).Against(Blow.Slam, backed: false));

    [Fact]
    public void ABreakerBendsAPlankBetweenTheFramesItIsFastenedTo()
    {
        var plank = new RigidShell { Material = "wood-oak", Metres = 0.02, SpanMetres = 0.25 };

        Assert.Equal(2 * 100e6 * 0.02 * 0.02 / (0.25 * 0.25) * Soaked, plank.Resists(30000, materials, Hull, CutShare).Against(Blow.Slam, backed: false), 3);
    }

    [Fact]
    public void ABreakerBendsACarvedSideFromTheBottomItStandsOn()
    {
        var side = new CarvedShell { Material = "wood-oak", Metres = 0.05, SpanMetres = 0.5 };

        Assert.Equal(100e6 * 0.05 * 0.05 / (3 * 0.5 * 0.5) * Soaked, side.Resists(30000, materials, Hull, CutShare).Against(Blow.Slam, backed: false), 3);
    }

    [Fact]
    public void BarkAndTheSheathingThatLinesItBendTogetherBetweenTheRibs()
    {
        var canoe = new RigidShell
        {
            Material = "bark", Metres = 0.005, SpanMetres = 0.25, Backing = new RigidShell { Material = "wood-oak", Metres = 0.003 }, BackedShare = 1
        };

        Assert.Equal(2 * (114e6 * 0.005 * 0.005 + 100e6 * 0.003 * 0.003) / (0.25 * 0.25) * Soaked,
            canoe.Resists(2400, materials, Hull, CutShare).Against(Blow.Slam, backed: false), 3);
    }

    [Fact]
    public void ABreakerPressesASkinOntoItsLathsEachBendingUnderItsShareOfTheSide() =>
        Assert.Equal(104e6 * 0.02 * 0.02 * 8 / (6 * 0.6 * 0.6) * 0.3 * Soaked,
            Skin.Resists(3000, materials, Hull, CutShare).Against(Blow.Slam, backed: false), 3);

    [Fact]
    public void ABreakerBendsABundleOfStemsWhichSpringBack() =>
        Assert.Equal(double.PositiveInfinity, Reeds.Resists(3000, materials, Hull, CutShare).Against(Blow.Slam, backed: false));
}
