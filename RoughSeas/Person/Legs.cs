using System;

namespace RoughSeas.Person;

/// <summary>
/// The legs pushing a body off the bed with the force their measured profile gives the push. The profile,
/// F = F0 (1 − v / v0), was measured as the push's mean force against its mean speed, half the take-off speed
/// (Samozino et al. 2008), so the push is one steady force: the one at which the legs, stretching their push against what
/// holds the body back, leave at the take-off speed that force gives. It runs along the stroke until they have stretched
/// their push or the body gains nothing on it, and again only once the feet have left the bed and come back to it.
/// </summary>
public sealed class Legs(Body body, LegsConfig config)
{
    private const int Halvings = 40;

    private (double X, double Y, double Z, SwimStroke Way, double Gained, double Newtons)? push;
    private bool landed = true;

    public double MaxNewtons => config.NewtonsPerKg * body.MassKg;

    /// <param name="inertiaKg">The body's mass and the water it drags along the push.</param>
    /// <param name="heldBackNewtons">What holds the body back along the push, its weight in the water when it pushes up.</param>
    public (double X, double Y, double Z) Push(double x, double y, double z, bool onBed, SwimStroke stroke, double inertiaKg, double heldBackNewtons)
    {
        if (!onBed) landed = true;
        if (push == null && onBed && landed && stroke.Jump)
        {
            push = (x, y, z, stroke, double.NegativeInfinity, Steady(inertiaKg, heldBackNewtons));
            landed = false;
        }
        if (push is not var (fromX, fromY, fromZ, way, gained, newtons)) return default;
        var reached = (x - fromX) * way.X + (y - fromY) * way.Y + (z - fromZ) * way.Z;
        if (reached >= config.PushMetres || reached <= gained)
        {
            push = null;
            return default;
        }
        push = (fromX, fromY, fromZ, way, reached, newtons);
        return (newtons * way.X, newtons * way.Y, newtons * way.Z);
    }

    /// <summary>The steady force F = F0 (1 − v / 2 v0) at which a push over its length leaves at v² = 2 d (F − held back) / inertia.</summary>
    public double Steady(double inertiaKg, double heldBackNewtons)
    {
        var (low, high) = (Math.Max(0, heldBackNewtons), MaxNewtons);
        if (high <= low) return 0;
        for (var i = 0; i < Halvings; i++)
        {
            var force = (low + high) / 2;
            var takeOff = Math.Sqrt(2 * config.PushMetres * (force - heldBackNewtons) / inertiaKg);
            if (force < MaxNewtons * (1 - takeOff / (2 * config.MetresPerSecond))) low = force;
            else high = force;
        }
        return (low + high) / 2;
    }
}
