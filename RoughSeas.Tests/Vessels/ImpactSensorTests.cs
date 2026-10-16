using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ImpactSensorTests
{
    private const long StepMs = 100;

    private readonly ImpactSensor sensor = new(minImpactSpeed: 1);

    [Fact]
    public void ReportsTheSpeedLostInACrashOnce()
    {
        var impacts = Sail(Repeat(3, 0, 5).Concat(Repeat(0, 0, 5)));

        Assert.Equal(3, Assert.Single(impacts), 3);
    }

    [Fact]
    public void IgnoresASingleLostOrDoubledPacket()
    {
        Assert.Empty(Sail([(3, 0), (3, 0), (3, 0), (0, 0), (3, 0), (6, 0), (3, 0), (3, 0)]));
    }

    [Fact]
    public void IgnoresAVesselEasingToAStop()
    {
        Assert.Empty(Sail(Enumerable.Range(0, 20).Select(step => (4 - step * 0.2, 0.0))));
    }

    [Fact]
    public void CountsOnlyTheSpeedLostIntoTheObstacleOfAGlancingBlow()
    {
        var impacts = Sail(Repeat(3, 3, 5).Concat(Repeat(0, 3, 5)));

        Assert.Equal(3, Assert.Single(impacts), 3);
    }

    [Fact]
    public void IgnoresARidersPacketsArrivingInBursts()
    {
        Assert.Empty(Bursts(seconds: 10, moving: _ => true));
    }

    [Fact]
    public void ReportsACrashWhileTheRidersPacketsArriveInBursts()
    {
        var impacts = Bursts(seconds: 10, moving: second => second < 5);

        Assert.InRange(Assert.Single(impacts), 1.5, 2);
    }

    private List<double> Sail(IEnumerable<(double x, double z)> velocities)
    {
        double x = 0, z = 0;
        long ms = 0;
        var impacts = new List<double>();
        foreach (var velocity in velocities)
        {
            (x, z, ms) = (x + velocity.x * StepMs / 1000, z + velocity.z * StepMs / 1000, ms + StepMs);
            if (sensor.Sample(x, z, ms) is double impact) impacts.Add(impact);
        }

        return impacts;
    }

    private List<double> Bursts(int seconds, Func<int, bool> moving)
    {
        const int PacketsPerBurst = 8;
        const double MetresPerSecond = 2;
        double x = 0;
        var impacts = new List<double>();
        for (int second = 1; second <= seconds; second++)
        {
            for (int packet = 0; packet < PacketsPerBurst; packet++)
            {
                if (moving(second)) x += MetresPerSecond / PacketsPerBurst;
                if (sensor.Sample(x, 0, second * 1000 + packet) is double impact) impacts.Add(impact);
            }
        }

        return impacts;
    }

    private static IEnumerable<(double, double)> Repeat(double x, double z, int count) => Enumerable.Repeat((x, z), count);
}
