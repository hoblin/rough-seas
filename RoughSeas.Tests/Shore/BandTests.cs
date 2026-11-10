using RoughSeas.Shore;

namespace RoughSeas.Tests.Shore;

public class BandTests
{
    [Fact]
    public void ASheetClimbsOneRiserButStopsAtASecondWithNoTreadBetween()
    {
        var reached = Spread(new Ground("~~012"), new Sea(8, 12));
        Assert.Contains((3, 0), reached.Keys);
        Assert.DoesNotContain((4, 0), reached.Keys);
    }

    [Fact]
    public void ASheetClimbsARiserAfterATread()
    {
        var reached = Spread(new Ground("~~0112"), new Sea(8, 12));
        Assert.Contains((5, 0), reached.Keys);
    }

    [Fact]
    public void ASheetStopsAtAStepHigherThanItsSlopeAllows()
    {
        var reached = Spread(new Ground("~~02"), new Sea(8, 12));
        Assert.DoesNotContain((3, 0), reached.Keys);
    }

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
    public void TheBandCarriesTheWaterOfTheShoreItCameFrom()
    {
        var band = Spread(new Ground("~~0000000000000000000000000000000000000000=="), Storm);
        Assert.Equal(1, band[(2, 0)].Fluid);
        Assert.Equal(2, band[(41, 0)].Fluid);
    }

    [Fact]
    public void TheBandCountsTheBlocksInFromTheSea()
    {
        var band = Spread(new Ground("~~0000"), Calm);
        Assert.Equal([1, 2, 3], new[] { 2, 3, 4 }.Select(x => band[(x, 0)].Rings));
    }

    [Fact]
    public void TheStrongerShoresSheetTakesTheLandBetweenThem()
    {
        var ground = new Ground("~~00000=");
        var band = ShoreFixtures.Band.Spread(ground, ground.Seeds(), (x, _) => x < 3 ? [Calm] : [Storm]);
        Assert.Equal(7, band[(3, 0)].FromX);
    }

    [Fact]
    public void TheSheetSpreadsAlongTheShoreAsWellAsUpIt()
    {
        var band = Spread(new Ground("~~000", ".~000", "..000"), Calm);
        Assert.Contains((2, 2), band.Keys);
    }

    [Fact]
    public void ACalmSeaWithoutWavesGivesNoBand() => Assert.Empty(Spread(new Ground("~~0000"), new Sea(0, 0)));
}
