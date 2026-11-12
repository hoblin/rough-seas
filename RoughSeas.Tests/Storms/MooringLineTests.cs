using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class MooringLineTests
{
    private const double SailboatWithItsWater = 2400;
    private const double Tick = 1 / 30.0;
    private const double ComingTaut = 1e-9;
    private const double Long = 60;

    private static MooringLine Flax(double metres) => new(metres, Fixtures.FlaxNewtons, Fixtures.FlaxStretchAtBreak);

    private static double Peak(MooringLine line, double steady, double speed, double kilograms) =>
        line.Over(ComingTaut, speed, steady, kilograms, Long, 1).PeakNewtons;

    [Fact]
    public void ItsStiffnessIsItsBreakingLoadOverTheStretchItBreaksAt()
    {
        Assert.Equal(0.1, Fixtures.FlaxStretchAtBreak);
        Assert.Equal(Fixtures.FlaxNewtons / 0.1 / 3, Flax(3).NewtonsPerMetre, 6);
    }

    [Fact]
    public void HeldTautItCarriesTheSteadyPullAlongItAndGivesItBack()
    {
        var line = Flax(3);
        var step = line.Over(400 / line.NewtonsPerMetre, 0, 400, SailboatWithItsWater, Tick, 1);

        Assert.Equal(400, step.PeakNewtons, 6);
        Assert.Equal(-400 * Tick / SailboatWithItsWater, step.Kick, 9);
    }

    [Fact]
    public void ALoadComingOnASlackRopeAtRestDoublesOnIt()
    {
        Assert.Equal(800, Peak(Flax(3), 400, 0, SailboatWithItsWater), 3);
    }

    [Fact]
    public void AHullComingTautWithNoOtherPullPullsBySpeedTimesTheRootOfStiffnessAndMass()
    {
        var line = Flax(3);
        Assert.Equal(1.0 * Math.Sqrt(line.NewtonsPerMetre * SailboatWithItsWater), Peak(line, 0, 1.0, SailboatWithItsWater), 3);
    }

    [Fact]
    public void AHullComingTautUnderASteadyPullPullsTheSteadyPullAndTheRootOfItsSquareAndHerEnergy()
    {
        var line = Flax(3);
        Assert.Equal(400 + Math.Sqrt(400 * 400 + line.NewtonsPerMetre * SailboatWithItsWater * 1.2 * 1.2), Peak(line, 400, 1.2, SailboatWithItsWater), 3);
    }

    [Fact]
    public void AnElasticLineThrowsBackAHullAsFastAsSheCame()
    {
        Assert.Equal(-2 * 1.0, Flax(3).Over(ComingTaut, 1.0, 0, SailboatWithItsWater, Long, 1).Kick, 6);
    }

    [Fact]
    public void WithinATickItPullsWhatTheStretchAndTheMotionGiveIt()
    {
        var line = Flax(3);
        var (kilograms, speed) = (SailboatWithItsWater, 0.5);
        var (stretch, velocity, steps) = (ComingTaut, speed, 1000);
        var kick = 0.0;
        for (var i = 0; i < steps; i++)
        {
            var accelerated = -line.NewtonsPerMetre * stretch / kilograms;
            velocity += accelerated * Tick / steps;
            kick += accelerated * Tick / steps;
            stretch += velocity * Tick / steps;
        }

        Assert.Equal(kick, line.Over(ComingTaut, speed, 0, kilograms, Tick, 1).Kick, 4);
    }

    [Fact]
    public void ALineAtASlopePullsAlongItAndHoldsHerAcrossTheWaterBySoMuchLess()
    {
        var line = Flax(3);
        const double level = 0.6;
        var step = line.Over(0.1, 0, 0, SailboatWithItsWater, 1e-6, level);

        Assert.Equal(line.NewtonsPerMetre * 0.1 * level, step.PeakNewtons, 3);
    }

    [Fact]
    public void ALongerRopeSnatchesSofter()
    {
        Assert.True(Peak(Flax(10), 0, 1.0, SailboatWithItsWater) < Peak(Flax(3), 0, 1.0, SailboatWithItsWater));
    }

    [Fact]
    public void AHeavierHullSnatchesHarder()
    {
        Assert.True(Peak(Flax(3), 0, 1.0, SailboatWithItsWater) > Peak(Flax(3), 0, 1.0, 120));
    }

    [Fact]
    public void AHullMovingTowardThePostPullsNothing()
    {
        var step = Flax(3).Over(ComingTaut, -1.0, -50, SailboatWithItsWater, Tick, 1);

        Assert.Equal(0, step.PeakNewtons, 3);
        Assert.Equal(0, step.Kick, 6);
    }

    [Fact]
    public void ASlackLinePullsNothing() => Assert.Equal(default, Flax(3).Over(-0.2, 1.0, 400, SailboatWithItsWater, Tick, 1));

    [Fact]
    public void ASailboatSurgedOutByAStormSeaPartsAShortRopeAndNotALongOne()
    {
        const double orbitalMetresPerSecond = 1.6;
        Assert.True(Flax(3).Parts(Peak(Flax(3), 0, orbitalMetresPerSecond, SailboatWithItsWater)));
        Assert.False(Flax(10).Parts(Peak(Flax(10), 0, orbitalMetresPerSecond, SailboatWithItsWater)));
    }

    [Fact]
    public void ItPartsOnlyPastItsBreakingLoad()
    {
        Assert.False(Flax(3).Parts(Fixtures.FlaxNewtons));
        Assert.True(Flax(3).Parts(Fixtures.FlaxNewtons + 1));
    }
}
