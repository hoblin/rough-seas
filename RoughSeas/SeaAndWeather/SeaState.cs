using System;

namespace RoughSeas.SeaAndWeather;

/// <param name="WindX">Wind towards east, m/s.</param>
/// <param name="WindZ">Wind towards south, m/s.</param>
/// <param name="Beaufort">Force on the Beaufort scale.</param>
/// <param name="WaveHeight">Probable wave height from the WMO Beaufort table, m. It builds and falls behind the wind.</param>
/// <param name="Oceanity">0 on land to 1 on open sea; 1 where the map region is not loaded on this side.</param>
/// <param name="StormPresence">0 to 1, the combined presence of the storms here.</param>
/// <param name="StormCode">The strongest storm's type, or null.</param>
public readonly record struct SeaState(
    double WindX,
    double WindZ,
    int Beaufort,
    double WaveHeight,
    double Oceanity,
    double StormPresence,
    string? StormCode)
{
    public double WindSpeed => Math.Sqrt(WindX * WindX + WindZ * WindZ);

    /// <summary>The compass direction the wind blows from, 0 north, 90 east.</summary>
    public double WindFromDegrees => Compass.FromDegrees(WindX, WindZ);
}
