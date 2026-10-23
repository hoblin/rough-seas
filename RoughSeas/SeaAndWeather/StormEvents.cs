using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// A region a storm covers carries the storm type's own event while that event's conditions hold there; elsewhere
/// vanilla's roll decides.
/// </summary>
public class StormEvents(SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private readonly ConditionalWeakTable<WeatherSimulationRegion, object> pinned = [];

    public void Drive(WeatherSimulationRegion sim)
    {
        var model = sea.Model;
        var stormiest = SeaModel.SamplesOf(sim.regionX, sim.regionZ, sapi.WorldManager.RegionSize)
            .Select(at => (at, storms: model.StormsAt(at.X, at.Z, sea.Clock.Hours)))
            .MaxBy(sample => sample.storms.Presence);
        var stormEvent = stormiest.storms.Presence >= sea.Config.Storm.WeatherEventThreshold ? stormiest.storms.Storm?.Type.WeatherEvent : null;
        if (stormEvent != null && Allows(sim, stormEvent, stormiest.at))
        {
            if (sim.CurWeatherEvent?.config.Code != stormEvent)
            {
                sea.Log("region {0} {1}: {2} for a {3} at {4:F0}% presence", sim.regionX, sim.regionZ, stormEvent,
                    stormiest.storms.Storm!.Code, stormiest.storms.Presence * 100);
                sim.SetWeatherEvent(stormEvent, true);
            }
            foreach (var weatherEvent in sim.WeatherEvents) weatherEvent.AllowStop = weatherEvent != sim.CurWeatherEvent;
            pinned.AddOrUpdate(sim, sim);
        }
        else if (pinned.Remove(sim))
        {
            sea.Log("region {0} {1}: the storm has passed, its event left to vanilla's roll", sim.regionX, sim.regionZ);
            foreach (var weatherEvent in sim.WeatherEvents) weatherEvent.AllowStop = true;
        }
    }

    private bool Allows(WeatherSimulationRegion sim, string stormEvent, (double X, double Z) at)
    {
        var climate = sapi.World.BlockAccessor.GetClimateAt(new BlockPos((int)at.X, sapi.World.SeaLevel, (int)at.Z, Dimensions.NormalWorld), EnumGetClimateMode.NowValues);
        var config = sim.WeatherEvents.FirstOrDefault(weatherEvent => weatherEvent.config.Code == stormEvent)?.config;
        return climate != null && config != null && config.getWeight(climate.Rainfall, climate.Temperature) > 0;
    }
}
