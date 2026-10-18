using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public class VanillaRegions(SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private const int TickMilliseconds = 2000;

    public void Drive()
    {
        var weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        var winds = new RegionWinds(sea);
        var events = new StormEvents(sea, sapi);
        sapi.Event.RegisterGameTickListener(dt =>
        {
            if (!weather.autoChangePatterns) return;
            using var measure = sea.Budget.Measure();
            foreach (var sim in weather.weatherSimByMapRegion.Values.ToArray())
            {
                winds.Drive(sim);
                events.Drive(sim);
            }
        }, TickMilliseconds);
    }
}
