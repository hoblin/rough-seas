using RoughSeas.SeaAndWeather;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.SeaAndWeather;

public class VanillaWindTests
{
    private static readonly VanillaConfig Vanilla = Fixtures.Config().Vanilla;

    [Fact]
    public void GivesVanillaTheStrengthOfItsOwnPatterns()
    {
        Assert.Equal(1.0, VanillaWind.Strength(25, 110, 110, Vanilla), 2);
        Assert.Equal(0.3, VanillaWind.Strength(7.5, 110, 110, Vanilla), 2);
    }

    [Fact]
    public void KeepsWhatVanillaSaysWhereTheMapAddsNoDetail()
    {
        var wind = VanillaWind.Express(new Vec3d(0.4, 0, 0), (0, -10), localStrength: 0.4, regionsStrength: 0.4);

        Assert.Equal(0.4, wind.Length(), 6);
    }

    [Fact]
    public void BlowsTheWayTheMapBlows()
    {
        var wind = VanillaWind.Express(new Vec3d(0.4, 0, 0), (0, -10), localStrength: 0.4, regionsStrength: 0.4);

        Assert.Equal(0, wind.X, 6);
        Assert.True(wind.Z < 0, "towards north");
    }

    [Fact]
    public void AddsASquallFinerThanARegion()
    {
        var wind = VanillaWind.Express(new Vec3d(0.3, 0, 0), (20, 0), localStrength: 0.8, regionsStrength: 0.3);

        Assert.Equal(0.8, wind.Length(), 6);
    }

    [Fact]
    public void KeepsAnotherModsPatternUnderTheMapsDetail()
    {
        var wind = VanillaWind.Express(new Vec3d(1.5, 0, 0), (7, 0), localStrength: 0.3, regionsStrength: 0.3);

        Assert.Equal(1.5, wind.Length(), 6);
    }

    [Fact]
    public void BlendsRegionsAsVanillasReaderDoes()
    {
        static double Region(int x, int z) => x * 10 + z;

        Assert.Equal(Region(3, 7), VanillaWind.Blend(3.5, 7.5, Region), 6);
        Assert.InRange(VanillaWind.Blend(4.0, 7.5, Region), Region(3, 7), Region(4, 7));
    }
}
