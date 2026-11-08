using RoughSeas.Shipyard;

namespace RoughSeas.Tests.Shipyard;

public class SoakingTests
{
    private const double Through = 48;
    private static readonly Soaking Dry = new(0, false, Through);

    [Fact]
    public void WoodInWaterSoaksThroughInItsHours()
    {
        Assert.False(Dry.After(47, inWater: true).Soaked);
        Assert.True(Dry.After(48, inWater: true).Soaked);
    }

    [Fact]
    public void WaterStopsAtSoakedThrough()
    {
        Assert.Equal(Through, Dry.After(100, inWater: true).Hours);
    }

    [Fact]
    public void SoakedWoodStaysWetOutOfTheWaterUntilItHasDriedAsLongAsItSoaked()
    {
        var soaked = Dry.After(Through, inWater: true);

        Assert.True(soaked.After(47, inWater: false).Soaked);
        Assert.False(soaked.After(48, inWater: false).Soaked);
    }

    [Fact]
    public void WoodHalfSoakedDriesBackOutOfTheWater()
    {
        var half = Dry.After(24, inWater: true).After(10, inWater: false);

        Assert.Equal(14, half.Hours);
        Assert.False(half.Soaked);
    }

    [Fact]
    public void SoakedWoodBackInTheWaterIsSoakedThroughAgain()
    {
        var drying = Dry.After(Through, inWater: true).After(30, inWater: false).After(30, inWater: true);

        Assert.Equal(Through, drying.Hours);
        Assert.True(drying.Soaked);
    }

    [Fact]
    public void DryWoodOutOfTheWaterStaysDry()
    {
        Assert.Equal(Dry, Dry.After(10, inWater: false));
    }
}
