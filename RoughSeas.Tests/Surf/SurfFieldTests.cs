using RoughSeas.Surf;

namespace RoughSeas.Tests.Surf;

public class SurfFieldTests
{
    private const int Size = 8;
    private const float GroundHeight = 112;

    private static SurfField ShoreAlongX(float breaking)
    {
        var field = new SurfField(0, 0, Size, []);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            var water = x < Size / 2;
            field.Set(field.Bed, x, z, 0, water ? 2 : -0.5f);
            field.Set(field.Aux, x, z, 3, water ? breaking : GroundHeight);
        }
        return field;
    }

    [Fact]
    public void AtTheShorelineBreakingStaysAShare()
    {
        var field = ShoreAlongX(0.5f);
        Assert.InRange(field.BreakingAt(Size / 2.0, Size / 2.0), 0, 0.5);
    }

    [Fact]
    public void OverOpenWaterBreakingIsTheCellsOwn()
    {
        var field = ShoreAlongX(0.5f);
        Assert.Equal(0.5, field.BreakingAt(1.5, Size / 2.0), 6);
    }

    [Fact]
    public void OverLandNothingBreaks()
    {
        var field = ShoreAlongX(0.5f);
        Assert.Equal(0, field.BreakingAt(Size - 1.5, Size / 2.0));
    }
}
