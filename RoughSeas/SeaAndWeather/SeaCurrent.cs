using System;

namespace RoughSeas.SeaAndWeather;

/// <param name="X">Current towards east, m/s.</param>
/// <param name="Z">Current towards south, m/s.</param>
/// <param name="WaterCelsius">Sea surface temperature, °C.</param>
/// <param name="WarmthCelsius">How much the current warms the water, °C; below zero it cools it. Known only where the model
/// runs: the client's map carries the water's temperature, not its parts.</param>
public readonly record struct SeaCurrent(double X, double Z, double WaterCelsius, double? WarmthCelsius)
{
    public double Speed => Math.Sqrt(X * X + Z * Z);

    /// <summary>The compass direction the current flows towards, 0 north, 90 east.</summary>
    public double TowardDegrees => Compass.TowardDegrees(X, Z);
}
