namespace RoughSeas.Storms;

public readonly record struct WaterOnHull(double CurrentX, double CurrentZ, double WaveX = 0, double WaveZ = 0, FlowGradient Gradient = default, double WayAlong = 0)
{
    public double X => CurrentX + WaveX;

    public double Z => CurrentZ + WaveZ;
}

public readonly record struct FlowGradient(double XX, double XZ, double ZZ)
{
    public (double X, double Z) Along(double x, double z) => (XX * x + XZ * z, XZ * x + ZZ * z);

    public FlowGradient Scaled(double by) => new(XX * by, XZ * by, ZZ * by);
}
