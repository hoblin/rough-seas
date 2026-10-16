using System;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherModel
{
    private readonly SeaWeatherConfig config;
    private readonly PrevailingWinds prevailing;
    private readonly BeaufortScale beaufort;
    private readonly NormalizedSimplexNoise strengthNoise;
    private readonly NormalizedSimplexNoise veerNoise;

    public SeaWeatherModel(long seed, SeaWeatherConfig config, WorldClimate climate, Func<double, double> latitude, double hoursPerYear)
    {
        this.config = config;
        prevailing = new PrevailingWinds(config.PrevailingWinds, latitude);
        beaufort = new BeaufortScale(config.Beaufort);
        Storms = new StormField(seed, config, climate, prevailing, hoursPerYear);
        strengthNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 1, 0.5, seed + 41017);
        veerNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 1, 0.5, seed + 90121);
    }

    public StormField Storms { get; }

    public SeaState At(double x, double z, double hours, double oceanity)
    {
        var wind = Wind(x, z, hours);
        var seaWind = config.SeaBuildHours.Average(lag => Speed(Wind(x, z, hours - lag)));
        return new SeaState(
            wind.X,
            wind.Z,
            beaufort.ForceAt(Speed(wind)).Force,
            beaufort.WaveHeightAt(seaWind) * oceanity,
            oceanity,
            wind.Presence,
            wind.Rain,
            wind.Storm?.Code);
    }

    public WindSample Wind(double x, double z, double hours)
    {
        double stormX = 0, stormZ = 0, presence = 0, rain = 0, strongest = 0;
        Storm? dominant = null;
        foreach (var storm in Storms.Near(x, z, hours))
        {
            var influence = storm.InfluenceAt(x, z, hours);
            if (influence.Presence <= 0) continue;
            stormX += influence.WindX;
            stormZ += influence.WindZ;
            presence += influence.Presence;
            rain = Math.Max(rain, influence.Rain);
            if (influence.Presence > strongest)
            {
                strongest = influence.Presence;
                dominant = storm;
            }
        }

        var (backgroundX, backgroundZ) = Background(x, z, hours);
        var calm = 1 - Math.Min(1, presence);
        return new WindSample(backgroundX * calm + stormX, backgroundZ * calm + stormZ, Math.Min(1, presence), rain, dominant);
    }

    private (double X, double Z) Background(double x, double z, double hours)
    {
        var background = config.Background;
        var nx = x / background.SpatialScale;
        var nz = z / background.SpatialScale;
        var nt = hours / background.TimeScaleHours;
        var speed = Math.Max(0, background.MeanMps + background.SpreadMps * (2 * strengthNoise.Noise(nx, nz, nt) - 1));
        var veer = (2 * veerNoise.Noise(nx, nz, nt) - 1) * background.VeerDegrees * GameMath.DEG2RAD_DOUBLE;
        var (towardX, towardZ) = PrevailingWinds.Toward(prevailing.TowardRadians(z) + veer);
        return (towardX * speed, towardZ * speed);
    }

    private static double Speed(WindSample wind) => Math.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
}

public readonly record struct WindSample(double X, double Z, double Presence, double Rain, Storm? Storm);
