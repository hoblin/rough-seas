using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ImpactTests
{
    private const double PerTick = 1 / 60.0;
    private const long StepMs = 17;

    private readonly Impact impact = new(give: 1, guardMs: 500);
    private long now;

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
    public void StrikesAgainOnceTheGuardHasPassed()
    {
        Assert.Single(Sail((3, 0), (0, 0)));
        now += 1000;

        Assert.Single(Sail((3, 0), (0, 0)));
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
        foreach (var (x, z) in metresPerSecond)
        {
            if (impact.Step(x * PerTick, z * PerTick, now += StepMs) is double lost) impacts.Add(lost);
        }
        return impacts;
    }
}
