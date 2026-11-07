using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class VesselDraughtTests
{
    private static double AfterVanillasSwimDrag(double y, double along, double stepFactor) =>
        y / VesselDraught.SwimDrag(Math.Sqrt(along * along + y * y), stepFactor);

    [Theory]
    [InlineData(0.057, 0, 2)]
    [InlineData(0.01, 0, 1)]
    [InlineData(0.02, 0.09, 1)]
    [InlineData(-0.04, 0.11, 2)]
    [InlineData(0.3, 0.2, 2)]
    public void HerHeaveComesThroughVanillasSwimDragUnchanged(double heaving, double along, double stepFactor)
    {
        double before = VesselDraught.BeforeSwimDrag(heaving, along, stepFactor);

        Assert.Equal(heaving, AfterVanillasSwimDrag(before, along, stepFactor), 9);
    }
}
