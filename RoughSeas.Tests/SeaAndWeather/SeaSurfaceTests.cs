using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaSurfaceTests
{
    private readonly SeaSurface surface = new(Fixtures.Config().Swell);

    [Fact]
    public void ACalmSeaHasNoSwell()
    {
        Assert.Empty(surface.Waves(0, 5, 0));
        Assert.Empty(surface.Waves(2, 0, 0));
    }

    [Fact]
    public void TheLongestWavesRunWithTheWindAtTheirDeepWaterLength()
    {
        var waves = surface.Waves(4, 0, 12);
        var longest = waves.MinBy(wave => Math.Sqrt(wave.NumberX * wave.NumberX + wave.NumberZ * wave.NumberZ));
        var number = Math.Sqrt(longest.NumberX * longest.NumberX + longest.NumberZ * longest.NumberZ);
        var period = surface.Period(4);

        Assert.True(longest.NumberZ > 0, "runs toward the wind's south");
        Assert.Equal(9.81 * period * period / (2 * Math.PI), 2 * Math.PI / number, 3);
    }

    [Fact]
    public void ItsCrestsRiseHalfTheWaveHeightAtMost()
    {
        Assert.Equal(2.0, surface.Waves(4, 8, 3).Sum(wave => wave.Amplitude), 6);
    }

    [Fact]
    public void TheShaderDrawsTheHeightBoatsRideFarFromTheWorldOrigin()
    {
        var waves = surface.Waves(6, 15, -9);
        const double seconds = 98765.4, originX = 512031.25, originZ = 498877.5;
        var shaderWaves = new float[16];
        SeaSurface.ShaderWaves(waves, seconds, (float)originX, (float)originZ, shaderWaves);

        foreach (var (dx, dz) in new[] { (0f, 0f), (12.5f, -3f), (-40f, 25.25f) })
        {
            double drawn = 0;
            for (var i = 0; i < waves.Length; i++)
            {
                drawn += shaderWaves[i * 4 + 2] * MathF.Sin(shaderWaves[i * 4] * dx + shaderWaves[i * 4 + 1] * dz - shaderWaves[i * 4 + 3]);
            }
            Assert.Equal(SeaSurface.Height(waves, seconds, (float)originX + dx, (float)originZ + dz), drawn, 2);
        }
    }
}
