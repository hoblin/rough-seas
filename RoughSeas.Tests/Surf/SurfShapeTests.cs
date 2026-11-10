using RoughSeas.Storms;
using RoughSeas.Surf;
using RoughSeas.Tests.Storms;

namespace RoughSeas.Tests.Surf;

public class SurfShapeTests
{
    private static readonly SurfShape Shape = new SeaSurface(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves)).Shape;

    [Fact]
    public void AWavePlungesOnASteepBeachAndSpillsOnAGentleOne()
    {
        Assert.True(Shape.Plunges(0.15, 4 * Math.Sqrt(0.5), 0.1));
        Assert.False(Shape.Plunges(0.01, 4 * Math.Sqrt(0.5), 0.1));
    }

    [Fact]
    public void NoWaveDoesNotPlunge() => Assert.False(Shape.Plunges(0.15, 0, 0.1));
}
