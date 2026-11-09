using RoughSeas.Shore;

namespace RoughSeas.Tests.Shore;

public class BandTests
{
    private static readonly Sea Calm = new(1, 6);
    private static readonly Sea Storm = new(8, 12);

    private static Dictionary<(int X, int Z), Reached> Spread(Ground ground, params Sea[] seas) =>
        ShoreFixtures.Band.Spread(ground, ground.Seeds(), (_, _) => seas);

    private static int Reach(Dictionary<(int X, int Z), Reached> band) => band.Keys.Max(cell => cell.X);

    [Fact]
    public void WaterTakesTheBeachFlushWithTheSea()
    {
        var band = Spread(new Ground("~~0000000000"), Calm);
        Assert.Contains((2, 0), band.Keys);
    }

    [Fact]
    public void TheBandStopsAtABankTheSeaCannotRunUp()
    {
        var band = Spread(new Ground("~~00055555"), Calm);
        Assert.Equal(4, Reach(band));
    }

    [Fact]
    public void AStormSendsTheBandFartherInland()
    {
        var beach = new Ground("~~0000000000000000000000000000000000000000000000000000000000");
        Assert.True(Reach(Spread(beach, Storm)) > Reach(Spread(beach, Calm)));
    }

    [Fact]
    public void TheBandAsksTheHigherOfTheSeasItIsGiven()
    {
        var beach = new Ground("~~0000000000000000000000000000000000000000000000000000000000");
        Assert.Equal(Reach(Spread(beach, Storm)), Reach(Spread(beach, Calm, Storm)));
    }

    [Fact]
    public void ARockShelfAtSeaLevelTakesWaterToo() => Assert.Contains((2, 0), Spread(new Ground("~~aaaa"), Calm).Keys);

    [Fact]
    public void TheBandGoesNoFurtherThanLandRuns() => Assert.DoesNotContain((5, 0), Spread(new Ground("~~000.0"), Storm).Keys);

    [Fact]
    public void TheBandCarriesTheWaterOfTheShoreItCameFrom() => Assert.All(Spread(new Ground("~~000"), Calm).Values, reached => Assert.Equal(1, reached.Fluid));

    [Fact]
    public void ACalmSeaWithoutWavesGivesNoBand() => Assert.Empty(Spread(new Ground("~~0000"), new Sea(0, 0)));
}
