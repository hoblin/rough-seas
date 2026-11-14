using System;

namespace RoughSeas.Wind;

public abstract class Planform
{
    public abstract double Potential { get; }

    public abstract double Vortex { get; }
}

public sealed class Delta(double apexRadians) : Planform
{
    private double Aspect => 4 * Math.Tan(apexRadians / 2);

    public override double Potential => 2 * Math.PI * Aspect / (2 + Math.Sqrt(Aspect * Aspect + 4));

    public override double Vortex => (Potential - Potential * Potential / (Math.PI * Aspect)) / Math.Sin(apexRadians / 2);
}

public sealed class Rectangle(double aspect) : Planform
{
    public override double Potential => 2 * Math.PI * aspect / (2 + Math.Sqrt(4.0 / 3 * aspect * aspect + 4));

    public override double Vortex => Math.PI * aspect / (2 + Math.Sqrt(aspect * aspect / 4 + 4)) + 2 * Math.PI / (2 + aspect);
}

public sealed class Wing(Planform planform, double breakdownRadians, double separatedRadians, double bluff, double friction)
{
    public static Wing Of(WindConfig config, string kind, Planform planform) =>
        config.Sails.TryGetValue(kind, out var measured)
            ? new Wing(planform, measured.BreakdownDegrees * Math.PI / 180, measured.SeparatedDegrees * Math.PI / 180, config.BluffDrag, config.ClothFriction)
            : throw new InvalidOperationException($"no sail measured for a rig of kind '{kind}'");

    public double Friction => friction;

    public double Pressing(double incidence)
    {
        var angle = Math.Min(Math.Abs(incidence), Math.PI / 2);
        var (sin, cos) = (Math.Sin(angle), Math.Cos(angle));
        var body = bluff * sin * sin;
        var lift = planform.Potential * sin * cos + planform.Vortex * sin * sin - body;
        var held = angle <= breakdownRadians ? 1 : angle >= separatedRadians ? 0 : (separatedRadians - angle) / (separatedRadians - breakdownRadians);
        return body + held * lift;
    }

    public (double Drive, double Side) At(double fromBow, double incidence)
    {
        var angle = Math.Clamp(incidence, 0, Math.PI / 2);
        var (normal, along) = (Pressing(angle), friction * Math.Cos(angle));
        var (lift, drag) = (normal * Math.Cos(angle) - along * Math.Sin(angle), normal * Math.Sin(angle) + along * Math.Cos(angle));
        var (sin, cos) = (Math.Sin(fromBow), Math.Cos(fromBow));
        return (lift * sin - drag * cos, lift * cos + drag * sin);
    }
}
