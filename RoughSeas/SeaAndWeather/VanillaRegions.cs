using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public class VanillaRegions(SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private const int TickMilliseconds = 2000;

    private readonly ConditionalWeakTable<WeatherSimulationRegion, string> events = [];

    public void Drive()
    {
        var weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        var winds = new RegionWinds(sea);
        var stormEvents = new StormEvents(sea, sapi);
        sapi.Event.RegisterGameTickListener(dt =>
        {
            if (!weather.autoChangePatterns) return;
            using var measure = sea.Budget.Measure();
            foreach (var sim in weather.weatherSimByMapRegion.Values.ToArray())
            {
                winds.Drive(sim);
                stormEvents.Drive(sim);
                Watch(sim);
            }
        }, TickMilliseconds);
    }

    private void Watch(WeatherSimulationRegion sim)
    {
        var code = sim.CurWeatherEvent?.config.Code;
        if (code == null || events.TryGetValue(sim, out var last) && last == code) return;
        events.AddOrUpdate(sim, code);
        var regionSize = sapi.WorldManager.RegionSize;
        var centre = new BlockPos((sim.regionX * regionSize + regionSize / 2), sapi.World.SeaLevel, sim.regionZ * regionSize + regionSize / 2, Dimensions.NormalWorld);
        var climate = sapi.World.BlockAccessor.GetClimateAt(centre, EnumGetClimateMode.NowValues);
        sea.Log("region {0} {1}: weather event {2}, rain {3:F2} and {4:F1} °C at its centre", sim.regionX, sim.regionZ, code,
            climate?.Rainfall ?? float.NaN, climate?.Temperature ?? float.NaN);
    }
}
