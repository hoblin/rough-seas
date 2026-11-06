using System;

namespace RoughSeas.Propulsion;

public readonly record struct Push(double Newtons, double NewtonMetres);

public abstract class Propulsor(Stroke stroke)
{
    public Stroke Stroke => stroke;

    public Push Push(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts) =>
        Pushed(ahead, turn, side, metresPerSecond, radiansPerSecond, Math.Max(0, watts - stroke.LiftWatts));

    protected abstract Push Pushed(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts);

    protected double Along(int direction, double metresPerSecond, double watts) =>
        direction == 0 ? 0 : direction * stroke.Thrust(direction * metresPerSecond, watts);
}

/// <summary>
/// A tool in the rower's hands. Worked <paramref name="abeamMetres"/> out from her middle on one side, what drives her
/// along turns her away from that side; at none it drives her straight.
/// </summary>
public sealed class HeldTool(Stroke stroke, double sweepMetres, double abeamMetres = 0) : Propulsor(stroke)
{
    protected override Push Pushed(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts)
    {
        var share = ahead != 0 && turn != 0 ? watts / 2 : watts;
        var along = Along(ahead, metresPerSecond, share);
        return new Push(along, sweepMetres * Along(turn, radiansPerSecond * sweepMetres, share) - (int)side * abeamMetres * along);
    }
}

public sealed class TholeOars(Stroke stroke, double spanMetres) : Propulsor(stroke)
{
    protected override Push Pushed(int ahead, int turn, Side side, double metresPerSecond, double radiansPerSecond, double watts)
    {
        var (port, starboard) = Pulls(ahead, turn);
        var share = watts / Math.Max(1, Math.Abs(port) + Math.Abs(starboard));
        var half = spanMetres / 2;
        var portNewtons = Along(port, metresPerSecond - radiansPerSecond * half, share);
        var starboardNewtons = Along(starboard, metresPerSecond + radiansPerSecond * half, share);
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
