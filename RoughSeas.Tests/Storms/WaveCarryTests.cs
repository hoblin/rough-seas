using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class WaveCarryTests
{
    private const double Frame = 1 / 60.0;
    private const double WaterDragValue = 0.92;
    private const double Water = 1.2;

    private static double Kept(double seconds) => RoughSeas.SeaAndWeather.WaterDrag.Kept(WaterDragValue, seconds);

    [Fact]
    public void AHullUnderWayTakesTheWavesVelocityOnTopOfItsWay()
    {
        Assert.Equal(Water / 60, WaterDrag.Carried(Water, 0.4, 0), 12);
    }

    [Fact]
    public void AHullLyingStillMovesWithTheWavesAsTheyChangeWhateverTheDrag()
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
        const double period = 8;
        var (reach, furthest) = (0.0, 0.0);
        for (var waves = 0.0; waves < period / 2; waves += wavesPerTickSecond * Frame)
        {
            var velocity = WaveCarry.OnTheClock((Water * Math.Sin(2 * Math.PI * waves / period), 0), wavesPerTickSecond);
            reach += velocity.X * Frame;
            furthest = Math.Max(furthest, reach);
        }

        Assert.InRange(furthest, 0.97 * Water * period / Math.PI, 1.03 * Water * period / Math.PI);
    }

    [Fact]
    public void StandsWhileTheClockStands() => Assert.Equal(default, WaveCarry.OnTheClock((Water, Water), 0));

    [Fact]
    public void StillWaterCarriesNothing()
    {
        Assert.Equal(0, WaterDrag.Carried(0, 0, 0));
        Assert.Equal(0, WaterDrag.Carried(0, 0, Kept(Frame)));
    }
}
