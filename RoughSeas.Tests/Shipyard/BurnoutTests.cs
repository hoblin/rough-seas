using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using RoughSeas.Tests.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Tests.Shipyard;

public class BurnoutTests
{
    private static readonly BurnoutConfig Config = JsonConvert.DeserializeObject<BurnoutConfig>(
        JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", "dugout.json")))["behaviorConfigs"]![EntityBehaviorBurnout.Code]!.ToString())!;

    private TreeAttribute? tree;
    private int changes;

    private Burnout Dugout() => new(() => tree!, () => changes++, Config);

    private Burnout Blank()
    {
        tree = new TreeAttribute();
        Burnout.Begin(tree);
        return Dugout();
    }

    [Fact]
    public void ADugoutFromBeforeTheBuildIsFinished()
    {
        var dugout = Dugout();

        Assert.True(dugout.Finished);
        Assert.Equal(1, dugout.Hollowed);
    }

    [Fact]
    public void TheFireLaidOnTheLogsChartsTheTopWithoutClay()
    {
        var blank = Blank();

        Assert.True(blank.Lit);
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
        blank.Burn(blank.HoursPerFire);
        Assert.Null(blank.HewRefusal());
    }

    [Fact]
    public void BelowTheRimTheFireWaitsForClay()
    {
        var blank = Blank();
        blank.Burn(blank.HoursPerFire);
        blank.Hew();

        Assert.Equal("shipyard-clay-first", blank.LightRefusal());
        blank.Clay();
        Assert.Null(blank.LightRefusal());
    }

    [Fact]
    public void TheFireSpendsTheClay()
    {
        var blank = Blank();
        blank.Burn(blank.HoursPerFire);
        blank.Hew();
        blank.Clay();
        blank.Light();
        blank.Burn(blank.HoursPerFire);

        Assert.False(blank.Clayed);
    }

    [Fact]
    public void RainPutsTheFireOutAndTheRestBurnsWhenRelit()
    {
        var blank = Blank();
        blank.Burn(blank.HoursPerFire / 2);
        blank.Douse();

        Assert.False(blank.Burn(blank.HoursPerFire));
        blank.Light();
        Assert.True(blank.Burn(blank.HoursPerFire / 2));
    }

    [Fact]
    public void EachHewingCutsMoreOfTheHoldUntilSheIsFinished()
    {
        var blank = Blank();
        var hollowed = new List<double> { blank.Hollowed };
        while (!blank.Finished)
        {
            if (blank.NeedsClay) blank.Clay();
            if (!blank.Lit) blank.Light();
            blank.Burn(blank.HoursPerFire);
            blank.Hew();
            hollowed.Add(blank.Hollowed);
        }

        Assert.Equal(hollowed.Order(), hollowed);
        Assert.Equal(Config.Hollowings + 1, hollowed.Count);
        Assert.Equal(1, blank.Hollowed);
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
