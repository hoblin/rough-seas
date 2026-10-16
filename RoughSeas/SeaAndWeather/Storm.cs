using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// A storm's whole life, fixed when it brews. Positions and radius in blocks, velocity in blocks per game hour,
/// times in total game hours. Equal storms are the same storm: compare them by value.
/// </summary>
public record Storm
{
    public required StormTypeConfig Type { get; init; }
    public required StormShapeConfig Shape { get; init; }
    public required double BornHours { get; init; }
    public required double LifetimeHours { get; init; }
    public required double OriginX { get; init; }
    public required double OriginZ { get; init; }
    public required double VelocityX { get; init; }
    public required double VelocityZ { get; init; }
    public required double Radius { get; init; }
    public required double PeakMps { get; init; }
    public required bool Northern { get; init; }

    public string Code => Type.Code;
    public double DiesHours => BornHours + LifetimeHours;

    public bool IsActive(double hours) => hours >= BornHours && hours <= DiesHours;

    public (double X, double Z) CentreAt(double hours) =>
        (OriginX + VelocityX * (hours - BornHours), OriginZ + VelocityZ * (hours - BornHours));

    public double Strength(double hours)
    {
        if (!IsActive(hours)) return 0;
        var age = (hours - BornHours) / LifetimeHours;
        var rising = GameMath.SmoothStep(Math.Min(1, age / Shape.RiseFraction));
        var falling = GameMath.SmoothStep(Math.Min(1, (1 - age) / Shape.FallFraction));
        return Math.Min(rising, falling);
    }

    public StormInfluence InfluenceAt(double x, double z, double hours)
    {
        var strength = Strength(hours);
        if (strength <= 0) return default;
        var (cx, cz) = CentreAt(hours);
        var dx = x - cx;
        var dz = z - cz;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        if (distance >= Radius) return default;

        var presence = strength * Bell(distance / Radius);
        if (!Type.Cyclonic)
        {
            var speed = Math.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);
            var gust = PeakMps * presence / Math.Max(speed, 1e-9);
            return new StormInfluence(presence, VelocityX * gust, VelocityZ * gust, Type.Rain * presence);
        }

        var vortex = PeakMps * strength * Vortex(distance) * Bell(distance / Radius);
        var (wx, wz) = CycloneDirection(dx, dz, distance);
        return new StormInfluence(presence, wx * vortex, wz * vortex, Type.Rain * presence);
    }

    private double Vortex(double distance)
    {
        var eye = Radius * Type.EyeFraction;
        return distance < eye ? distance / eye : Math.Sqrt(eye / distance);
    }

    private (double X, double Z) CycloneDirection(double dx, double dz, double distance)
    {
        if (distance < 1e-6) return (0, 0);
        var radialX = dx / distance;
        var radialZ = dz / distance;
        var spin = Northern ? 1 : -1;
        var tangentX = radialZ * spin;
        var tangentZ = -radialX * spin;
        var inflow = Shape.InflowDegrees * GameMath.DEG2RAD_DOUBLE;
        return (
            tangentX * Math.Cos(inflow) - radialX * Math.Sin(inflow),
            tangentZ * Math.Cos(inflow) - radialZ * Math.Sin(inflow)
        );
    }

    private static double Bell(double relativeDistance)
    {
        var falloff = 1 - relativeDistance * relativeDistance;
        return falloff * falloff;
    }
}

public readonly record struct StormInfluence(double Presence, double WindX, double WindZ, double Rain);
