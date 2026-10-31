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
    private static readonly int RimVoxels = Burnout.TopVoxels(JsonConvert.DeserializeObject<Shape>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "dugout.json")))!.GetElementByName("Clay"));

    private TreeAttribute? tree;
    private int changes;

    private Burnout Dugout() => new(() => tree!, () => changes++, Config, RimVoxels, () => HoldLitres);

    private Burnout Blank()
    {
        tree = new TreeAttribute();
        Burnout.Begin(tree);
        return Dugout();
    }

    private static Burnout Charred(Burnout blank)
    {
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
        while (blank.Fire.OnPile < blank.Fire.PileFirewood) blank.Fire.Add(8);
        blank.Fire.Kindle();
        while (!blank.Fire.Laid) blank.Fire.Add(1);
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
    public void TheCharcoalPitOnTheLogsCharsTheTopWithoutClay()
    {
        var blank = Blank();

        Assert.True(blank.Lit);
        Assert.True(blank.Fire.Laid);
        Assert.False(blank.NeedsClay);
        Assert.False(blank.Burn(blank.HoursPerFire / 2));
        Assert.True(blank.Burn(blank.HoursPerFire / 2));
        Assert.True(blank.Charred);
        Assert.False(blank.Lit);
    }

    [Fact]
    public void OnlyCharredWoodHewsAway()
    {
        var blank = Blank();

        Assert.Equal("shipyard-burning", blank.HewRefusal());
        Charred(blank);
        Assert.Null(blank.HewRefusal());
    }

    [Fact]
    public void TheRimTakesBallsOfClayByVanillasVoxelsOverItsOutline()
    {
        var dugout = Dugout();

        Assert.Equal(268, dugout.RimVoxels);
        Assert.Equal(11, dugout.ClayBalls);
    }

    [Fact]
    public void EachBallCoatsItsVoxelsUntilTheRimIsGuarded()
    {
        var blank = Hollowed();
        for (int ball = 1; ball < blank.ClayBalls; ball++) blank.Clay();

        Assert.False(blank.Clayed);
        blank.Clay();
        Assert.True(blank.Clayed);
        Assert.Equal("shipyard-clayed-already", blank.ClayRefusal());
    }

    [Fact]
    public void AFireIsLaidAsForACharcoalPit()
    {
        var blank = Hollowed();
        ClayTheRim(blank);

        Assert.Equal("shipyard-lay-fire-first", blank.LightRefusal());
        Assert.Equal("shipyard-pile-first", blank.KindlingRefusal());
        Assert.Equal(2, blank.Fire.Add(2));
        Assert.Equal("shipyard-pile-first", blank.FirewoodRefusal(piles: false));
        while (blank.Fire.OnPile < blank.Fire.PileFirewood) blank.Fire.Add(8);
        Assert.Equal("shipyard-kindle-first", blank.FirewoodRefusal(piles: true));
        blank.Fire.Kindle();
        for (int firewood = 0; firewood < LaidFire.FirepitFirewood; firewood++)
        {
            Assert.Null(blank.FirewoodRefusal(piles: false));
            Assert.Equal(1, blank.Fire.Add(8));
        }
        Assert.True(blank.Fire.Laid);
        Assert.Equal("shipyard-laid-already", blank.FirewoodRefusal(piles: true));
        Assert.Null(blank.LightRefusal());
    }

    [Fact]
    public void ThePileTakesOnlyWhatItLacks()
    {
        var blank = Hollowed();
        while (blank.Fire.OnPile < blank.Fire.PileFirewood - 3) blank.Fire.Add(1);

        Assert.Equal(3, blank.Fire.Add(8));
    }

    [Fact]
    public void BelowTheRimTheFireWaitsForClay()
    {
        var blank = Hollowed();
        LayTheFire(blank);

        Assert.Equal("shipyard-clay-first", blank.LightRefusal());
        ClayTheRim(blank);
        Assert.Null(blank.LightRefusal());
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
        Assert.Equal(0, blank.Fire.OnPile);
        Assert.False(blank.Fire.Kindled);
        Assert.False(blank.Fire.Laid);
    }

    [Fact]
    public void RainPutsTheFireOutAndTheRestBurnsWhenRelit()
    {
        var blank = Blank();
        blank.Burn(blank.HoursPerFire / 2);
        blank.Douse();

        Assert.False(blank.Burn(blank.HoursPerFire));
        Assert.True(blank.Fire.Laid);
        blank.Light();
        Assert.True(blank.Burn(blank.HoursPerFire / 2));
    }

    [Fact]
    public void HewingTakesAStrokeForEveryChipOfTheDaysChar()
    {
        var blank = Charred(Blank());

        Assert.Equal(106, blank.StrokesPerHewing);
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
            if (blank.NeedsClay) ClayTheRim(blank);
            if (!blank.Fire.Laid) LayTheFire(blank);
            if (!blank.Lit) blank.Light();
            blank.Burn(blank.HoursPerFire);
            Hew(blank);
            hollowed.Add(blank.Hollowed);
        }

        Assert.Equal(hollowed.Order(), hollowed);
        Assert.Equal(Config.Hollowings + 1, hollowed.Count);
        Assert.Equal(1, blank.Hollowed);
    }

    [Fact]
    public void ABlankFromBeforeKeepsItsClayAndItsFire()
    {
        tree = new TreeAttribute();
        tree.SetInt("level", 2);
        tree.SetBool("clayed", true);
        tree.SetDouble("burntHours", 3);
        var blank = Dugout();
        blank.Migrate();

        Assert.True(blank.Clayed);
        Assert.True(blank.Fire.Laid);
        Assert.Null(blank.LightRefusal());
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
