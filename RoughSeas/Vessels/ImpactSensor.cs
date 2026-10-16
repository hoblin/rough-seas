using System;

namespace RoughSeas.Vessels;

/// <summary>
/// Turns a vessel's horizontal velocity samples (m/s) into impacts: a sudden change of velocity, reported once
/// it ends, as the speed lost.
/// </summary>
public class ImpactSensor
{
    private const int Window = 3;

    private readonly double minImpactSpeed;
    private readonly double[] xs = new double[Window];
    private readonly double[] zs = new double[Window];
    private int samples;
    private double filteredX, filteredZ;
    private bool inImpact;
    private double impactFromX, impactFromZ;

    public ImpactSensor(double minImpactSpeed)
    {
        this.minImpactSpeed = minImpactSpeed;
    }

    public double? Sample(double velocityX, double velocityZ)
    {
        xs[samples % Window] = velocityX;
        zs[samples % Window] = velocityZ;
        samples++;
        if (samples < Window) return null;

        // A steered vessel's samples come from its rider's position packets; a lost or teleporting packet makes
        // a single spike, which the median drops before it can read as an impact.
        double x = Median(xs), z = Median(zs);
        if (samples == Window)
        {
            (filteredX, filteredZ) = (x, z);
            return null;
        }

        double? impact = null;
        if (Distance(x, z, filteredX, filteredZ) >= minImpactSpeed)
        {
            if (!inImpact) (impactFromX, impactFromZ, inImpact) = (filteredX, filteredZ, true);
        }
        else if (inImpact)
        {
            impact = Distance(impactFromX, impactFromZ, filteredX, filteredZ);
            inImpact = false;
        }

        (filteredX, filteredZ) = (x, z);
        return impact;
    }

    private static double Median(double[] window) =>
        Math.Max(Math.Min(window[0], window[1]), Math.Min(Math.Max(window[0], window[1]), window[2]));

    private static double Distance(double x1, double z1, double x2, double z2) =>
        Math.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));
}
