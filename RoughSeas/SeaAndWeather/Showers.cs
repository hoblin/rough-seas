using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

/// <summary>The weather's ordinary rain: showers that come oftener and heavier where the climate is wetter.</summary>
public class Showers
{
    private readonly ShowersConfig config;
    private readonly WorldClimate climate;
    private readonly NormalizedSimplexNoise noise;

    public Showers(long seed, ShowersConfig config, WorldClimate climate)
    {
        this.config = config;
        this.climate = climate;
        noise = NormalizedSimplexNoise.FromDefaultOctaves(3, 1, 0.5, seed + 60457);
    }

    public double CloudnessAt(double x, double z, double hours)
    {
        var rainfall = climate.RainfallAt(x, z);
        var threshold = config.DryThreshold + (config.WetThreshold - config.DryThreshold) * rainfall;
        var cloud = noise.Noise(x / config.SpatialScale, z / config.SpatialScale, hours / config.TimeScaleHours);
        return GameMath.Clamp((cloud - threshold) / config.Span, 0, 0.5 + config.WettestRain * rainfall);
    }
}
