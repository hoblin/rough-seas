using System;

namespace RoughSeas.Surf;

/// <summary>Linear wave theory over a bed, by deep-water wavenumber: Eckart's dispersion, shoaling by the group velocity, refraction by Snell's law over straight depth contours.</summary>
public static class Shoaling
{
    public const double Gravity = 9.81;

    public static double Wavenumber(double deepNumber, double depth) =>
        depth <= 0 ? double.PositiveInfinity : deepNumber / Math.Sqrt(Math.Tanh(deepNumber * depth));

    public static double Factor(double deepNumber, double depth)
    {
        if (depth <= 0) return 0;
        var number = Wavenumber(deepNumber, depth);
        var kd = Math.Min(number * depth, 20);
        var groupShare = 0.5 * (1 + 2 * kd / Math.Sinh(2 * kd));
        var celerity = Math.Sqrt(Gravity * deepNumber) / number;
        var deepGroup = 0.5 * Math.Sqrt(Gravity / deepNumber);
        return Math.Sqrt(deepGroup / (groupShare * celerity));
    }

    public static double Refraction(double deepNumber, double depth, double cosineOffshore)
    {
        var sineOffshore = Math.Sqrt(Math.Max(0, 1 - cosineOffshore * cosineOffshore));
        var sine = sineOffshore * deepNumber / Wavenumber(deepNumber, depth);
        var cosine = Math.Sqrt(Math.Max(0, 1 - sine * sine));
        return cosine > 0 ? Math.Sqrt(Math.Abs(cosineOffshore) / cosine) : 1;
    }

    public static double Iribarren(double slope, double height, double deepNumber) =>
        height > 0 ? slope / Math.Sqrt(height * deepNumber / (2 * Math.PI)) : double.PositiveInfinity;
}
