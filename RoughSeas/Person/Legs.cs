using System;

namespace RoughSeas.Person;

/// <summary>
/// The legs pushing a body up off the bed with the force their measured profile gives the push. The profile,
/// F = F0 (1 − v / v0), was measured as the push's mean force against its mean speed, half the take-off speed
/// (Samozino et al. 2008), so the push is one steady force: the one at which the legs, stretching their push against what
/// holds the body back, leave at the take-off speed that force gives. It runs up until they have stretched their push or
/// the body gains nothing on it, and again only once the feet have left the bed and come back to it.
/// </summary>
public sealed class Legs(Body body, LegsConfig config)
{
    private const int Halvings = 40;

    private (double From, double Gained, double Newtons)? push;
    private bool landed = true;

    public double MaxNewtons => config.NewtonsPerKg * body.MassKg;

    /// <returns>The push up, in newtons.</returns>
    /// <param name="inertiaKg">The body's mass and the water it drags along the push.</param>
    /// <param name="heldBackNewtons">What holds the body back, its weight in the water.</param>
    public double Push(double feet, bool onBed, bool jump, double inertiaKg, double heldBackNewtons)
    {
        if (!onBed) landed = true;
        if (push == null && onBed && landed && jump)
        {
            push = (feet, double.NegativeInfinity, Steady(inertiaKg, heldBackNewtons));
            landed = false;
        }
        if (push is not var (from, gained, newtons)) return 0;
        var reached = feet - from;
        if (reached >= config.PushMetres || reached <= gained)
        {
            push = null;
            return 0;
        }
        push = (from, reached, newtons);
        return newtons;
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
