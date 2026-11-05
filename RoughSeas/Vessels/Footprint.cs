using System;

namespace RoughSeas.Vessels;

public readonly record struct Footprint(double X, double Z, double AlongX, double AlongZ, double HalfLengthMetres, double HalfBeamMetres)
{
    public static Footprint Of(HullShape shape, double x, double z, double yaw)
    {
        var (centreX, centreZ) = HullShape.ToWorld(shape.CentreAlongMetres, shape.CentreAcrossMetres, yaw);
        var (alongX, alongZ) = HullShape.ToWorld(1, 0, yaw);
        return new Footprint(x + centreX, z + centreZ, alongX, alongZ, shape.HalfLengthMetres, shape.HalfBeamMetres);
    }

    public bool Touches(Footprint other) =>
        !Apart(AlongX, AlongZ, other) && !Apart(-AlongZ, AlongX, other) &&
        !Apart(other.AlongX, other.AlongZ, other) && !Apart(-other.AlongZ, other.AlongX, other);

    private bool Apart(double axisX, double axisZ, Footprint other) =>
        Math.Abs((other.X - X) * axisX + (other.Z - Z) * axisZ) > Reach(axisX, axisZ) + other.Reach(axisX, axisZ);

    private double Reach(double axisX, double axisZ) =>
        HalfLengthMetres * Math.Abs(AlongX * axisX + AlongZ * axisZ) + HalfBeamMetres * Math.Abs(-AlongZ * axisX + AlongX * axisZ);
}
