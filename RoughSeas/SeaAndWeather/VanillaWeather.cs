using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public class VanillaWeather
{
    private const string CalmEvent = "noevent";

    private static readonly (double X, double Z)[] RegionSamples = [(0.5, 0.5), (0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75)];

    private readonly SeaWeatherSystem sea;
    private readonly ICoreAPI api;
    private readonly HashSet<long> stormRegions = [];

    public VanillaWeather(SeaWeatherSystem sea, ICoreAPI api)
    {
        this.sea = sea;
        this.api = api;
        api.Event.OnGetWindSpeed -= VanillaWindHandler(api.ModLoader.GetModSystem<WeatherSystemBase>());
        api.Event.OnGetWindSpeed += OnGetWindSpeed;
        api.Event.OnGetClimate += OnGetClimate;
    }

    public void FollowOnServer(ICoreServerAPI sapi)
    {
        var weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        sapi.Event.RegisterGameTickListener(_ => RaiseStormEvents(weather), 2000);
    }

    /// <summary>Wind in vanilla's units at a height, scaled by height as WeatherSimulationRegion.GetWindSpeed does.</summary>
    public static double Strength(double mps, double y, int seaLevel, VanillaConfig vanilla)
    {
        var strength = mps * vanilla.UnitsPerMps;
        if (y <= seaLevel) return strength / (1 + (seaLevel - y) / 4);
        return Math.Min(strength * Math.Max(1, 0.9 + (y - seaLevel) / 100), Math.Max(strength, 1.5));
    }

    private static OnGetWindSpeedDelegate VanillaWindHandler(WeatherSystemBase weather) =>
        AccessTools.MethodDelegate<OnGetWindSpeedDelegate>(AccessTools.Method(typeof(WeatherSystemBase), "Event_OnGetWindSpeed"), weather);

    private void OnGetWindSpeed(Vec3d pos, ref Vec3d windSpeed)
    {
        var began = sea.Budget.Begin();
        var wind = sea.Wind(pos.X, pos.Z);
        var mps = Math.Sqrt(wind.X * wind.X + wind.Z * wind.Z);
        var scale = mps > 0 ? Strength(mps, pos.Y, api.World.SeaLevel, sea.Config.Vanilla) / mps : 0;
        windSpeed.Set(wind.X * scale, 0, wind.Z * scale);
        sea.Budget.End(began);
    }

    private void OnGetClimate(ref ClimateCondition climate, BlockPos pos, EnumGetClimateMode mode, double totalDays)
    {
        if (mode is EnumGetClimateMode.WorldGenValues or EnumGetClimateMode.ForSuppliedDate_TemperatureOnly) return;
        var began = sea.Budget.Begin();
        var hours = mode == EnumGetClimateMode.NowValues ? sea.NowHours : totalDays * api.World.Calendar.HoursPerDay;
        var wind = sea.Wind(pos.X, pos.Z, hours);
        if (wind.Presence > 0)
        {
            climate.Rainfall = Math.Max(climate.Rainfall, (float)wind.Rain);
            climate.RainCloudOverlay = Math.Max(climate.RainCloudOverlay, (float)wind.Presence);
        }
        sea.Budget.End(began);
    }

    private void RaiseStormEvents(WeatherSystemServer weather)
    {
        var began = sea.Budget.Begin();
        var regionSize = api.World.BlockAccessor.RegionSize;
        var threshold = sea.Config.Storm.WeatherEventThreshold;
        foreach (var (index, sim) in weather.weatherSimByMapRegion.ToArray())
        {
            var stormiest = RegionSamples
                .Select(sample => new BlockPos((int)((sim.regionX + sample.X) * regionSize), api.World.SeaLevel, (int)((sim.regionZ + sample.Z) * regionSize)))
                .Select(pos => (pos, wind: sea.Wind(pos.X, pos.Z)))
                .MaxBy(sample => sample.wind.Presence);
            var stormEvent = stormiest.wind.Presence >= threshold ? stormiest.wind.Storm?.Type.WeatherEvent : null;
            if (stormEvent != null && Allows(sim, stormEvent, stormiest.pos))
            {
                if (sim.CurWeatherEvent?.config.Code != stormEvent) sim.SetWeatherEvent(stormEvent, true);
                sim.CurWeatherEvent!.AllowStop = false;
                stormRegions.Add(index);
            }
            else if (stormRegions.Remove(index))
            {
                sim.CurWeatherEvent.AllowStop = true;
                sim.SetWeatherEvent(CalmEvent, true);
            }
        }
        sea.Budget.End(began);
    }

    private bool Allows(WeatherSimulationRegion sim, string stormEvent, BlockPos centre)
    {
        var climate = api.World.BlockAccessor.GetClimateAt(centre, EnumGetClimateMode.NowValues);
        var config = sim.WeatherEvents.FirstOrDefault(weatherEvent => weatherEvent.config.Code == stormEvent)?.config;
        return climate != null && config != null && config.getWeight(climate.Rainfall, climate.Temperature) > 0;
    }
}
