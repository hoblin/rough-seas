using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherModel
{
    private readonly SeaWeatherConfig config;
    private readonly PrevailingWinds prevailing;
    private readonly BeaufortScale beaufort;
    private readonly WorldClimate climate;
    private readonly OceanBasins basins;
    private readonly OceanCurrents currents;
    private readonly NormalizedSimplexNoise strengthNoise;
    private readonly NormalizedSimplexNoise veerNoise;

    public SeaWeatherModel(long seed, SeaWeatherConfig config, WorldClimate climate, OceanBasins basins, Func<double, double> latitude, double hoursPerYear)
    {
        this.config = config;
        this.climate = climate;
        this.basins = basins;
        prevailing = new PrevailingWinds(config.PrevailingWinds, latitude);
        beaufort = new BeaufortScale(config.Beaufort);
        currents = new OceanCurrents(config.Currents);
        Storms = new StormField(seed, config, climate, prevailing, hoursPerYear);
        strengthNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 1, 0.5, seed + 41017);
        veerNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 1, 0.5, seed + 90121);
    }

    public StormField Storms { get; }

    public SeaState At(double x, double z, double hours, double oceanity)
    {
        var wind = Wind(x, z, hours);
        var seaWind = 0.0;
        foreach (var lag in config.SeaBuildHours)
        {
            seaWind += lag == 0 ? wind.Speed : Wind(x, z, hours - lag).Speed;
        }
        seaWind /= config.SeaBuildHours.Length;
        return new SeaState(
            wind.X,
            wind.Z,
            beaufort.ForceAt(wind.Speed).Force,
            beaufort.WaveHeightAt(seaWind) * oceanity,
            oceanity,
            wind.Presence,
            wind.Rain,
            wind.Storm?.Code);
    }

    public SeaCurrent Current(double x, double z) =>
        currents.At(prevailing.LatitudeDegrees(z), prevailing.PolewardZ(z), basins.At(x, z), climate.At(x, z).Celsius);

    public WindSample Wind(double x, double z, double hours)
    {
        var storms = StormsAt(x, z, hours);
        var (backgroundX, backgroundZ) = Background(x, z, hours);
        var calm = 1 - storms.Presence;
        return storms with { X = backgroundX * calm + storms.X, Z = backgroundZ * calm + storms.Z };
    }

    public WindSample StormsAt(double x, double z, double hours)
    {
        double windX = 0, windZ = 0, presence = 0, rain = 0, strongest = 0;
        Storm? dominant = null;
        foreach (var storm in Storms.Near(x, z, hours))
        {
            var influence = storm.InfluenceAt(x, z, hours);
            if (influence.Presence <= 0) continue;
            windX += influence.WindX;
            windZ += influence.WindZ;
            presence += influence.Presence;
            rain = Math.Max(rain, influence.Rain);
            if (influence.Presence > strongest)
            {
                strongest = influence.Presence;
                dominant = storm;
            }
        }
        return new WindSample(windX, windZ, Math.Min(1, presence), rain, dominant);
    }

    private (double X, double Z) Background(double x, double z, double hours)
    {
        var background = config.Background;
        var nx = x / background.SpatialScale;
        var nz = z / background.SpatialScale;
        var nt = hours / background.TimeScaleHours;
        var speed = Math.Max(0, background.MeanMps + background.SpreadMps * (2 * strengthNoise.Noise(nx, nz, nt) - 1));
        var veer = (2 * veerNoise.Noise(nx, nz, nt) - 1) * background.VeerDegrees * GameMath.DEG2RAD_DOUBLE;
        var (towardX, towardZ) = Compass.Toward(prevailing.TowardRadians(z) + veer);
        return (towardX * speed, towardZ * speed);
    }
}

/// <summary>Wind in m/s towards east (X) and south (Z), with the storms' combined presence, their rain and the strongest storm.</summary>
public readonly record struct WindSample(double X, double Z, double Presence, double Rain, Storm? Storm)
{
    public double Speed => Math.Sqrt(X * X + Z * Z);
}
