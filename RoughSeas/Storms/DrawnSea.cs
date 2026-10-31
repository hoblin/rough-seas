using System;

namespace RoughSeas.Storms;

/// <param name="shareAt">The share of <paramref name="surface"/>'s waves drawn at a position: the client's <see cref="SurfaceField.Factor"/>.</param>
public class DrawnSea(SeaSurface surface, Func<double, double, double> shareAt) : IDrawnSea
{
    private const double ModelTurn = Math.PI / 2;

    public bool Swell => !surface.Calm;

    public bool SwellAt(double x, double z) => ShareAt(x, z) > 0;

    public double Seconds => surface.Seconds;

    public double HeightAt(double x, double z) => ShareAt(x, z) is var share and > 0 ? surface.HeightAt(x, z, share) : 0;

    public (double X, double Z) OrbitalVelocityAt(double x, double z, double depth = 0, double side = 0) =>
        ShareAt(x, z) is var share and > 0 ? surface.OrbitalVelocityAt(x, z, depth, side, share) : default;

    public (double X, double Z) HullVelocityAt(double x, double z, double lift, double draft, double yaw, double length, double beam) =>
        ShareAt(x, z) is var share and > 0 ? surface.HullVelocityAt(x, z, lift, draft, yaw, length, beam, share) : default;

    public SeaPose PoseOf(double x, double z, double yaw, double length, double width)
    {
        var share = ShareAt(x, z);
        if (share <= 0) return default;

        var (sin, cos) = Math.SinCos(yaw + ModelTurn);
        double At(double alongX, double alongZ) =>
            surface.HeightAt(x + cos * alongX + sin * alongZ, z - sin * alongX + cos * alongZ, share);

        var (plusX, minusX, plusZ, minusZ) = (At(length / 2, 0), At(-length / 2, 0), At(0, width / 2), At(0, -width / 2));
        return new SeaPose(
            share,
            (plusX + minusX + plusZ + minusZ) / 4,
            -Math.Atan((plusZ - minusZ) / width),
            Math.Atan((plusX - minusX) / length));
    }

    private double ShareAt(double x, double z) => surface.Calm ? 0 : shareAt(x, z);
}
