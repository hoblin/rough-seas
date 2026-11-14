namespace RoughSeas.Person;

/// <summary>
/// The legs pushing a body off the bed, by their measured force-velocity profile: F = F0 (1 − v / v0) at the body's speed
/// v away from the bed, along the stroke, until they have stretched their push or the body stops gaining on it. They push
/// again only once the feet have left the bed and come back to it.
/// </summary>
public sealed class Legs(Body body, LegsConfig config)
{
    private (double X, double Y, double Z, SwimStroke Way, double Gained)? push;
    private bool landed = true;

    public double MaxNewtons => config.NewtonsPerKg * body.MassKg;

    public (double X, double Y, double Z) Push(double x, double y, double z, (double X, double Y, double Z) velocity, bool onBed, SwimStroke stroke)
    {
        if (!onBed) landed = true;
        if (push == null && onBed && landed && stroke.Jump)
        {
            push = (x, y, z, stroke, double.NegativeInfinity);
            landed = false;
        }
        if (push is not var (fromX, fromY, fromZ, way, gained)) return default;
        var reached = (x - fromX) * way.X + (y - fromY) * way.Y + (z - fromZ) * way.Z;
        if (reached >= config.PushMetres || reached <= gained)
        {
            push = null;
            return default;
        }
        push = (fromX, fromY, fromZ, way, reached);
        var speed = velocity.X * way.X + velocity.Y * way.Y + velocity.Z * way.Z;
        var newtons = MaxNewtons * System.Math.Max(0, 1 - speed / config.MetresPerSecond);
        return (newtons * way.X, newtons * way.Y, newtons * way.Z);
    }
}
