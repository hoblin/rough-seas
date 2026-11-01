using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class WaveCarryTests
{
    private const double Frame = 1 / 60.0;
    private const double WaterDrag = 0.92;
    private const double Water = 1.2;

    private static double Kept(double seconds) => WaveCarry.Kept(WaterDrag, seconds);

    [Fact]
    public void AHullUnderWayTakesTheWavesVelocityOnTopOfItsWay()
    {
        Assert.Equal(Water / 60, WaveCarry.Carried(Water, 0.4, kept: null), 12);
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
            motion += WaveCarry.Carried(waves, before, Kept(Frame));
            before = waves;

            Assert.Equal(waves / 60, motion, 12);
        }
    }

    [Fact]
    public void StillWaterCarriesNothing()
    {
        Assert.Equal(0, WaveCarry.Carried(0, 0, kept: null));
        Assert.Equal(0, WaveCarry.Carried(0, 0, Kept(Frame)));
    }
}
