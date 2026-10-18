using System;

namespace RoughSeas.SeaAndWeather;

public class WindSea(WavesConfig config)
{
    private const double Gravity = 9.81;

    public (double Height, double Period) Grow(double windMps, double fetchMetres, double depthMetres, double blowingHours = double.PositiveInfinity)
    {
        if (windMps <= 0 || fetchMetres <= 0 || depthMetres <= 0 || blowingHours <= 0) return (0, 0);
        var windSquared = windMps * windMps;
        var depth = Gravity * depthMetres / windSquared;
        var fetch = Gravity * Math.Min(fetchMetres, DurationFetch(windMps, blowingHours)) / windSquared;

        var heightDepthLimit = Math.Tanh(0.493 * Math.Pow(depth, 0.75));
        var energy = 3.64e-3 * Math.Pow(heightDepthLimit * Math.Tanh(3.13e-3 * Math.Pow(fetch, 0.57) / heightDepthLimit), 1.74);
        var periodDepthLimit = Math.Tanh(0.331 * Math.Pow(depth, 1.01));
        var frequency = 0.133 * Math.Pow(periodDepthLimit * Math.Tanh(5.215e-4 * Math.Pow(fetch, 0.73) / periodDepthLimit), -0.37);

        var height = Soften(4 * Math.Sqrt(energy) * windSquared / Gravity);
        var period = Math.Min(windMps / (Gravity * frequency), 9.78 * Math.Sqrt(depthMetres / Gravity));
        return (height, period);
    }

    private static double DurationFetch(double windMps, double blowingHours)
    {
        if (double.IsPositiveInfinity(blowingHours)) return double.PositiveInfinity;
        var friction = windMps * Math.Sqrt(0.001 * (1.1 + 0.035 * windMps));
        return 5.23e-3 * Math.Pow(Gravity * blowingHours * 3600 / friction, 1.5) * friction * friction / Gravity;
    }

    private double Soften(double height)
    {
        if (height <= config.KneeMetres) return height;
        var headroom = config.CeilingMetres - config.KneeMetres;
        return config.KneeMetres + headroom * Math.Tanh((height - config.KneeMetres) / headroom);
    }
}
