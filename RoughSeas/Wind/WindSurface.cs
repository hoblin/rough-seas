using System;

namespace RoughSeas.Wind;

public abstract class WindSurface(double heightMetres)
{
    public double HeightMetres => heightMetres;

    public abstract Push Push(Airflow air, double halfDensity);
}

public sealed class BluffSurface(double sideSquareMetres, double frontSquareMetres, double drag, double centreAlong, double centreAcross, double heightMetres)
    : WindSurface(heightMetres)
{
    public override Push Push(Airflow air, double halfDensity) => new(
        halfDensity * drag * frontSquareMetres * air.Along * Math.Abs(air.Along),
        halfDensity * drag * sideSquareMetres * air.Across * Math.Abs(air.Across),
        centreAlong, centreAcross);
}

public sealed class Sail(SailPolar polar, double fullSquareMetres, double halfSquareMetres, double tackAlong, double tackAcross, double reachMetres, double heightMetres)
    : WindSurface(heightMetres)
{
    public double SquareMetres { get; private set; }

    public void Set(int sailPosition) => SquareMetres = sailPosition switch { 0 => 0, 1 => halfSquareMetres, _ => fullSquareMetres };

    public double Trim { get; private set; }

    public double Pressure { get; private set; }

    public bool Draws(Airflow air) => SquareMetres > 0 && air.Speed > 0 && polar.At(air.FromBow) != null;

    public void Slack() => Pressure = 0;

    public override Push Push(Airflow air, double halfDensity)
    {
        if (polar.At(air.FromBow) is not var (drive, side)) return default;

        var leeward = air.Leeward;
        var dynamic = halfDensity * air.Speed * air.Speed;
        Pressure = dynamic * Math.Sqrt(drive * drive + side * side);
        Trim = leeward * Math.Clamp(Math.PI / 2 - Math.Atan2(side, drive), 0, Math.PI / 2);
        var force = dynamic * SquareMetres;
        return new Push(-force * drive, leeward * force * side, tackAlong + reachMetres * Math.Cos(Trim), tackAcross + reachMetres * Math.Sin(Trim));
    }
}
