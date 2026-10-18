using Newtonsoft.Json;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
    public void CalmsACellBelowItsWindyRegionsWithoutBlowingBackwards()
    {
        var wind = VanillaWind.Express(new Vec3d(0.3, 0, 0), (1, 0), localStrength: 0.04, regionsStrength: 0.6);

        Assert.Equal(0, wind.Length(), 6);
    }

    [Fact]
    public void BlowsAlongXWhereTheMapHasNoWind()
    {
        var wind = VanillaWind.Express(new Vec3d(0.4, 0, 0), (0, 0), localStrength: 0, regionsStrength: 0);

        Assert.Equal((0.4, 0.0), (wind.X, wind.Z));
    }

    [Fact]
    public void LetsAStormAloftBlowPastTheCapVanillaPutsOnItsRegions()
    {
        const int seaLevel = 110;

        Assert.True(VanillaWind.Strength(55, seaLevel + 50, seaLevel, Vanilla) > 1.5);
        Assert.Equal(1.5, VanillaWind.RegionStrength(55 * Vanilla.UnitsPerMps, seaLevel + 50, seaLevel), 6);
    }

    [Fact]
    public void BlendsRegionsAsVanillasReaderDoes()
    {
        static double Region(int x, int z) => x * 10 + z;

        Assert.Equal(Region(3, 7), VanillaWind.Blend(3.5, 7.5, Region), 6);
        Assert.Equal((Region(3, 7) + Region(3, 8)) / 2.0, VanillaWind.Blend(3.5, 8.0, Region), 6);
    }

    [Fact]
    public void GivesEveryBeaufortForceAPatternOfItsOwnStrength()
    {
        var forces = Fixtures.Config().Beaufort.OrderBy(force => force.MinMps).ToArray();
        var patterns = JsonConvert.DeserializeObject<WindPatternConfig[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "windpatterns", "beaufort.json")))!;

        Assert.All(forces.Select((force, i) => (force, ceiling: i + 1 < forces.Length ? forces[i + 1].MinMps : double.MaxValue)), band =>
        {
            var pattern = patterns.Single(pattern => pattern.Code == RegionWinds.PatternPrefix + band.force.Force);
            var mps = pattern.Strength.avg / Vanilla.UnitsPerMps;
            Assert.InRange(mps, band.force.MinMps, band.ceiling);
            Assert.Equal(band.force.Name, pattern.Name);
        });
    }
}
