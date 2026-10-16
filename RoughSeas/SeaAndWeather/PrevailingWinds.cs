using System;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public class PrevailingWinds
{
    private readonly PrevailingWind[] bands;
    private readonly Func<double, double> latitude;

    public PrevailingWinds(PrevailingWind[] bands, Func<double, double> latitude)
    {
        this.bands = bands.OrderBy(band => band.MaxLatitude).ToArray();
        this.latitude = latitude;
    }

    public double LatitudeDegrees(double z) => latitude(z) * 90;

    public double TowardRadians(double z)
    {
        var degrees = LatitudeDegrees(z);
        var band = bands.FirstOrDefault(b => Math.Abs(degrees) <= b.MaxLatitude) ?? bands[^1];
        var fromDegrees = degrees >= 0 ? band.NorthFromDegrees : band.SouthFromDegrees;
        return (fromDegrees + 180) * GameMath.DEG2RAD_DOUBLE;
    }
}
