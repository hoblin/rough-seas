using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ImpactTests
{
    private const double PerTick = 1 / 60.0;
    private const long StepMs = 17;

    private readonly Impact impact = new(give: 1, guardMs: 500);
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
        var knocked = impact.Step(0, 4 * PerTick, x, z += 4.0 * StepMs / 1000, now += StepMs)!.Value;

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
        impact.Step(6 * PerTick, 0, x, z, now += StepMs);
        impact.Step(6 * PerTick, 0, x += 2.0 * StepMs / 1000, z, now += StepMs);
        var knocked = impact.Step(0, 0, x, z, now += StepMs)!.Value;

        Assert.Equal(2, knocked.SpeedLost, 6);
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
            if (impact.Step(vx * PerTick, vz * PerTick, x += vx * seconds, z += vz * seconds, now += StepMs) is { } knocked) impacts.Add(knocked.SpeedLost);
        }
        return impacts;
    }

    private List<double> Pressed(double asked, double wobble)
    {
        var impacts = new List<double>();
        for (int step = 0; step < 30; step++)
        {
            bool asking = step % 2 == 0;
            if (impact.Step(asking ? asked * PerTick : 0, 0, x += asking ? wobble : 0, z, now += StepMs) is { } knocked) impacts.Add(knocked.SpeedLost);
        }
        return impacts;
    }
}
