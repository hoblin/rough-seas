using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class MooringLineTests
{
    private const double SailboatWithItsWater = 2400;

    private static MooringLine Flax(double metres) => new(metres, Fixtures.FlaxNewtons, Fixtures.FlaxStretchAtBreak);

    [Fact]
    public void ItsStiffnessIsItsBreakingLoadOverTheStretchItBreaksAt()
    {
        Assert.Equal(0.1, Fixtures.FlaxStretchAtBreak);
        Assert.Equal(Fixtures.FlaxNewtons / 0.1 / 3, Flax(3).NewtonsPerMetre, 6);
    }

    [Fact]
    public void HeldTautItCarriesTheSteadyPullAlongIt()
    {
        Assert.Equal(400, Flax(3).Tension(400, 1.5, SailboatWithItsWater, snatches: false));
    }

    [Fact]
    public void ALoadComingOnASlackRopeAtRestDoublesOnIt()
    {
        Assert.Equal(800, Flax(3).Tension(400, 0, SailboatWithItsWater, snatches: true), 6);
    }

    [Fact]
    public void AHullComingTautWithNoOtherPullPullsBySpeedTimesTheRootOfStiffnessAndMass()
    {
        var line = Flax(3);
        Assert.Equal(1.0 * System.Math.Sqrt(line.NewtonsPerMetre * SailboatWithItsWater), line.Tension(0, 1.0, SailboatWithItsWater, snatches: true), 6);
    }

    [Fact]
    public void ALongerRopeSnatchesSofter()
    {
        Assert.True(Flax(10).Tension(0, 1.0, SailboatWithItsWater, true) < Flax(3).Tension(0, 1.0, SailboatWithItsWater, true));
    }

    [Fact]
    public void AHeavierHullSnatchesHarder()
    {
        Assert.True(Flax(3).Tension(0, 1.0, SailboatWithItsWater, true) > Flax(3).Tension(0, 1.0, 120, true));
    }

    [Fact]
    public void AHullMovingTowardThePostPullsNothing()
    {
        Assert.Equal(0, Flax(3).Tension(-50, -1.0, SailboatWithItsWater, snatches: true));
    }

    [Fact]
    public void ASailboatSurgedOutByAStormSeaPartsAShortRopeAndNotALongOne()
    {
        const double orbitalMetresPerSecond = 1.6;
        Assert.True(Flax(3).Parts(Flax(3).Tension(0, orbitalMetresPerSecond, SailboatWithItsWater, true)));
        Assert.False(Flax(10).Parts(Flax(10).Tension(0, orbitalMetresPerSecond, SailboatWithItsWater, true)));
    }

    [Fact]
    public void AnUnbreakableRopeComingOnAtRestStillDoublesTheLoad()
    {
        var unbreakable = new MooringLine(3, double.PositiveInfinity, 0.1);

        Assert.Equal(800, unbreakable.Tension(400, 0, SailboatWithItsWater, snatches: true));
        Assert.False(unbreakable.Parts(unbreakable.Tension(400, 0, SailboatWithItsWater, snatches: true)));
    }

    [Fact]
    public void ItPartsOnlyPastItsBreakingLoad()
    {
        Assert.False(Flax(3).Parts(Fixtures.FlaxNewtons));
        Assert.True(Flax(3).Parts(Fixtures.FlaxNewtons + 1));
    }
}
