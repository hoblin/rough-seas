using RoughSeas.Water;

namespace RoughSeas.Surf;

/// <summary>What the bed does to a breaker's shape: it plunges where the Iribarren number of the bed's slope says so. <c>seasurface.vsh</c> computes the same.</summary>
public sealed class SurfShape(double breakingDepthRatio, double plungingFrom, double plungingTo, double crestRatio)
{
    public double CrestRatio => crestRatio;

    public double BreakingDepthRatio => breakingDepthRatio;

    public double PlungingFrom => plungingFrom;

    public double PlungingTo => plungingTo;

    public bool Plunges(double slope, double offshoreHeight, double dominantNumber)
    {
        var iribarren = WaveTheory.Iribarren(slope, offshoreHeight, 2 * System.Math.PI / dominantNumber);
        return iribarren >= plungingFrom && iribarren < plungingTo;
    }
}
