using System;

namespace RoughSeas.SeaAndWeather;

/// <summary>Vanilla's water drag on what floats, and its motion's unit: blocks per tick of 1/60 s.</summary>
public static class WaterDrag
{
    public const double TicksPerSecond = 60;

    private const double DragTicksPerSecond = 33;

    /// <summary>The share of a body's motion vanilla's water drag keeps over <paramref name="seconds"/>.</summary>
    public static double Kept(double waterDrag, double seconds) => Math.Pow(waterDrag, seconds * DragTicksPerSecond);
}
