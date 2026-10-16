using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaSurfaceTests
{
    private const double FrameSeconds = 1.0 / 60;
    private const double FarX = 512031.25, FarZ = 498877.5;

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
    public void TheWaterRisesSmoothlyFrameByFrameWhileTheSeaChangesFarFromTheWorldOrigin()
    {
        var swell = Settled(height: 6, seconds: 1000);
        var steepness = surface.Steepness;
        var previous = swell.Height(FarX, FarZ, steepness);
        for (var frame = 1; frame <= 600; frame++)
        {
            if (frame % 30 == 0) swell.Aim(6 + frame / 600.0, 15 + frame / 100.0, -9);
            swell.Advance(1000 + frame * FrameSeconds);
            var height = swell.Height(FarX, FarZ, steepness);
            Assert.True(Math.Abs(height - previous) < 0.1, $"frame {frame} jumped from {previous:F3} to {height:F3} m");
            previous = height;
        }
    }

    [Fact]
    public void WavesAlreadyRunningKeepTheirCourseAndPaceWhenTheWindVeers()
    {
        var swell = Settled(height: 6, seconds: 1000);
        var far = (x: FarX + 250, z: FarZ - 180);
        swell.Advance(1001);
        var before = swell.Height(far.x, far.z, surface.Steepness);

        swell.Aim(6.2, 15.5, -8.5);
        swell.Advance(1001 + FrameSeconds);

        Assert.True(Math.Abs(swell.Height(far.x, far.z, surface.Steepness) - before) < 0.1, "a slight veer moves no distant wave");
    }

    [Fact]
    public void NewWavesRiseWhenTheWindTurnsWhileTheOldOnesFade()
    {
        var swell = Settled(height: 6, seconds: 1000);
        var shaderWaves = new float[swell.Slots * 4];

        swell.Aim(6, -9, -15);
        swell.Advance(1000 + 10 * 60);
        swell.ShaderWaves(FarX, FarZ, shaderWaves);
        var amplitudes = Enumerable.Range(0, swell.Slots).Select(slot => shaderWaves[slot * 4 + 2]).ToArray();

        Assert.True(amplitudes.Take(swell.Slots / 2).All(amplitude => amplitude < 0.01) || amplitudes.Skip(swell.Slots / 2).All(amplitude => amplitude < 0.01),
            "after a minute one set has faded");
        Assert.Equal(3.0, amplitudes.Sum(), 1);
    }

    [Fact]
    public void MovingTheAnchorLeavesTheWaterWhereItWas()
    {
        var swell = Settled(height: 6, seconds: 1000);
        var before = swell.Height(FarX + 40, FarZ - 25, surface.Steepness);

        swell.Anchor(FarX + 300, FarZ + 200);

        Assert.Equal(before, swell.Height(FarX + 40, FarZ - 25, surface.Steepness), 6);
    }

    [Fact]
    public void TheShaderDrawsTheSurfaceBoatsRide()
    {
        var swell = Settled(height: 6, seconds: 98765.4);
        var steepness = surface.Steepness;
        const float originX = 512030f, originZ = 498880f;
        var shaderWaves = new float[swell.Slots * 4];
        swell.ShaderWaves(originX, originZ, shaderWaves);

        foreach (var (restX, restZ) in new[] { (0f, 0f), (12.5f, -3f), (-40f, 25.25f) })
        {
            var (drawnX, drawnY, drawnZ) = Drawn(shaderWaves, (float)steepness, restX, restZ);
            Assert.Equal(swell.Height(originX + drawnX, originZ + drawnZ, steepness), drawnY, 2);
        }
    }

    private SwellField Settled(double height, double seconds)
    {
        var swell = surface.Field();
        swell.Aim(height, 15, -9);
        swell.Advance(seconds);
        swell.Anchor(FarX, FarZ);
        return swell;
    }

    private static (float X, float Y, float Z) Drawn(float[] waves, float steepness, float restX, float restZ)
    {
        float x = restX, y = 0, z = restZ;
        for (var i = 0; i < waves.Length / 4; i++)
        {
            var (numberX, numberZ, amplitude, phase) = (waves[i * 4], waves[i * 4 + 1], waves[i * 4 + 2], waves[i * 4 + 3]);
            var number = MathF.Sqrt(numberX * numberX + numberZ * numberZ);
            if (number == 0) continue;

            var theta = numberX * restX + numberZ * restZ - phase;
            y += amplitude * MathF.Sin(theta);
            x += steepness * amplitude * MathF.Cos(theta) * numberX / number;
            z += steepness * amplitude * MathF.Cos(theta) * numberZ / number;
        }
        return (x, y, z);
    }
}
