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
    public const string CalmEvent = "noevent";

    private static readonly (double X, double Z)[] RegionSamples = [(0.5, 0.5), (0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75)];

    private readonly SeaWeatherSystem sea;
    private readonly ICoreAPI api;
    private readonly HashSet<long> stormRegions = [];

    public VanillaWeather(SeaWeatherSystem sea, ICoreAPI api)
    {
        this.sea = sea;
        this.api = api;
    }

    /// <summary>Takes the place of vanilla's wind handler; handlers added after it, like the Devastation's, still shape our wind.</summary>
    public void FollowWind()
    {
        api.Event.OnGetWindSpeed -= VanillaWindHandler(api.ModLoader.GetModSystem<WeatherSystemBase>());
        api.Event.OnGetWindSpeed += OnGetWindSpeed;
    }

    public void FollowStormEvents(ICoreServerAPI sapi)
    {
        var weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        sapi.Event.RegisterGameTickListener(_ => RaiseStormEvents(weather), 2000);
    }

    /// <summary>
    /// Wind in vanilla's units at a height, scaled by height as WeatherSimulationRegion.GetWindSpeed does,
    /// except that its 1.5 cap holds only for winds below it: storms blow harder.
    /// </summary>
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
        using var _ = sea.Budget.Measure();
        var wind = sea.Wind(pos.X, pos.Z);
        var scale = wind.Speed > 0 ? Strength(wind.Speed, pos.Y, api.World.SeaLevel, sea.Config.Vanilla) / wind.Speed : 0;
        windSpeed.Set(wind.X * scale, 0, wind.Z * scale);
    }

    private void RaiseStormEvents(WeatherSystemServer weather)
    {
        using var _ = sea.Budget.Measure();
        var regionSize = api.World.BlockAccessor.RegionSize;
        var threshold = sea.Config.Storm.WeatherEventThreshold;
        foreach (var (index, sim) in weather.weatherSimByMapRegion.ToArray())
        {
            var stormiest = RegionSamples
                .Select(sample => new BlockPos((int)((sim.regionX + sample.X) * regionSize), api.World.SeaLevel, (int)((sim.regionZ + sample.Z) * regionSize)))
                .Select(pos => (pos, storms: sea.StormsAt(pos.X, pos.Z, sea.NowHours)))
                .MaxBy(sample => sample.storms.Presence);
            var stormEvent = stormiest.storms.Presence >= threshold ? stormiest.storms.Storm?.Type.WeatherEvent : null;
            if (stormEvent != null && Allows(sim, stormEvent, stormiest.pos))
            {
                if (sim.CurWeatherEvent?.config.Code != stormEvent)
                {
                    sea.Log("region {0} {1}: {2} for a {3} at {4:F0}% presence", sim.regionX, sim.regionZ, stormEvent,
                        stormiest.storms.Storm!.Code, stormiest.storms.Presence * 100);
                    sim.SetWeatherEvent(stormEvent, true);
                }
                foreach (var weatherEvent in sim.WeatherEvents) weatherEvent.AllowStop = weatherEvent != sim.CurWeatherEvent;
                stormRegions.Add(index);
            }
            else if (stormRegions.Remove(index) || sim.CurWeatherEvent?.config.Code is { } current && current != CalmEvent)
            {
                sea.Log("region {0} {1}: calm, no storm of ours here", sim.regionX, sim.regionZ);
                foreach (var weatherEvent in sim.WeatherEvents) weatherEvent.AllowStop = true;
                sim.SetWeatherEvent(CalmEvent, true);
            }
        }
    }

    private bool Allows(WeatherSimulationRegion sim, string stormEvent, BlockPos centre)
    {
        var climate = api.World.BlockAccessor.GetClimateAt(centre, EnumGetClimateMode.NowValues);
        var config = sim.WeatherEvents.FirstOrDefault(weatherEvent => weatherEvent.config.Code == stormEvent)?.config;
        return climate != null && config != null && config.getWeight(climate.Rainfall, climate.Temperature) > 0;
    }
}
