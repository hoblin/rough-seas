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
        var blank = Charred(Blank());
        Hew(blank);
        return blank;
    }

    [Fact]
    public void ADugoutFromBeforeTheBuildIsFinished()
    {
        var dugout = Dugout();

        Assert.True(dugout.Finished);
        Assert.Equal(1, dugout.Hollowed);
    }

    [Fact]
    public void DryGrassOnTheLogsLaysTheBlankWithItsFiresBase()
    {
        var blank = Blank();

        Assert.Equal(0, blank.Level);
        Assert.False(blank.Lit);
        Assert.True(blank.Fire.Kindled);
        Assert.False(blank.Fire.Laid);
        Assert.Equal("shipyard-kindled-already", blank.KindlingRefusal());
    }

    [Fact]
    public void OnlyCharredWoodHewsAway()
    {
        var blank = Blank();

        Assert.Equal("shipyard-burn-first", blank.HewRefusal());
        Charred(blank);
        Assert.Null(blank.HewRefusal());
    }

    [Fact]
    public void TheRimTakesBallsOfClayByVanillasVoxelsOverItsOutline()
    {
        var blank = Blank();

        Assert.Equal(560, blank.RimVoxels);
        Assert.Equal(23, blank.ClayBalls);
    }

    [Fact]
    public void EachBallCoatsItsVoxelsUntilTheRimIsGuarded()
    {
        var blank = Blank();
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
        var blank = Blank();
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
        var blank = Blank();
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
        var blank = Blank();
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
    public void TheFireSpendsTheClayAndItsWood()
    {
        var blank = Hollowed();
        ClayTheRim(blank);
        LayTheFire(blank);
        blank.Light();
        blank.Burn(blank.HoursPerFire);

        Assert.Equal(0, blank.ClayVoxels);
        Assert.False(blank.Fire.Kindled);
        Assert.False(blank.Fire.Laid);
    }

    [Fact]
    public void RainPutsTheFireOutAndTheRestBurnsWhenRelit()
    {
        var blank = Blank();
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
        var blank = Charred(Blank());

        Assert.Equal(32, blank.StrokesPerHewing);
        for (int stroke = 1; stroke < blank.StrokesPerHewing; stroke++) Assert.False(blank.Stroke());
        Assert.Equal(0, blank.Level);
        Assert.True(blank.Stroke());
        Assert.Equal(1, blank.Level);
        Assert.False(blank.Charred);
    }

    [Fact]
    public void EachHewingCutsMoreOfTheHoldUntilSheIsFinished()
    {
        var blank = Blank();
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
