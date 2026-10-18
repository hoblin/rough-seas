using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public class RegionWinds(SeaWeatherSystem sea)
{
    public const string PatternPrefix = "roughseas-force";

    private readonly ConditionalWeakTable<WeatherSimulationRegion, Lease> leases = [];
    private readonly BeaufortScale beaufort = new(sea.Config.Beaufort);

    private VanillaConfig Config => sea.Config.Vanilla;

    public void Drive(WeatherSimulationRegion sim)
    {
        if (!leases.TryGetValue(sim, out var lease))
        {
            leases.Add(sim, lease = new Lease());
            Take(sim, lease);
            return;
        }

        var current = sim.CurWindPattern;
        if (current == lease.Pattern || lease.Yielded != null && sea.NowHours > lease.YieldUntil)
        {
            Take(sim, lease);
        }
        else if (current != lease.Yielded)
        {
            lease.Yielded = current;
            lease.YieldUntil = current.State.ActiveUntilTotalHours;
            sea.Log("region {0} {1}: wind yields to {2}, set from outside, until hour {3:F1}", sim.regionX, sim.regionZ,
                current.config.Code, lease.YieldUntil);
        }
        else
        {
            lease.YieldUntil = Math.Max(lease.YieldUntil, current.State.ActiveUntilTotalHours);
        }
    }

    private void Take(WeatherSimulationRegion sim, Lease lease)
    {
        var mps = sea.Model.RegionMps(sim.regionX, sim.regionZ);
        var force = beaufort.ForceAt(mps);
        var pattern = sim.WindPatterns.FirstOrDefault(pattern => pattern.config.Code == PatternPrefix + force.Force);
        if (pattern == null) return;

        var strength = (float)(mps * Config.UnitsPerMps);
        var turned = pattern != sim.CurWindPattern;
        sim.CurWindPattern = pattern;
        pattern.Strength = pattern.State.BaseStrength = strength;
        pattern.State.ActiveUntilTotalHours = sea.NowHours + Config.WindLeaseHours;
        if (lease.Yielded != null) sea.Log("region {0} {1}: wind back to the map", sim.regionX, sim.regionZ);
        (lease.Pattern, lease.Yielded) = (pattern, null);

        if (turned) sea.Log("region {0} {1}: {2} at {3:F1} m/s", sim.regionX, sim.regionZ, pattern.config.Name, mps);
        if (!turned && Math.Abs(strength - lease.SentStrength) < Config.WindResendStrength) return;
        sim.sendWeatherUpdatePacket();
        lease.SentStrength = strength;
    }

    private sealed class Lease
    {
        public WindPattern? Pattern;
        public WindPattern? Yielded;
        public double YieldUntil;
        public float SentStrength;
    }
}
