namespace RoughSeas.Storms;

/// <summary>The rate the water's velocity changes across a metre, ∂u_i/∂x_j, per second; the same in both orders for waves.</summary>
public readonly record struct FlowGradient(double XX, double XZ, double ZZ)
{
    /// <summary>How the water's velocity changes per second for something moving through it at this velocity, (v·∇)u.</summary>
    public (double X, double Z) Moving(double x, double z) => (XX * x + XZ * z, XZ * x + ZZ * z);

    public FlowGradient Scaled(double by) => new(XX * by, XZ * by, ZZ * by);
}
