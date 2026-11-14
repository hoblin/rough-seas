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

    public int PolewardZ(double z) => Math.Abs(latitude(z + 1)) >= Math.Abs(latitude(z - 1)) ? 1 : -1;

    public double TowardRadians(double z)
    {
        var degrees = LatitudeDegrees(z);
        var band = Band(Math.Abs(degrees));
        var fromDegrees = degrees >= 0 ? band.NorthFromDegrees : band.SouthFromDegrees;
        return (fromDegrees + 180) * GameMath.DEG2RAD_DOUBLE;
    }

    private PrevailingWind Band(double degrees)
    {
        foreach (var band in bands)
        {
            if (degrees <= band.MaxLatitude) return band;
        }
        return bands[^1];
    }
}
