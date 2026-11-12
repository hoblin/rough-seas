using System;
using RoughSeas.Water;

namespace RoughSeas.Storms;

public class DrawnSea(SeaSurface surface, SurfaceField field) : IDrawnSea
{
    private const double ModelTurn = Math.PI / 2;

    public bool Swell => !surface.Trains.Calm;

    public bool SwellAt(double x, double z) => ShareAt(x, z) > 0;

    public double Seconds => surface.Trains.Seconds;

    public double HeightAt(double x, double z) => ShareAt(x, z) is var share and > 0 ? surface.HeightAt(x, z, share) : 0;

    public WaveReading WaveHere(double x, double z) => ShareAt(x, z) is var share and > 0 ? surface.WaveHere(x, z, share) : default;

    public (double X, double Z) HullVelocityAt(double x, double z, double lift, double draft, double yaw, double length, double beam) =>
        ShareAt(x, z) is var share and > 0 ? surface.HullVelocityAt(x, z, lift, draft, yaw, length, beam, share) : default;

    public (double X, double Z) HullAccelerationAt(double x, double z, double lift, double draft, double yaw, double length, double beam) =>
        ShareAt(x, z) is var share and > 0 ? surface.HullAccelerationAt(x, z, lift, draft, yaw, length, beam, share) : default;

    public double RiseVelocityAt(double x, double z, double below = 0) =>
        ShareAt(x, z) is var share and > 0 ? surface.RiseVelocityAt(x, z, below, share) : 0;

    public double RiseAccelerationAt(double x, double z, double below = 0) =>
        ShareAt(x, z) is var share and > 0 ? surface.RiseAccelerationAt(x, z, below, share) : 0;

    public SeaPose PoseOf(double x, double z, double yaw, double length, double width)
    {
        var share = ShareAt(x, z);
        if (share <= 0) return default;

        var (sin, cos) = Math.SinCos(yaw + ModelTurn);
        double At(double alongX, double alongZ) =>
            surface.HeightAt(x + cos * alongX + sin * alongZ, z - sin * alongX + cos * alongZ, share, shortWaves: false);

        var (plusX, minusX, plusZ, minusZ) = (At(length / 2, 0), At(-length / 2, 0), At(0, width / 2), At(0, -width / 2));
        return new SeaPose(
            share,
            (plusX + minusX + plusZ + minusZ) / 4,
            -Math.Atan((plusZ - minusZ) / width),
            Math.Atan((plusX - minusX) / length));
    }

    private double ShareAt(double x, double z) => surface.Trains.Calm ? 0 : field.Factor(x, z);
}
