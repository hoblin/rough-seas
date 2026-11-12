using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class WaveRideTests
{
    private const double Frame = 1 / 60.0;
    private const double WaterDragValue = 0.92;
    private const double Water = 1.2;
    private const double Period = 8;

    private static double Kept(double seconds) => RoughSeas.SeaAndWeather.WaterDrag.Kept(WaterDragValue, seconds);

    [Fact]
    public void ABodyUnderWayTakesTheWatersVelocityOnTopOfItsWay()
    {
        Assert.Equal(Water / 60, WaterDrag.Carried(Water, 0.4, 0), 12);
    }

    [Fact]
    public void ABodyLyingStillMovesWithTheWaterAsItChangesWhateverTheDrag()
    {
        var motion = 0.0;
        var before = 0.0;
        for (var step = 0; step < 600; step++)
        {
            var waves = Water * Math.Sin(step * Frame);
            motion *= Kept(Frame);
            motion += WaterDrag.Carried(waves, before, Kept(Frame));
            before = waves;

            Assert.Equal(waves / 60, motion, 12);
        }
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(17)]
    public void OneWaveCarriesAHullAsFarAtAnySpeedOfTime(double wavesPerTickSecond)
    {
        var (reach, furthest) = (0.0, 0.0);
        for (var waves = 0.0; waves < Period / 2; waves += wavesPerTickSecond * Frame)
        {
            var velocity = WaveRide.OnTheClock((Water * Math.Sin(2 * Math.PI * waves / Period), 0), wavesPerTickSecond);
            reach += velocity.X * Frame;
            furthest = Math.Max(furthest, reach);
        }

        Assert.InRange(furthest, 0.97 * Water * Period / Math.PI, 1.03 * Water * Period / Math.PI);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(17)]
    public void OneWavesPushGivesAHullTheWatersSpeedAtAnySpeedOfTime(double wavesPerTickSecond)
    {
        var turning = 2 * Math.PI / Period;
        var speed = 0.0;
        for (var waves = 0.0; waves < Period / 4; waves += wavesPerTickSecond * Frame)
        {
            speed += WaveRide.OnTheClock((Water * turning * Math.Cos(turning * (waves + wavesPerTickSecond * Frame / 2)), 0), wavesPerTickSecond * wavesPerTickSecond).X * Frame;
        }

        Assert.InRange(speed, 0.97 * Water * wavesPerTickSecond, 1.03 * Water * wavesPerTickSecond);
    }

    [Fact]
    public void StandsWhileTheClockStands() => Assert.Equal(default, WaveRide.OnTheClock((Water, Water), 0));

    [Fact]
    public void StillWaterCarriesNothing()
    {
        Assert.Equal(0, WaterDrag.Carried(0, 0, 0));
        Assert.Equal(0, WaterDrag.Carried(0, 0, Kept(Frame)));
    }
}
