using RoughSeas.Person;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Person;

public class WadingBodyTests
{
    private const double Frame = 1 / 60.0;
    private const double Still = 100;

    private static readonly Waves Waves = new(Fixtures.Config().Waves);
    private static readonly WadingBody Wader = new(Fixtures.Person, Fixtures.PersonConfig, Waves);

    private static (double X, double Z) Over(double seconds, double waterline, (double X, double Z) metresPerSecond, (double X, double Z) water = default)
    {
        var motion = (metresPerSecond.X / WaterDrag.TicksPerSecond, metresPerSecond.Z / WaterDrag.TicksPerSecond);
        for (var t = 0.0; t < seconds; t += Frame)
            motion = Wader.Dragged(new WaterSurface(Still, 0, true), new BodyPlace(0, Still - waterline, 0, 1.7, 0.6), motion, default, Frame, water);
        return (motion.Item1 * WaterDrag.TicksPerSecond, motion.Item2 * WaterDrag.TicksPerSecond);
    }

    [Fact]
    public void KeepsMostOfItsWayThroughAJumpWithTheWaterAtItsKnees() =>
        Assert.InRange(Over(0.5, 0.5, (2, 0)).X / 2, 0.7, 1);

    [Fact]
    public void LosesMoreOfItsWayTheDeeperItWades() =>
        Assert.True(Over(0.5, 1.2, (2, 0)).X < Over(0.5, 0.5, (2, 0)).X - 0.2);

    [Fact]
    public void DragsNothingOutOfTheWater() =>
        Assert.Equal(2, Over(0.5, 0, (2, 0)).X, 9);

    [Fact]
    public void TheWaterCarriesAStandingBodyTowardItsSpeed() =>
        Assert.InRange(Over(20, 1, (0, 0), (0.5, 0)).X, 0.45, 0.5);
}
