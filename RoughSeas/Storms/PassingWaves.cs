using System;
using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
using RoughSeas.Water;

namespace RoughSeas.Storms;

/// <summary>
/// The waves passing one body afloat, by the one rule every body at sea takes them by: a wave every dominant period of
/// the world's clock, its height drawn from the sea's Rayleigh spread, breaking for certain where the bed breaks
/// it (<see cref="WaveReading.Breaks"/>) and otherwise by the wind's share of breaking waves.
/// </summary>
public sealed class PassingWaves(SeaClock clock, Waves waves, double minHeight)
{
    private readonly SeaClock.Watch watch = clock.StartWatch();
    private double since;

    /// <summary>Laps the clock on every tick, afloat or not, so a body back in the water starts counting afresh.</summary>
    /// <returns><paramref name="afloat"/>.</returns>
    public bool Lap(bool afloat)
    {
        since += watch.Lap().Seconds;
        if (!afloat) since = 0;
        return afloat;
    }

    public IEnumerable<PassingWave> Over(WaveReading here, double windMps, Random rand)
    {
        if (here.Height < minHeight || here.Period <= 0)
        {
            since = 0;
            yield break;
        }
        for (; since >= here.Period; since -= here.Period) yield return Roll(here, windMps, rand);
    }

    private PassingWave Roll(WaveReading here, double windMps, Random rand)
    {
        var height = WaveTheory.RayleighHeight(here.Height, rand.NextDouble());
        var surf = here.Breaks(height);
        var breaks = surf || rand.NextDouble() < waves.BreakingChance(windMps);
        return new PassingWave(height, surf, breaks, waves.BreakerCelerity(height, here.Period, here.Depth));
    }
}

public readonly record struct PassingWave(double Height, bool Surf, bool Breaks, double Celerity);
