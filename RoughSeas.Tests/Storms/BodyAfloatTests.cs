using RoughSeas.Storms;
using Vintagestory.API.Config;

namespace RoughSeas.Tests.Storms;

public class BodyAfloatTests
{
    private const double Frame = 1 / 60.0;
    private const double Water = 1.2;

    [Fact]
    public void CarriesASwimmerToTheWatersVelocityAgainstVanillasDrag()
    {
        var motion = 0.0;
        for (var step = 0; step < 600; step++)
        {
            motion += BodyAfloat.Carried(Water, Frame);
            motion *= WaveCarry.Kept(GlobalConstants.WaterDrag, Frame);
        }

        Assert.Equal(Water / 60, motion, 9);
    }

    [Fact]
    public void StillWaterCarriesNothing()
    {
        Assert.Equal(0, BodyAfloat.Carried(0, Frame));
    }
}
