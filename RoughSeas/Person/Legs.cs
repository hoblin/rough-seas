namespace RoughSeas.Person;

public sealed class Legs(Body body, LegsConfig config, double gravity)
{
    private (double X, double Y, double Z, SwimStroke Way)? push;
    private bool landed = true;

    public double Newtons => body.MassKg * gravity * (1 + config.JumpMetres / config.PushMetres);

    public (double X, double Y, double Z) Push(double x, double y, double z, bool onBed, SwimStroke stroke)
    {
        if (!onBed) landed = true;
        if (push == null && onBed && landed && stroke.Y > 0)
        {
            push = (x, y, z, stroke);
            landed = false;
        }
        if (push is not var (fromX, fromY, fromZ, way)) return default;
        if ((x - fromX) * way.X + (y - fromY) * way.Y + (z - fromZ) * way.Z >= config.PushMetres)
        {
            push = null;
            return default;
        }
        return (Newtons * way.X, Newtons * way.Y, Newtons * way.Z);
    }
}
