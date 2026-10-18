using System;

namespace RoughSeas.SeaAndWeather;

/// <param name="WindX">Wind towards east, m/s.</param>
/// <param name="WindZ">Wind towards south, m/s.</param>
/// <param name="Beaufort">Force on the Beaufort scale.</param>
/// <param name="WaveHeight">Significant wave height of the wind sea, m: from the wind, the fetch and the depth of the water here.</param>
/// <param name="WavePeriod">Peak period of the wind sea, s.</param>
/// <param name="StormPresence">0 to 1, the combined presence of the storms here.</param>
/// <param name="StormCode">The strongest storm's type, or null; known only on the server.</param>
public readonly record struct SeaState(
    double WindX,
    double WindZ,
    int Beaufort,
    double WaveHeight,
    double WavePeriod,
    double StormPresence,
    string? StormCode)
{
    public double WindSpeed => Math.Sqrt(WindX * WindX + WindZ * WindZ);

    /// <summary>The compass direction the wind blows from, 0 north, 90 east.</summary>
    public double WindFromDegrees => Compass.FromDegrees(WindX, WindZ);
}

public readonly record struct SeaSample(
    double WindX,
    double WindZ,
    double StormPresence,
    double RainCloudness,
    double WaveHeight,
    double WavePeriod,
    double CurrentX,
    double CurrentZ,
    double WaterCelsius)
{
    public double WindSpeed => Math.Sqrt(WindX * WindX + WindZ * WindZ);
}
