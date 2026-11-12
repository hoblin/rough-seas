using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class MooringLoadTests
{
    [Fact]
    public void OneLineStopsHerWhole()
    {
        Assert.Equal([1.0], MooringLoad.Masses(0.5, 0, [(1, 0)], [20000]));
    }

    [Fact]
    public void TwoLinesSheStretchesTogetherShareHerByTheirStiffness()
    {
        var masses = MooringLoad.Masses(0.6, 0, [(1, 0), (1, 0)], [20000, 5000]);

        Assert.Equal(0.8, masses[0], 9);
        Assert.Equal(0.2, masses[1], 9);
    }

    [Fact]
    public void SwungAcrossTheLineThatHoldsHerTheNextLineTakesTheJerk()
    {
        var masses = MooringLoad.Masses(0, 0.7, [(1, 0), (0, 1)], [20000, 5000]);

        Assert.Equal([0.0, 1.0], masses);
    }

    [Fact]
    public void ALineTooStiffToStretchStopsHerWhole()
    {
        Assert.Equal([0.0, 1.0], MooringLoad.Masses(0.6, 0, [(1, 0), (1, 0)], [20000, double.PositiveInfinity]));
    }

    [Fact]
    public void OneLineCarriesThePullAlongIt()
    {
        Assert.Equal([300.0], MooringLoad.Share(300, 400, [(1, 0)]));
    }

    [Fact]
    public void TwoLinesSplitThePullSoTheirTensionsAddUpToIt()
    {
        var half = System.Math.Sqrt(0.5);
        var shares = MooringLoad.Share(0, 1000, [(half, half), (-half, half)]);

        Assert.Equal(1000 / (2 * half), shares[0], 6);
        Assert.Equal(1000 / (2 * half), shares[1], 6);
    }

    [Fact]
    public void ALineTheBalanceWouldPushGoesSlackAndTheOtherTakesItsShareAlongIt()
    {
        Assert.Equal([1000.0, 0.0], MooringLoad.Share(1000, -500, [(1, 0), (0, 1)]));
        Assert.Equal([0.0, 1000.0], MooringLoad.Share(-500, 1000, [(1, 0), (0, 1)]));
        Assert.Equal([0.0, 0.0], MooringLoad.Share(-1000, -1000, [(1, 0), (0, 1)]));
    }

    [Fact]
    public void OfTwoLinesFarApartTheOneThePullRunsAlongTakesItWhicheverComesFirst()
    {
        Assert.Equal([700.0, 0.0], MooringLoad.Share(700, -600, [(1, 0), (-0.8, 0.6)]));
        Assert.Equal([0.0, 700.0], MooringLoad.Share(700, -600, [(-0.8, 0.6), (1, 0)]));
    }

    [Fact]
    public void TwoLinesTheSameWayShareThePullEvenly()
    {
        Assert.Equal([500.0, 500.0], MooringLoad.Share(1000, 0, [(1, 0), (1, 0)]));
    }

    [Fact]
    public void ALineSlidingAtItsHoldLeavesTheRestOfThePullToTheOthers()
    {
        Assert.Equal([200.0, 800.0], MooringLoad.Beside(1000, 0, [(1, 0), (1, 0)], sliding: 0, slidingNewtons: 200));
        Assert.Equal([800.0, 200.0], MooringLoad.Beside(1000, 0, [(1, 0), (1, 0)], sliding: 1, slidingNewtons: 200));
    }

    [Fact]
    public void APullTowardTheMooringLoadsNoLine()
    {
        Assert.Equal([0.0], MooringLoad.Share(-500, 0, [(1, 0)]));
    }
}
