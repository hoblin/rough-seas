using System;
using RoughSeas.Water;

namespace RoughSeas.Wind;

public abstract class WindSurface
{
    public abstract double HeightMetres { get; }

    public abstract Push Push(Airflow air, double halfDensity);
}

public sealed class BluffSurface(double sideSquareMetres, double frontSquareMetres, double drag, double centreAlong, double centreAcross, double heightMetres)
    : WindSurface
{
    public override double HeightMetres => heightMetres;

    public override Push Push(Airflow air, double halfDensity) => new(
        halfDensity * drag * frontSquareMetres * air.Along * Math.Abs(air.Along),
        halfDensity * drag * sideSquareMetres * air.Across * Math.Abs(air.Across),
        centreAlong, centreAcross);
}

public sealed class Sail(SailPolar polar, SailForm form, double fullSquareMetres, double halfSquareMetres, double leastTrim, double clothNewtonsPerSquareMetre)
    : WindSurface
{
    public SailForm Form => form;

    public double SquareMetres { get; private set; }

    public double Reef => fullSquareMetres > 0 ? Math.Sqrt(SquareMetres / fullSquareMetres) : 0;

    public double Trim { get; private set; }

    public double Fill { get; private set; }

    public double Pressure { get; private set; }

    public override double HeightMetres => form.Centre(1, 0, 0).Height;

    public void Set(int sailPosition) => SquareMetres = sailPosition switch { 0 => 0, 1 => halfSquareMetres, _ => fullSquareMetres };

    public bool Draws(Airflow air) => SquareMetres > 0 && air.Speed > 0 && polar.At(air.FromBow) != null;

    public void Slack(Airflow air)
    {
        Pressure = Fill = 0;
        Trim = air.Leeward * Math.Clamp(air.FromBow, leastTrim, Math.PI / 2);
    }

    public override Push Push(Airflow air, double halfDensity)
    {
        if (polar.At(air.FromBow) is not var (drive, side)) return default;

        var leeward = air.Leeward;
        var dynamic = halfDensity * air.Speed * air.Speed;
        Pressure = dynamic * Math.Sqrt(drive * drive + side * side);
        Fill = leeward * Pressure / (Pressure + clothNewtonsPerSquareMetre);
        Trim = leeward * Math.Clamp(Math.PI / 2 - Math.Atan2(side, drive), leastTrim, Math.PI / 2);
        var force = dynamic * SquareMetres;
        var centre = form.Centre(Reef, Trim, Fill);
        return new Push(-force * drive, leeward * force * side, centre.Along, centre.Across);
    }

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
