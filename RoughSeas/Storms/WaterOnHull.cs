namespace RoughSeas.Storms;

/// <summary>
/// The water a hull meets in one step, in m/s of the tick's clock: the current and the waves' water at her place, in
/// world axes, how the waves' water changes across a metre, and her own way through the water along her
/// (<see cref="RoughSeas.Vessels.HullShape.ToHull"/>'s axis).
/// </summary>
public readonly record struct WaterOnHull(double CurrentX, double CurrentZ, double WaveX = 0, double WaveZ = 0, FlowGradient Gradient = default, double WayAlong = 0)
{
    public double X => CurrentX + WaveX;

    public double Z => CurrentZ + WaveZ;
}

/// <summary>The rate the water's velocity changes across a metre, ∂u_i/∂x_j, per second; the same in both orders for waves.</summary>
public readonly record struct FlowGradient(double XX, double XZ, double ZZ)
{
    /// <summary>How the water's velocity changes per second for something moving through it at this velocity, (v·∇)u.</summary>
    public (double X, double Z) Along(double x, double z) => (XX * x + XZ * z, XZ * x + ZZ * z);

    public FlowGradient Scaled(double by) => new(XX * by, XZ * by, ZZ * by);
}
