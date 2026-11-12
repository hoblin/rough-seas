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
    public void AHullStillShortOfTheLineAtTheStepsStartIsCaughtWhereSheReachesIt()
    {
        var line = Flax(3);
        var step = line.Over(-0.02, 1.0, 0, SailboatWithItsWater, Long, 1);

        Assert.Equal(1.0 * Math.Sqrt(line.NewtonsPerMetre * SailboatWithItsWater), step.PeakNewtons, 3);
        Assert.Equal(-2 * 1.0, step.Kick, 6);
    }

    [Theory]
    [InlineData(-0.1, 0.6, 0.5)]
    [InlineData(0.02, -0.6, 0.5)]
    [InlineData(0.05, 0, 0.3)]
    [InlineData(-0.1, 0.6, 1.0)]
    public void OneLongStepLeavesHerWhereAndAsFastAsManyShortOnes(double stretch, double speed, double seconds)
    {
        var line = new MooringLine(5.5, Fixtures.FlaxNewtons, Fixtures.FlaxStretchAtBreak);
        const double kilograms = 135;
        const double steady = 60;
        const int steps = 20000;
        var pull = steady / kilograms;
        var step = line.Over(stretch, speed, steady, kilograms, seconds, 1);
        var (x, v, owed) = (stretch, speed, 0.0);
        for (var i = 0; i < steps; i++)
        {
            v += owed;
            var fine = line.Over(x, v, steady, kilograms, seconds / steps, 1);
            v += pull * seconds / steps;
            x += (v + fine.Moved) * seconds / steps;
            (v, owed) = (v + fine.Moved, fine.Kick - fine.Moved);
        }

        Assert.Equal(x, stretch + (speed + pull * seconds + step.Moved) * seconds, 3);
        Assert.Equal(v + owed, speed + pull * seconds + step.Kick, 3);
    }

    [Fact]
    public void AStepTooLongToFollowEveryBounceLeavesHerAtRestWhereTheLineHoldsThePull()
    {
        const double kilograms = 150;
        const double steady = 50;
        const double seconds = 600;
        var line = Flax(1.5);
        var step = line.Over(ComingTaut, 1.0, steady, kilograms, seconds, 1);

        Assert.Equal(0, 1.0 + steady / kilograms * seconds + step.Kick, 9);
        Assert.Equal(steady / line.NewtonsPerMetre, ComingTaut + (1.0 + steady / kilograms * seconds + step.Moved) * seconds, 6);
    }

    [Fact]
    public void AnUnbreakableRopeStopsHerAtItsEndAndNeverParts()
    {
        var unbreakable = new MooringLine(3, double.PositiveInfinity, 0.1);
        var step = unbreakable.Over(0.01, 0.5, 400, SailboatWithItsWater, Tick, 1);

        Assert.Equal(-(0.5 + 400 * Tick / SailboatWithItsWater), step.Kick, 9);
        Assert.True(double.IsFinite(step.PeakNewtons));
        Assert.False(unbreakable.Parts(step.PeakNewtons));
    }

    [Theory]
    [InlineData(1 / 30.0)]
    [InlineData(0.25)]
    [InlineData(1.5)]
    public void MovedAsFarAsTheLineLetsHerEachStepSheBouncesNoHigherAtAnyStepLength(double seconds)
    {
        var line = Flax(1.5);
        const double kilograms = 150;
        const double arriving = 1.0;
        var (stretch, speed, owed, highest) = (1e-9, arriving, 0.0, 0.0);
        for (var t = 0.0; t < 60; t += seconds)
        {
            speed += owed;
            var step = line.Over(stretch, speed, 0, kilograms, seconds, 1);
            highest = Math.Max(highest, step.PeakNewtons);
            stretch += (speed + step.Moved) * seconds;
            (speed, owed) = (speed + step.Moved, step.Kick - step.Moved);
        }

        Assert.InRange(highest, 0, 1.0001 * arriving * Math.Sqrt(line.NewtonsPerMetre * kilograms));
    }

    [Fact]
    public void ADraggingAnchorHoldsHerBackByItsHoldingAndNeverDrawsHerBack()
    {
        var holding = MooringLine.Dragging(1.0, 0, 300, SailboatWithItsWater, Tick, 1);
        Assert.Equal(-300 * Tick / SailboatWithItsWater, holding.Kick, 12);
        Assert.Equal(300, holding.PeakNewtons);

        Assert.Equal(-0.001, MooringLine.Dragging(0.001, 0, 300, SailboatWithItsWater, Tick, 1).Kick, 12);
        Assert.Equal(0, MooringLine.Dragging(-0.5, 0, 300, SailboatWithItsWater, Tick, 1).Kick);
    }

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
