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
