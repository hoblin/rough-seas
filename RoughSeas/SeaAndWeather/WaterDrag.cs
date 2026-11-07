using System;

namespace RoughSeas.SeaAndWeather;

/// <summary>Vanilla's water drag on what floats, and its motion's unit: blocks per tick of 1/60 s.</summary>
public static class WaterDrag
{
    public const double TicksPerSecond = 60;

    private const double DragTicksPerSecond = 33;

    /// <summary>The share of a body's motion vanilla's water drag keeps over <paramref name="seconds"/>.</summary>
    public static double Kept(double waterDrag, double seconds) => Math.Pow(waterDrag, seconds * DragTicksPerSecond);

    /// <summary>
    /// What a body's motion takes this step, in blocks per tick, for the part of it the water carries to go from
    /// <paramref name="beforeMetresPerSecond"/> to <paramref name="metresPerSecond"/>, on top of the body's own way.
    /// Vanilla's drag acts on the whole motion: <paramref name="keptSince"/> is what it has kept of last step's since then,
    /// and <paramref name="keptNext"/> what it will keep of this step's when it runs after this. A body left to it comes to
    /// the water's velocity and vanilla's drag only slows its own way.
    /// </summary>
    public static double Carried(double metresPerSecond, double beforeMetresPerSecond, double keptSince, double keptNext = 1) =>
        (metresPerSecond / keptNext - keptSince * beforeMetresPerSecond) / TicksPerSecond;
}
