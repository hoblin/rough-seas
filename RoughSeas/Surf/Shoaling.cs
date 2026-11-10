using System;

namespace RoughSeas.Surf;

/// <summary>Linear wave theory over a bed, by deep-water wavenumber: Eckart's dispersion and the group speed, which carries a wave's energy.</summary>
public static class Shoaling
{
    public const double Gravity = 9.81;

    public static double Wavenumber(double deepNumber, double depth) =>
        depth <= 0 ? double.PositiveInfinity : deepNumber / Math.Sqrt(Math.Tanh(deepNumber * depth));

    public static double GroupSpeed(double deepNumber, double depth)
    {
        var number = Wavenumber(deepNumber, depth);
        var kd = Math.Min(number * depth, 20);
        return 0.5 * (1 + 2 * kd / Math.Sinh(2 * kd)) * Math.Sqrt(Gravity * deepNumber) / number;
    }

    public static double Iribarren(double slope, double height, double deepNumber) =>
        height > 0 ? slope / Math.Sqrt(height * deepNumber / (2 * Math.PI)) : double.PositiveInfinity;
}
