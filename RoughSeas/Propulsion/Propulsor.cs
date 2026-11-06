using System;

namespace RoughSeas.Propulsion;

public readonly record struct Push(double Newtons, double NewtonMetres);

public abstract class Propulsor(Stroke stroke)
{
    public Stroke Stroke => stroke;

    public Push Push(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts, double? drawnShare = null)
    {
        var stroking = StrokeSpeed(ahead, turn, metresPerSecond, radiansPerSecond);
        return Pushed(ahead, turn, side, metresPerSecond, radiansPerSecond, Math.Max(0, watts - stroke.LiftWatts(stroking)),
            (direction, speed, power) => direction == 0 ? 0 : direction * stroke.Shove(stroke.Thrust(direction * speed, power, stroking), stroking, drawnShare));
    }

    public virtual double StrokeSpeed(int ahead, int turn, double metresPerSecond, double radiansPerSecond) => Math.Max(0, ahead * metresPerSecond);

    protected abstract Push Pushed(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts, Thrusting along);

    protected delegate double Thrusting(int direction, double metresPerSecond, double watts);
}

/// <summary>
/// A tool in the rower's hands. Worked <paramref name="abeamMetres"/> out from her middle on one side, what drives her
/// along turns her away from that side; at none it drives her straight.
/// </summary>
public sealed class HeldTool(Stroke stroke, double sweepMetres, double abeamMetres = 0) : Propulsor(stroke)
{
    public override double StrokeSpeed(int ahead, int turn, double metresPerSecond, double radiansPerSecond) =>
        ahead != 0 ? Math.Max(0, ahead * metresPerSecond) : Math.Max(0, turn * radiansPerSecond * sweepMetres);

    protected override Push Pushed(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts, Thrusting along)
    {
        var share = ahead != 0 && turn != 0 ? watts / 2 : watts;
        var driving = along(ahead, metresPerSecond, share);
        return new Push(driving, sweepMetres * along(turn, radiansPerSecond * sweepMetres, share) - (int)side * abeamMetres * driving);
    }
}

public sealed class TholeOars(Stroke stroke, double spanMetres) : Propulsor(stroke)
{
    protected override Push Pushed(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts, Thrusting along)
    {
        var (port, starboard) = Pulls(ahead, turn);
        var share = watts / Math.Max(1, Math.Abs(port) + Math.Abs(starboard));
        var half = spanMetres / 2;
        var portNewtons = along(port, metresPerSecond - radiansPerSecond * half, share);
        var starboardNewtons = along(starboard, metresPerSecond + radiansPerSecond * half, share);
        return new Push(portNewtons + starboardNewtons, half * (starboardNewtons - portNewtons));
    }

    private static (int Port, int Starboard) Pulls(int ahead, int turn) => (ahead, turn) switch
    {
        (_, 0) => (ahead, ahead),
        (0, _) => (-turn, turn),
        (> 0, > 0) => (0, 1),
        (> 0, _) => (1, 0),
        (_, > 0) => (-1, 0),
        _ => (0, -1)
    };
}
