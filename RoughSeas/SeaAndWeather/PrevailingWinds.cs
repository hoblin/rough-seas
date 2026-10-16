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

    public static (double X, double Z) Toward(double radians) => (Math.Sin(radians), -Math.Cos(radians));

    public static double FromDegrees(double windX, double windZ)
    {
        var toward = Math.Atan2(windX, -windZ) / GameMath.DEG2RAD_DOUBLE;
        return ((toward + 180) % 360 + 360) % 360;
    }
}
