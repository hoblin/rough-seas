using RoughSeas.Surf;

namespace RoughSeas.Tests.Surf;

public class SurfShapeTests
{
    private static readonly SurfShape Shape = new(1.28, 0.5, 3.3);
    private static readonly (double X, double Z)[] Numbers = [(-0.1, 0)];

    private static (Shaped Shaped, double Height) Shaped(double depth, double amplitude, double slope = 0.05)
    {
        Span<double> heights = stackalloc double[1], numbers = stackalloc double[1];
        var shaped = Shape.Shape(depth, slope, 0, Numbers, [amplitude], heights, numbers, 0);
        return (shaped, heights[0]);
    }

    [Fact]
    public void InShallowWaterTheWaveIsNoHigherThanTheDepthLetsIt()
    {
        var (shaped, height) = Shaped(depth: 1, amplitude: 1.5);
        Assert.Equal(1 / 1.28, 4 * Math.Sqrt(height * height / 2), 6);
        Assert.True(shaped.Breaking > 0);
    }

    [Fact]
    public void AWaveTheDepthCanCarryDoesNotBreak() => Assert.Equal(0, Shaped(depth: 10, amplitude: 0.2).Shaped.Breaking);

    [Fact]
    public void AWavePlungesOnASteepBeachAndSpillsOnAGentleOne()
    {
        Assert.True(Shaped(depth: 1, amplitude: 1, slope: 0.15).Shaped.Plunging);
        Assert.False(Shaped(depth: 1, amplitude: 1, slope: 0.01).Shaped.Plunging);
    }

    [Fact]
    public void OverLandNoWaveIsShaped() => Assert.Equal(default, Shaped(depth: 0, amplitude: 1).Shaped);
}
