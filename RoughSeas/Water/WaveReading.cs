namespace RoughSeas.Water;

/// <summary>The waves standing at a point now, over the bed under it.</summary>
/// <param name="Height">Their significant height here, in metres.</param>
/// <param name="Period">The dominant wave's period, in seconds.</param>
/// <param name="TowardRadians">Where the dominant wave runs here, as the bed turns it: 0 along +X, a quarter turn along +Z.</param>
/// <param name="Depth">The depth the waves feel; infinite where the bed isn't read.</param>
/// <param name="BedSlope">The bed's slope at the scale the waves feel it.</param>
/// <param name="Breaking">The share of waves breaking here.</param>
/// <param name="Arriving">The significant height of the sea coming into the breaking water here, before the shallows take it down: what a shore's surf is as big as.</param>
public readonly record struct WaveReading(double Height, double Period, double TowardRadians, double Depth, double BedSlope, double Breaking, double Arriving)
{
    private const double BreakingFromShare = 0.1;

    /// <summary>Whether enough waves break here to call it surf.</summary>
    public bool Surf => IsSurf(Breaking);

    public static bool IsSurf(double breaking) => breaking >= BreakingFromShare;
}
