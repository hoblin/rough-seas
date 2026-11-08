using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class MooringLineTests
{
    private const double Sailboat = 1900;

    private static MooringLine Flax(double metres) => new(metres, Fixtures.FlaxNewtons, 0.1);

    [Fact]
    public void ItsStiffnessIsItsBreakingLoadOverTheStretchItBreaksAt()
    {
        Assert.Equal(Fixtures.FlaxNewtons / 0.1 / 3, Flax(3).NewtonsPerMetre, 6);
    }

    [Fact]
    public void HeldTautItCarriesTheSteadyPullAlongIt()
    {
        Assert.Equal(400, Flax(3).Tension(400, 1.5, Sailboat, wasTaut: true));
    }

    [Fact]
    public void ALoadComingOnASlackRopeAtRestDoublesOnIt()
    {
        Assert.Equal(800, Flax(3).Tension(400, 0, Sailboat, wasTaut: false), 6);
    }

    [Fact]
    public void AHullComingTautWithNoOtherPullPullsBySpeedTimesTheRootOfStiffnessAndMass()
    {
        var line = Flax(3);
        Assert.Equal(1.0 * System.Math.Sqrt(line.NewtonsPerMetre * Sailboat), line.Tension(0, 1.0, Sailboat, wasTaut: false), 6);
    }

    [Fact]
    public void ALongerRopeSnatchesSofter()
    {
        Assert.True(Flax(10).Tension(0, 1.0, Sailboat, false) < Flax(3).Tension(0, 1.0, Sailboat, false));
    }

    [Fact]
    public void AHeavierHullSnatchesHarder()
    {
        Assert.True(Flax(3).Tension(0, 1.0, Sailboat, false) > Flax(3).Tension(0, 1.0, 120, false));
    }

    [Fact]
    public void AHullMovingTowardThePostPullsNothing()
    {
        Assert.Equal(0, Flax(3).Tension(-50, -1.0, Sailboat, wasTaut: false));
    }

    [Fact]
    public void ASailboatSurgedOutByAStormSeaPartsAShortRopeAndNotALongOne()
    {
        const double orbitalMetresPerSecond = 1.6;
        Assert.True(Flax(3).Parts(Flax(3).Tension(0, orbitalMetresPerSecond, Sailboat, false)));
        Assert.False(Flax(10).Parts(Flax(10).Tension(0, orbitalMetresPerSecond, Sailboat, false)));
    }

    [Fact]
    public void ItPartsOnlyPastItsBreakingLoad()
    {
        Assert.False(Flax(3).Parts(Fixtures.FlaxNewtons));
        Assert.True(Flax(3).Parts(Fixtures.FlaxNewtons + 1));
    }
}
