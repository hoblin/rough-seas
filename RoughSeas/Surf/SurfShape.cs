using System;

namespace RoughSeas.Surf;

/// <summary>
/// The waves over a bed: each slot shoaled and refracted by the depth, then all of them held to the height the depth
/// carries, the rest breaking; the breaker plunges where the Iribarren number of the bed's slope says so.
/// <c>seasurface.vsh</c> computes the same.
/// </summary>
public sealed class SurfShape(double breakingDepthRatio, double plungingFrom, double plungingTo)
{
    public double BreakingDepthRatio => breakingDepthRatio;

    public double PlungingFrom => plungingFrom;

    public double PlungingTo => plungingTo;

    public Shaped Shape(double depth, double slopeX, double slopeZ, ReadOnlySpan<(double X, double Z)> numbers, ReadOnlySpan<double> amplitudes,
        Span<double> shaped, Span<double> localNumbers, int dominant)
    {
        var slope = Math.Sqrt(slopeX * slopeX + slopeZ * slopeZ);
        double offshore = 0, local = 0;
        for (var slot = 0; slot < amplitudes.Length; slot++)
        {
            var (numberX, numberZ) = numbers[slot];
            var deepNumber = Math.Sqrt(numberX * numberX + numberZ * numberZ);
            if (amplitudes[slot] <= 0 || deepNumber <= 0)
            {
                (shaped[slot], localNumbers[slot]) = (0, deepNumber);
                continue;
            }
            var cosine = slope > 0 ? -(numberX * slopeX + numberZ * slopeZ) / (deepNumber * slope) : 1;
            shaped[slot] = amplitudes[slot] * Shoaling.Factor(deepNumber, depth) * (cosine > 0 ? Shoaling.Refraction(deepNumber, depth, cosine) : 1);
            localNumbers[slot] = Shoaling.Wavenumber(deepNumber, depth);
            offshore += amplitudes[slot] * amplitudes[slot];
            local += shaped[slot] * shaped[slot];
        }
        if (local <= 0) return default;

        var height = 4 * Math.Sqrt(local / 2);
        var limit = depth / breakingDepthRatio;
        var kept = Math.Min(1, limit / height);
        for (var slot = 0; slot < amplitudes.Length; slot++) shaped[slot] *= kept;

        var (x, z) = numbers[dominant];
        var iribarren = Shoaling.Iribarren(slope, 4 * Math.Sqrt(offshore / 2), Math.Sqrt(x * x + z * z));
        return new Shaped(1 - kept, iribarren >= plungingFrom && iribarren < plungingTo);
    }
}

public readonly record struct Shaped(double Breaking, bool Plunging);
