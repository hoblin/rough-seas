using System;

namespace RoughSeas.Storms;

public class DrawnSea(SeaSurface surface, SurfaceField field) : IDrawnSea
{
    private const double ModelTurn = Math.PI / 2;

    public bool Swell => !surface.Calm;

    public double HeightAt(double x, double z) => ShareAt(x, z) is var share and > 0 ? surface.HeightAt(x, z, share) : 0;

    public SeaPose PoseOf(double x, double z, double yaw, double length, double width)
    {
        var share = ShareAt(x, z);
        if (share <= 0) return default;

        var (sin, cos) = Math.SinCos(yaw + ModelTurn);
        double At(double alongX, double alongZ) =>
            surface.HeightAt(x + cos * alongX + sin * alongZ, z - sin * alongX + cos * alongZ, share);

        var (plusX, minusX, plusZ, minusZ) = (At(length / 2, 0), At(-length / 2, 0), At(0, width / 2), At(0, -width / 2));
        return new SeaPose(
            Math.Min(share, 1),
            (plusX + minusX + plusZ + minusZ) / 4,
            -Math.Atan((plusZ - minusZ) / width),
            Math.Atan((plusX - minusX) / length));
    }

    private double ShareAt(double x, double z) => surface.Calm ? 0 : field.Factor(x, z);
}
