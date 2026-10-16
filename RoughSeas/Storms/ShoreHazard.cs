using System;
using System.Collections.Generic;

namespace RoughSeas.Storms;

public class ShoreHazard(AshoreConfig config)
{
    public double Chance(IEnumerable<double> heights, double hoursPerSample, double openWaterToShore)
    {
        double hazard = 0;
        foreach (var height in heights)
        {
            hazard += config.HazardPerSquareMetreHour * height * height * hoursPerSample * Exposure(openWaterToShore, height);
        }
        return 1 - Math.Exp(-hazard);
    }

    private double Exposure(double openWaterToShore, double height)
    {
        if (openWaterToShore <= 0) return 1;
        return height <= 0 ? 0 : Math.Exp(-openWaterToShore / (config.SurfReachPerMetre * height));
    }
}
