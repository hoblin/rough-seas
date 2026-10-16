using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ImpactSensorTests
{
    private readonly ImpactSensor sensor = new(minImpactSpeed: 1);

    [Fact]
    public void ReportsTheSpeedLostInACrashOnce()
    {
        var impacts = Feed(Repeat(3, 0, 5).Concat(Repeat(0, 0, 5)));

        Assert.Equal(3, Assert.Single(impacts), 3);
    }

    [Fact]
    public void IgnoresASingleLostOrDoubledPacket()
    {
        Assert.Empty(Feed([(3, 0), (3, 0), (3, 0), (0, 0), (3, 0), (6, 0), (3, 0), (3, 0)]));
    }

    [Fact]
    public void IgnoresAVesselEasingToAStop()
    {
        Assert.Empty(Feed(Enumerable.Range(0, 20).Select(step => (4 - step * 0.2, 0.0))));
    }

    [Fact]
    public void CountsOnlyTheSpeedLostIntoTheObstacleOfAGlancingBlow()
    {
        var impacts = Feed(Repeat(3, 3, 5).Concat(Repeat(0, 3, 5)));

        Assert.Equal(3, Assert.Single(impacts), 3);
    }

    private List<double> Feed(IEnumerable<(double x, double z)> velocities) =>
        velocities.Select(velocity => sensor.Sample(velocity.x, velocity.z)).OfType<double>().ToList();

    private static IEnumerable<(double, double)> Repeat(double x, double z, int count) => Enumerable.Repeat((x, z), count);
}
