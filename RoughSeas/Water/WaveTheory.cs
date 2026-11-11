using System;

namespace RoughSeas.Water;

/// <summary>The formulas of water waves, each written once: linear wave theory over a bed by deep-water wavenumber (Eckart's dispersion), the statistics of a sea's heights, and a broken wave's shape. <c>seasurface.vsh</c> takes the constants from here.</summary>
public static class WaveTheory
{
    public const double Gravity = 9.81;

    private const double FullTurn = 2 * Math.PI;
    private const double NarrowestBoreFront = 0.02;
    private const double WidestBoreFront = 0.5;
    private const double DeepEnoughKd = 20;

    /// <summary>A sawtooth carries as much energy as a sine of 1 at this amplitude: its variance is a third of the square of it, a sine's a half.</summary>
    public static readonly double BoreAmplitude = Math.Sqrt(1.5);

    /// <summary>The height exceeded by 2% of a sea's waves, over its significant height.</summary>
    public static readonly double TwoPercentRatio = Math.Sqrt(Math.Log(50) / 2);

    public static double DeepLength(double period) => Gravity * period * period / FullTurn;

    public static double DeepNumber(double period) => FullTurn / DeepLength(period);

    public static double DeepFrequency(double deepNumber) => Math.Sqrt(Gravity * deepNumber);

    public static double Wavenumber(double deepNumber, double depth) =>
        depth <= 0 ? double.PositiveInfinity : deepNumber / Math.Sqrt(Math.Tanh(deepNumber * depth));

    public static double Celerity(double period, double depth) => period > 0 ? FullTurn / period / Wavenumber(DeepNumber(period), depth) : 0;

    public static double GroupSpeed(double deepNumber, double depth)
    {
        var number = Wavenumber(deepNumber, depth);
        var kd = Math.Min(number * depth, 20);
        return 0.5 * (1 + 2 * kd / Math.Sinh(2 * kd)) * DeepFrequency(deepNumber) / number;
    }

    /// <summary>
    /// A wave's orbital speed, against the surface's in deep water, averaged over a body from <paramref name="lift"/> over still water to <paramref name="draft"/> under that:
    /// linear theory's cosh k(d + z) / sinh kd, which in deep water is e^(kz).
    /// </summary>
    public static double OrbitalFade(double number, double depth, double lift, double draft)
    {
        if (double.IsInfinity(depth) || number * depth > DeepEnoughKd) return Math.Exp(number * lift) * (draft > 0 ? (1 - Math.Exp(-number * draft)) / (number * draft) : 1);
        var bottom = Math.Max(lift - draft, -depth);
        var span = lift - bottom;
        var integral = span > 0 ? (Math.Sinh(number * (depth + lift)) - Math.Sinh(number * (depth + bottom))) / (number * span) : Math.Cosh(number * (depth + lift));
        return integral / Math.Sinh(number * depth);
    }

    /// <summary>
    /// A wave's vertical motion <paramref name="below"/> metres under a surface standing <paramref name="surface"/> over
    /// still water, against the surface's own: sinh k(d + z) / sinh kd, e^(kz) in deep water, with z stretched over the
    /// whole column up to the surface (Wheeler 1970), so the surface moves as it is drawn and the bed stands still.
    /// </summary>
    public static double RiseFade(double number, double depth, double below, double surface)
    {
        if (double.IsInfinity(depth) || number * depth > DeepEnoughKd) return Math.Exp(-number * below);
        var column = depth + surface;
        if (column <= 0 || below >= column) return 0;
        return Math.Sinh(number * depth * (1 - below / column)) / Math.Sinh(number * depth);
    }

    public static double Iribarren(double slope, double height, double deepLength) =>
        height > 0 && deepLength > 0 ? slope / Math.Sqrt(height / deepLength) : double.PositiveInfinity;

    /// <summary>The significant height of waves whose amplitudes' squares add up to this.</summary>
    public static double SignificantHeight(double squaredAmplitudes) => 4 * Math.Sqrt(squaredAmplitudes / 2);

    /// <summary>The height of one wave in a sea of <paramref name="significantHeight"/>, which <paramref name="draw"/> of all waves fall below: wave heights follow the Rayleigh distribution.</summary>
    public static double RayleighHeight(double significantHeight, double draw) =>
        significantHeight * Math.Sqrt(-Math.Log(1 - draw) / 2);

    /// <summary>The share of a wavelength a bore's front takes, this high on this wavenumber, at this slope.</summary>
    public static double BoreFront(double height, double number, double slope) =>
        Math.Clamp(height * number / (FullTurn * slope), NarrowestBoreFront, WidestBoreFront);

    /// <summary>A bore's surface at this phase, of unit energy: a steep front that <paramref name="front"/> of the wavelength takes, and a long gentle back.</summary>
    public static double Bore(double theta, double front)
    {
        var ahead = ((theta - Math.PI / 2) % FullTurn + FullTurn) % FullTurn / FullTurn;
        return BoreAmplitude * (ahead < front ? 1 - 2 * ahead / front : 2 * (ahead - front) / (1 - front) - 1);
    }

    public static double BoreSlope(double theta, double front)
    {
        var ahead = ((theta - Math.PI / 2) % FullTurn + FullTurn) % FullTurn / FullTurn;
        return BoreAmplitude / FullTurn * (ahead < front ? -2 / front : 2 / (1 - front));
    }
}
