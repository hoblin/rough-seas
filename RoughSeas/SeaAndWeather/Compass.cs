using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

public static class Compass
{
    public static (double X, double Z) Toward(double radians) => (Math.Sin(radians), -Math.Cos(radians));

    public static double Radians(double towardX, double towardZ) => Math.Atan2(towardX, -towardZ);

    public static double FromDegrees(double towardX, double towardZ)
    {
        var toward = Math.Atan2(towardX, -towardZ) / GameMath.DEG2RAD_DOUBLE;
        return ((toward + 180) % 360 + 360) % 360;
    }

    public static double TowardDegrees(double towardX, double towardZ) => (FromDegrees(towardX, towardZ) + 180) % 360;
}
