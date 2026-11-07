using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Server;

namespace RoughSeas.Storms;

/// <summary>
/// The drawn sea of each region with a vessel nobody steers or a player in the water, on the server. A client's surface depends on the region
/// it is aimed from and not on the point in it, so everything in a region shares one <see cref="SwellAround"/>, aimed as
/// often as a client aims its own. A region nothing has asked about for a while is dropped.
/// </summary>
public sealed class RegionSwells(StormsModSystem storms, SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private const double ForgottenAfterSeconds = 30;

    private readonly Dictionary<(int X, int Z), Swell> swells = [];
    private double sweptAt;

    /// <summary>The sea drawn over a position now, with its waves run on to now.</summary>
    public IDrawnSea At(double x, double z) => Around(x, z).Sea;

    /// <summary>The sea drawn over a position now and the water it makes of the blocks, with its waves run on to now.</summary>
    public SwellAround Around(double x, double z)
    {
        var regionSize = sapi.WorldManager.RegionSize;
        var region = ((int)Math.Floor(x / regionSize), (int)Math.Floor(z / regionSize));
        var now = sapi.World.ElapsedMilliseconds / 1000.0;
        if (now - sweptAt > ForgottenAfterSeconds) Sweep(now);
        if (!swells.TryGetValue(region, out var swell))
        {
            swells[region] = swell = new Swell(new SwellAround(storms.Config.Surface, storms.Waves, sea.Config, sea.Clock));
        }
        swell.ReadAt = now;
        var seconds = sea.Clock.Seconds;
        if (now - swell.AimedAt >= storms.Config.Surface.AimSeconds)
        {
            swell.AimedAt = now;
            swell.Around.At(sea.Map, x, z, seconds, regionSize);
        }
        else if (seconds != swell.Seconds)
        {
            swell.Around.Advance(seconds);
        }
        swell.Seconds = seconds;
        return swell.Around;
    }

    private void Sweep(double now)
    {
        sweptAt = now;
        foreach (var region in swells.Where(swell => now - swell.Value.ReadAt > ForgottenAfterSeconds).Select(swell => swell.Key).ToList())
        {
            swells.Remove(region);
        }
    }

    private sealed class Swell(SwellAround around)
    {
        public SwellAround Around { get; } = around;

        public double AimedAt { get; set; } = double.NegativeInfinity;

        public double ReadAt { get; set; }

        public double Seconds { get; set; } = double.NaN;
    }
}
