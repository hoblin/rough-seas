using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class BurnoutTests
{
    private static readonly JToken Entity = JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "dugout.json")));
    private static readonly BurnoutConfig Config = JsonConvert.DeserializeObject<BurnoutConfig>(Entity["behaviorConfigs"]![EntityBehaviorBurnout.Code]!.ToString())!;
    private static readonly double HoldLitres = Entity["behaviorConfigs"]!["roughseas:bilge"]!.Value<double>("capacityLitres");
    private static readonly Shape DugoutShape = JsonConvert.DeserializeObject<Shape>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "dugout.json")))!;

    private TreeAttribute? tree;
    private int changes;

    private Burnout Dugout() => new(() => tree!, () => changes++, Config, RimVoxels, () => HoldLitres);

    private static int RimVoxels(int level) => Burnout.TopVoxels(DugoutShape.GetElementByName($"Clay{level}"));

    private Burnout Blank()
    {
        tree = new TreeAttribute();
        Burnout.Begin(tree);
        return Dugout();
    }

    private Burnout Bare()
    {
        var blank = Blank();
        while (!blank.Stroke()) { }
        return blank;
    }

    private static Burnout Charred(Burnout blank)
    {
        ClayTheRim(blank);
        LayTheFire(blank);
        blank.Light();
        blank.Burn(blank.HoursPerFire);
        return blank;
    }

    private static void Hew(Burnout blank)
    {
        while (!blank.Stroke()) { }
    }

    private static void ClayTheRim(Burnout blank)
    {
        while (!blank.Clayed) blank.Clay();
    }

    private static void LayTheFire(Burnout blank)
    {
        blank.Fire.Kindle();
        while (!blank.Fire.Laid) blank.Fire.AddFirewood();
    }

    private Burnout Hollowed()
    {
        var blank = Charred(Bare());
        Hew(blank);
        return blank;
    }

    [Fact]
    public void HerHullBoxSinksWithTheTrunkAtEachHewingAndKeepsTheLogsLength()
    {
        Assert.Equal(Config.Hollowings, Config.HullBoxesMetres.Length);
        Assert.Equal(Config.BlankDepthMetres, Config.HullBoxesMetres[0].To[1], 2);
        for (int level = 1; level < Config.Hollowings; level++)
        {
            Assert.True(Config.HullBoxesMetres[level].To[1] < Config.HullBoxesMetres[level - 1].To[1]);
            Assert.Equal(Config.HullBoxesMetres[0].To[0] - Config.HullBoxesMetres[0].From[0],
                Config.HullBoxesMetres[level].To[0] - Config.HullBoxesMetres[level].From[0], 3);
        }
    }

    [Fact]
    public void EveryHewnSectionClosesAgainstTheCharOfTheNext()
    {
        int sections = Config.HewnSectionsMetres.Length;
        for (int level = 0; level < Config.Hollowings; level++)
        {
            for (int section = 1; section < sections; section++) Assert.NotNull(DugoutShape.GetElementByName($"Hewn{level}x{section}Step"));
            Assert.Null(DugoutShape.GetElementByName($"Hewn{level}x{sections}Step"));
        }
    }

    [Fact]
    public void ADugoutFromBeforeTheBuildIsFinished()
    {
        var dugout = Dugout();

        Assert.True(dugout.Finished);
        Assert.Equal(1, dugout.Hollowed);
    }

    [Fact]
    public void AnAxeOnTheLogsLaysTheBlankInItsBark()
    {
        var blank = Blank();

        Assert.Equal(0, blank.Level);
        Assert.False(blank.Debarked);
        Assert.Equal(0, blank.BarkStripped);
        Assert.False(blank.Fire.Kindled);
    }

    [Fact]
    public void NothingButTheAxeWorksATrunkInItsBark()
    {
        var blank = Blank();

        Assert.All(new[] { blank.ClayRefusal(), blank.FirewoodRefusal(), blank.KindlingRefusal(), blank.LightRefusal() },
            refusal => Assert.Equal("shipyard-debark-first", refusal));
        Assert.Null(blank.HewRefusal());
    }

    [Fact]
    public void EachStrokeStripsTheBarkFromOneSideOfALog()
    {
        var blank = Blank();

        for (int piece = 1; piece < Config.BarkPieces; piece++)
        {
            Assert.False(blank.Stroke());
            Assert.Equal(piece, blank.BarkStripped);
        }
        Assert.True(blank.Stroke());
        Assert.True(blank.Debarked);
        Assert.Equal(0, blank.Level);
        Assert.Equal("shipyard-burn-first", blank.HewRefusal());
    }

    [Fact]
    public void OnlyCharredWoodHewsAway()
    {
        var blank = Bare();

        Assert.Equal("shipyard-burn-first", blank.HewRefusal());
        Charred(blank);
        Assert.Null(blank.HewRefusal());
    }

    [Fact]
    public void TheRimTakesBallsOfClayByVanillasVoxelsOverItsOutline()
    {
        var blank = Bare();

        Assert.Equal(461, blank.RimVoxels);
        Assert.Equal(19, blank.ClayBalls);
    }

    [Fact]
    public void EachBallCoatsItsVoxelsUntilTheRimIsGuarded()
    {
        var blank = Bare();
        for (int ball = 1; ball < blank.ClayBalls; ball++) blank.Clay();

        Assert.False(blank.Clayed);
        blank.Clay();
        Assert.True(blank.Clayed);
        Assert.Equal("shipyard-clayed-already", blank.ClayRefusal());
    }

    [Fact]
    public void AFireIsLaidAsACampfireIs()
    {
        var blank = Hollowed();

        Assert.Equal("shipyard-lay-fire-first", blank.LightRefusal());
        Assert.Equal("shipyard-kindle-first", blank.FirewoodRefusal());
        blank.Fire.Kindle();
        Assert.Equal("shipyard-kindled-already", blank.KindlingRefusal());
        for (int firewood = 0; firewood < LaidFire.FirepitFirewood; firewood++)
        {
            Assert.Null(blank.FirewoodRefusal());
            blank.Fire.AddFirewood();
        }
        Assert.True(blank.Fire.Laid);
        Assert.Equal("shipyard-laid-already", blank.FirewoodRefusal());
        Assert.Null(blank.LightRefusal());
    }

    [Fact]
    public void AFireLightsWithoutClayAndBurnsTheRimUnguarded()
    {
        var blank = Bare();
        LayTheFire(blank);

        Assert.Null(blank.LightRefusal());
        blank.Light();
        blank.Burn(blank.HoursPerFire);
        Assert.Equal(blank.HoursPerFire * Config.CharMillimetresPerHour / 1000, blank.RimCharMetres, 6);
        Assert.True(blank.Spoiled);
    }

    [Fact]
    public void TheRimSpoilsOnceItsCharReachesThroughTheWall()
    {
        var blank = Bare();
        LayTheFire(blank);
        blank.Light();
        double throughTheWall = Config.WallMetres * 1000 / Config.CharMillimetresPerHour;

        blank.Burn(throughTheWall * 0.99);
        Assert.False(blank.Spoiled);
        blank.Burn(throughTheWall * 0.02);
        Assert.True(blank.Spoiled);
    }

    [Fact]
    public void ASpoiledBlankTakesNoMoreWork()
    {
        var blank = Bare();
        LayTheFire(blank);
        blank.Light();
        blank.Burn(blank.HoursPerFire);

        Assert.All(new[] { blank.ClayRefusal(), blank.FirewoodRefusal(), blank.KindlingRefusal(), blank.LightRefusal(), blank.HewRefusal() },
            refusal => Assert.Equal("shipyard-spoiled", refusal));
    }

    [Fact]
    public void AGuardedFireLeavesTheRimSound()
    {
        var blank = Hollowed();

        Assert.Equal(0, blank.RimCharMetres);
        Assert.False(blank.Spoiled);
    }

    [Fact]
    public void TheFireSpendsItsWoodAndTheHewingTakesTheClayWithTheRim()
    {
        var blank = Hollowed();
        ClayTheRim(blank);
        LayTheFire(blank);
        blank.Light();
        blank.Burn(blank.HoursPerFire);

        Assert.False(blank.Fire.Kindled);
        Assert.False(blank.Fire.Laid);
        Assert.True(blank.Clayed);
        Hew(blank);
        Assert.Equal(0, blank.ClayVoxels);
        Assert.False(blank.Clayed);
    }

    [Fact]
    public void AFireCatchesFirstThenBurnsDownInEqualShares()
    {
        var blank = Bare();
        ClayTheRim(blank);
        LayTheFire(blank);
        blank.Light();
        double after = blank.HoursPerFire - 1;

        Assert.Equal(0, blank.BurningPhase(3, 1));
        blank.Burn(0.99);
        Assert.Equal(0, blank.BurningPhase(3, 1));
        blank.Burn(0.02);
        Assert.Equal(1, blank.BurningPhase(3, 1));
        blank.Burn(after / 3);
        Assert.Equal(2, blank.BurningPhase(3, 1));
        blank.Burn(after / 3);
        Assert.Equal(3, blank.BurningPhase(3, 1));
    }

    [Fact]
    public void TheFirstBallOfClayNamesTheClayOnTheRim()
    {
        var blank = Bare();
        blank.Clay("red");
        blank.Clay("blue");

        Assert.Equal("red", blank.ClayType);
    }

    [Fact]
    public void RainPutsTheFireOutAndTheRestBurnsWhenRelit()
    {
        var blank = Bare();
        ClayTheRim(blank);
        LayTheFire(blank);
        blank.Light();
        blank.Burn(blank.HoursPerFire / 2);
        blank.Douse();

        Assert.False(blank.Burn(blank.HoursPerFire));
        Assert.True(blank.Fire.Laid);
        blank.Light();
        Assert.True(blank.Burn(blank.HoursPerFire / 2));
    }

    [Fact]
    public void EachStrokeBreaksTheCrumblingCharAwayAcrossTheHollow()
    {
        var blank = Charred(Bare());

        Assert.Equal(36, blank.StrokesPerHewing);
        for (int stroke = 1; stroke < blank.StrokesPerHewing; stroke++) Assert.False(blank.Stroke());
        Assert.Equal(0, blank.Level);
        Assert.True(blank.Stroke());
        Assert.Equal(1, blank.Level);
        Assert.False(blank.Charred);
    }

    [Fact]
    public void AHewingCutsTheInsideSectionBySectionThenTrimsTheOutsideInTheSameStrokes()
    {
        var blank = Charred(Bare());
        var stages = new List<(int Cut, int Trimmed)> { (blank.SectionsCut, blank.SectionsTrimmed) };
        while (!blank.Stroke())
        {
            if (stages[^1] != (blank.SectionsCut, blank.SectionsTrimmed)) stages.Add((blank.SectionsCut, blank.SectionsTrimmed));
        }

        Assert.Equal(Enumerable.Range(0, blank.Sections + 1).Select(cut => (cut, 0))
            .Concat(Enumerable.Range(1, blank.Sections - 1).Select(trimmed => (blank.Sections, trimmed))), stages);
        Assert.Equal(1, blank.Level);
        Assert.Equal(0, blank.Stage);
    }

    [Fact]
    public void TheInsideWorkCutsTheHoldAndTheOutsideWorkTrimsTheWoodWithNoStepAtTheHewingsEnd()
    {
        var blank = Charred(Bare());
        while (blank.SectionsCut < blank.Sections) blank.Stroke();

        Assert.Equal(1.0 / Config.Hollowings, blank.Hollowed, 6);
        Assert.Equal(0, blank.Worked);
        Assert.Equal(Config.UnhewnLitres, blank.UnhewnLitres);
        Assert.Equal(Config.BlankDepthMetres, blank.TopMetres);
        Assert.True(blank.FloorMetres < Config.BlankDepthMetres);

        var lastStage = (blank.Hollowed, blank.Worked, blank.FloorMetres);
        while (!blank.Stroke()) lastStage = (blank.Hollowed, blank.Worked, blank.FloorMetres);
        Assert.Equal(lastStage.Hollowed, blank.Hollowed, 6);
        Assert.True(blank.Worked > lastStage.Worked);
        Assert.Equal(1.0 / Config.Hollowings, blank.Worked, 6);
        Assert.Equal(lastStage.FloorMetres, blank.FloorMetres, 6);
    }

    [Theory]
    [InlineData(-2.2, 1)]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(2.2, 3)]
    public void EverythingAlongHerLiesInOneSectionHerEndsInTheirs(double alongMetres, int section)
    {
        Assert.Equal(section, Dugout().SectionAt(alongMetres));
    }

    [Fact]
    public void EachHewingCutsMoreOfTheHoldUntilSheIsFinished()
    {
        var blank = Bare();
        var hollowed = new List<double> { blank.Hollowed };
        while (!blank.Finished)
        {
            Charred(blank);
            Hew(blank);
            hollowed.Add(blank.Hollowed);
        }

        Assert.Equal(hollowed.Order(), hollowed);
        Assert.Equal(Config.Hollowings + 1, hollowed.Count);
        Assert.Equal(1, blank.Hollowed);
        Assert.False(blank.Spoiled);
    }

    [Fact]
    public void TheHollowBurnsOutInTheDaysAMishoonTakes()
    {
        double days = Config.Hollowings * Dugout().HoursPerFire / 24;

        Assert.InRange(days, 6, 10);
    }

    [Theory]
    [InlineData("oak", false)]
    [InlineData("guajacum", true)]
    public void AnUncutBlankFloatsOnItsOwnWoodUnlessItIsHeavierThanWater(string wood, bool sinks)
    {
        var dugout = Fixtures.Hull("dugout", wood);
        var blank = new HullBuild(dugout.Kilograms, dugout.Flotation.BuoyantKg / VesselMass.SeawaterKgPerLitre).WithUncut(dugout.CapacityLitres);
        var solid = new Flotation(dugout.Flotation.DepthMetres, 0, blank.Litres);

        Assert.Equal(sinks, solid.Float(new Aboard(blank.Kilograms, 0, 0, 0, 0, 0), true).Sunk);
    }
}
