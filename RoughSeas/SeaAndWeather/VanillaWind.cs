using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public class VanillaWind(SeaWeatherSystem sea, ICoreAPI api)
{
    private const double VanillaCap = 1.5;

    private VanillaConfig Config => sea.Config.Vanilla;

    public void Follow() => api.Event.OnGetWindSpeed += OnGetWindSpeed;

    public static double Strength(double mps, double y, int seaLevel, VanillaConfig vanilla)
    {
        var strength = mps * vanilla.UnitsPerMps;
        return AtHeight(strength, y, seaLevel, Math.Max(strength, VanillaCap));
    }

    public static double RegionStrength(double strength, double y, int seaLevel) => AtHeight(strength, y, seaLevel, VanillaCap);

    public static double Blend(double regionX, double regionZ, System.Func<int, int, double> region)
    {
        var left = (int)Math.Round(regionX) - 1;
        var top = (int)Math.Round(regionZ) - 1;
        return GameMath.BiLerp(
            region(left, top), region(left, top + 1), region(left + 1, top), region(left + 1, top + 1),
            GameMath.Smootherstep(regionZ - Math.Round(regionZ) + 0.5),
            GameMath.Smootherstep(regionX - Math.Round(regionX) + 0.5));
    }

    public static Vec3d Express(Vec3d vanilla, (double X, double Z) local, double localStrength, double regionsStrength)
    {
        var strength = Math.Max(0, Math.Sqrt(vanilla.X * vanilla.X + vanilla.Z * vanilla.Z) + localStrength - regionsStrength);
        var speed = Math.Sqrt(local.X * local.X + local.Z * local.Z);
        var (towardX, towardZ) = speed > 0 ? (local.X / speed, local.Z / speed) : (1, 0);
        return vanilla.Set(towardX * strength, vanilla.Y, towardZ * strength);
    }

    private static double AtHeight(double strength, double y, int seaLevel, double cap) =>
        y <= seaLevel
            ? strength / (1 + (seaLevel - y) / 4)
            : Math.Min(strength * Math.Max(1, 0.9 + (y - seaLevel) / 100), cap);

    private void OnGetWindSpeed(Vec3d pos, ref Vec3d windSpeed)
    {
        using var _ = sea.Budget.Measure();
        var source = sea.Source;
        var seaLevel = api.World.SeaLevel;
        var regionSize = api.World.BlockAccessor.RegionSize;
        var local = source.Wind(pos.X, pos.Z);
        var localMps = Math.Sqrt(local.X * local.X + local.Z * local.Z);
        var regions = Blend(pos.X / regionSize, pos.Z / regionSize,
            (x, z) => RegionStrength(source.RegionMps(x, z) * Config.UnitsPerMps, pos.Y, seaLevel));
        Express(windSpeed, local, Strength(localMps, pos.Y, seaLevel, Config), regions);
    }
}
