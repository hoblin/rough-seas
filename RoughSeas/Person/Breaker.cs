using System;
using ProtoBuf;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Person;

/// <summary>
/// A crest breaking over a swimmer: the water it churns is full of air, which holds a body up less, and its roller runs
/// along the wave's travel. Both fade from the moment it breaks, the air to <see cref="BreakingConfig.AerationAfterPeriod"/>
/// within one wave period, the roller within <see cref="BreakingConfig.RollerSeconds"/>; they reach as deep as
/// <see cref="BreakingConfig.PlumeDepthPerHeight"/> wave heights.
/// </summary>
public readonly record struct Breaker(double BrokeSeconds, double HeightMetres, double PeriodSeconds, double Celerity, double TowardRadians)
{
    private const double Faded = 0.001;

    /// <summary>The share of air in the water, by volume, <paramref name="seconds"/> after it broke.</summary>
    public double AirShare(BreakingConfig config, double seconds) =>
        seconds < 0 ? 0 : config.Aeration * Math.Pow(config.AerationAfterPeriod / config.Aeration, seconds / PeriodSeconds);

    /// <summary>The roller's speed along <see cref="TowardRadians"/>, in metres a second, <paramref name="seconds"/> after it broke.</summary>
    public double RollerSpeed(BreakingConfig config, double seconds) =>
        seconds < 0 ? 0 : config.RollerSpeedRatio * Celerity * Math.Exp(-seconds / config.RollerSeconds);

    /// <summary>How deep the churned water reaches under the surface, in metres.</summary>
    public double PlumeDepth(BreakingConfig config) => config.PlumeDepthPerHeight * HeightMetres;

    public bool Spent(BreakingConfig config, double seconds) =>
        AirShare(config, seconds) < Faded && RollerSpeed(config, seconds) < Faded;
}

/// <summary>A crest that broke over a swimmer, as the server tells the client that moves them.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BreakerPacket
{
    public double HeightMetres;
    public double PeriodSeconds;
    public double Celerity;
    public double TowardRadians;
}

/// <summary>
/// The waves passing one body, by the vessels' rule: one roll for each dominant wave, on the first wave to pass once a
/// wave period has gone by; in water shallower than the breaking depth it breaks for certain, otherwise by the share of waves
/// that break. Its height is trough to crest (<see cref="CrestWatch.WatchWave"/>), the wave actually drawn.
/// </summary>
public sealed class CrestRoll(Waves waves, Storms.WavesConfig config)
{
    private readonly CrestWatch crests = new();
    private double sinceRoll;

    /// <summary>Takes the surface's next height over the body; the roll when a wave has just passed and took one.</summary>
    /// <param name="surface">The drawn surface's height above still water, in metres.</param>
    /// <param name="seconds">The time since the last call. A leap of the clock brings no rolls of its own: a roll waits for a wave to pass.</param>
    /// <param name="depth">The water's depth here, in metres.</param>
    /// <param name="draw">A uniform random number, drawn only for a roll.</param>
    public RolledWave? See(double surface, double seconds, SeaState state, double depth, Func<double> draw)
    {
        sinceRoll += seconds;
        var wave = crests.WatchWave(surface);
        if (state.WaveHeight < config.MinHeight)
        {
            sinceRoll = 0;
            return null;
        }
        if (wave is not { } height || sinceRoll < state.WavePeriod) return null;

        sinceRoll %= state.WavePeriod;
        var surf = waves.BreaksAt(depth, height);
        var chance = waves.BreakingChance(state.WindSpeed);
        var celerity = waves.BreakerCelerity(height, state.WavePeriod, depth < config.MaxDepthBlocks ? depth : double.PositiveInfinity);
        return new RolledWave(height, surf || draw() < chance, celerity, surf, chance);
    }

    public void Forget()
    {
        sinceRoll = 0;
        crests.Forget();
    }
}

public readonly record struct RolledWave(double HeightMetres, bool Broke, double Celerity, bool Surf, double Chance);
