using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ImpactTests
{
    private const double PerTick = 1 / 60.0;
    private const long StepMs = 17;

    private readonly Impact impact = new(give: 1, guardMs: 500);
    private readonly Course course = new();
    private long now;
    private double x, z;

    [Fact]
    public void ReportsTheSpeedLostWhenTheHullStopsAgainstAWall()
    {
        var impacts = Sail((3, 0), (3, 0), (0, 0), (0, 0));

        Assert.Equal(3, Assert.Single(impacts), 6);
    }

    [Fact]
    public void CountsOnlyTheSpeedIntoTheWallWhenTheHullGlancesOffIt()
    {
        var impacts = Sail((3, 4), (3, 4), (0, 4), (0, 4));

        Assert.Equal(3, Assert.Single(impacts), 6);
    }

    [Fact]
    public void PointsTheKnockTowardTheObstacleAlongTheAxisItBlocked()
    {
        Sail((-3, 4), (-3, 4));
        var knocked = Step(0, 4 * PerTick, x, z += 4.0 * StepMs / 1000)!.Value;

        Assert.Equal(-3, knocked.TowardX, 6);
        Assert.Equal(0, knocked.TowardZ, 6);
    }

    [Fact]
    public void IgnoresAStopUnderTheHullsGive()
    {
        Assert.Empty(Sail((0.8, 0), (0, 0)));
    }

    [Fact]
    public void IgnoresAVesselEasingToAStop()
    {
        Assert.Empty(Sail(Enumerable.Range(0, 40).Select(step => (4 * Math.Pow(0.9, step), 0.0)).ToArray()));
    }

    [Fact]
    public void IgnoresPressingOnAgainstTheWall()
    {
        var impacts = Sail((3, 0), (0, 0), (3, 0), (0, 0), (0, 0), (3, 0), (0, 0));

        Assert.Single(impacts);
    }

    [Fact]
    public void ARiderPaddlingOnAgainstTheRockStrikesItOnce()
    {
        Assert.Single(Sail((3, 0), (3, 0), (0, 0)));

        for (int second = 0; second < 5; second++)
        {
            now += 1000;
            Assert.Empty(Pressed(3, 0));
        }
    }

    [Fact]
    public void AHullHeldOffAMomentAndPressedBackGainsNoSpeedToStrikeWith()
    {
        Sail((3, 0), (3, 0), (0, 0));
        now += 1000;

        Assert.Empty(Pressed(3, 0.003));
    }

    [Fact]
    public void TakesTheSpeedFromHowFarTheHullReallyMoved()
    {
        Step(6 * PerTick, 0, x, z);
        Step(6 * PerTick, 0, x += 2.0 * StepMs / 1000, z);
        var knocked = Step(0, 0, x, z)!.Value;

        Assert.Equal(2, knocked.SpeedLost, 6);
    }

    [Theory]
    [InlineData(6, 100)]
    [InlineData(12, 200)]
    public void ReadsTheSameSpeedWhateverHowManyStepsAFrameRuns(int steps, long frameMs)
    {
        Knocked? knocked = null;
        for (int frame = 0; frame < 4 && knocked == null; frame++)
        {
            now += frameMs;
            for (int step = 0; step < steps && knocked == null; step++)
            {
                bool blocked = frame == 3 && step == steps / 2;
                if (!blocked) x += 3.0 * frameMs / 1000 / steps;
                course.Record(x, z, now);
                knocked = impact.Step(blocked ? 0 : 3 * PerTick, 0, course.Velocity, now);
            }
        }

        Assert.Equal(3, knocked!.Value.SpeedLost, 6);
    }

    [Fact]
    public void StrikesAgainOnceTheGuardHasPassed()
    {
        Assert.Single(Sail((3, 0), (3, 0), (0, 0)));
        now += 1000;

        Assert.Single(Sail((3, 0), (3, 0), (0, 0)));
    }

    [Fact]
    public void ForgetsTheSpeedOfAVesselItStoppedMeasuring()
    {
        Sail((3, 0));
        impact.Forget();
        now += 1000;

        Assert.Empty(Sail((0, 0)));
    }

    [Fact]
    public void StrikesWithTheEnergyAboveTheHullsGive()
    {
        Assert.Equal(0.5 * 100 * (9 - 1), impact.Joules(kilograms: 100, speedLost: 3), 6);
    }

    private List<double> Sail(params (double x, double z)[] metresPerSecond)
    {
        var impacts = new List<double>();
        foreach (var (vx, vz) in metresPerSecond)
        {
            double seconds = StepMs / 1000.0;
            if (Step(vx * PerTick, vz * PerTick, x += vx * seconds, z += vz * seconds) is { } knocked) impacts.Add(knocked.SpeedLost);
        }
        return impacts;
    }

    private Knocked? Step(double motionX, double motionZ, double atX, double atZ)
    {
        course.Record(atX, atZ, now += StepMs);
        return impact.Step(motionX, motionZ, course.Velocity, now);
    }

    private List<double> Pressed(double asked, double wobble)
    {
        var impacts = new List<double>();
        for (int step = 0; step < 30; step++)
        {
            bool asking = step % 2 == 0;
            if (Step(asking ? asked * PerTick : 0, 0, x += asking ? wobble : 0, z) is { } knocked) impacts.Add(knocked.SpeedLost);
        }
        return impacts;
    }
}
