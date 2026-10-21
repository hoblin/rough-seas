using System;

namespace RoughSeas.Vessels;

/// <summary>How strong one piece of a material is against its tables: a factor of mean 1, spread lognormally by the property's coefficient of variation.</summary>
public static class Scatter
{
    public static double Of(double variation, Random rand)
    {
        double sigma = Math.Sqrt(Math.Log(1 + variation * variation));
        double normal = Math.Sqrt(-2 * Math.Log(1 - rand.NextDouble())) * Math.Cos(2 * Math.PI * rand.NextDouble());
        return Math.Exp(sigma * normal - sigma * sigma / 2);
    }
}
