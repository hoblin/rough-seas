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

    private static double KeptOver(double seconds, double waterline, BodyMotion motion, (double X, double Z) current = default)
    {
        var start = motion;
        for (var t = 0.0; t < seconds; t += Frame)
            motion = Wader.Dragged(new WaterSurface(Still, 0, true), new BodyPlace(0, Still - waterline, 0, 1.7, 0.6), motion, default, Frame, current);
        return start.X != 0 ? motion.X / start.X : motion.Y / start.Y;
    }

    private static BodyMotion Across(double metresPerSecond) => new(metresPerSecond / WaterDrag.TicksPerSecond, 0, 0);

    [Fact]
    public void KeepsMostOfItsWayThroughAJumpWithTheWaterAtItsKnees() =>
        Assert.InRange(KeptOver(0.5, 0.5, Across(2)), 0.7, 1);

    [Fact]
    public void LosesMoreOfItsWayTheDeeperItWades() =>
        Assert.True(KeptOver(0.5, 1.2, Across(2)) < KeptOver(0.5, 0.5, Across(2)) - 0.1);

    [Fact]
    public void DragsNothingOutOfTheWater() =>
        Assert.Equal(1, KeptOver(0.5, 0, Across(2)), 9);

    [Fact]
    public void DragsAJumpOnlyByWhatOfTheBodyIsUnder() =>
        Assert.True(KeptOver(0.1, 0.5, new BodyMotion(0, 8.2 / WaterDrag.TicksPerSecond, 0)) > 0.8);

    [Fact]
    public void ACurrentCarriesAStandingBodyTowardItsSpeed()
    {
        var motion = Across(0);
        for (var t = 0.0; t < 20; t += Frame)
            motion = Wader.Dragged(new WaterSurface(Still, 0, true), new BodyPlace(0, Still - 1, 0, 1.7, 0.6), motion, default, Frame, (0.5, 0));

        Assert.InRange(motion.X * WaterDrag.TicksPerSecond, 0.45, 0.5);
    }
}
