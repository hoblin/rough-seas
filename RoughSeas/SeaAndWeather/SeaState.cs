using System;

namespace RoughSeas.SeaAndWeather;

/// <param name="WindX">Wind towards east, m/s.</param>
/// <param name="WindZ">Wind towards south, m/s.</param>
/// <param name="Beaufort">Force on the Beaufort scale.</param>
/// <param name="WaveHeight">Significant wave height, m. It builds and falls behind the wind.</param>
/// <param name="Oceanity">0 on land to 1 on open sea; 1 where the map region is not loaded on this side.</param>
/// <param name="StormPresence">0 to 1, how deep the position lies in the strongest storm here.</param>
/// <param name="StormRain">0 to 1, the rain the storms bring.</param>
/// <param name="StormCode">The strongest storm's type, or null.</param>
public readonly record struct SeaState(
    double WindX,
    double WindZ,
    int Beaufort,
    double WaveHeight,
    double Oceanity,
    double StormPresence,
    double StormRain,
    string? StormCode)
{
    public double WindSpeed => Math.Sqrt(WindX * WindX + WindZ * WindZ);

    /// <summary>The compass direction the wind blows from, 0 north, 90 east.</summary>
    public double WindFromDegrees => PrevailingWinds.FromDegrees(WindX, WindZ);
}
