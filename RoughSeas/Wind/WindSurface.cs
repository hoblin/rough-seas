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

public sealed class Sail(SailPolar polar, SailForm form, double fullSquareMetres, double halfSquareMetres, (double Reach, double Span) half, double leastTrim,
    double clothNewtonsPerSquareMetre)
    : WindSurface
{
    public SailForm Form => form;

    public double SquareMetres { get; private set; }

    public (double Reach, double Span) Set { get; private set; }

    public double Turn { get; private set; }

    public double Depth { get; private set; }

    public double Pressure { get; private set; }

    public override double HeightMetres => form.Centre(SailForm.Whole, 0, 0).Height;

    public void Hoist(int sailPosition) => (SquareMetres, Set) = sailPosition switch
    {
        0 => (0, default),
        1 => (halfSquareMetres, half),
        _ => (fullSquareMetres, SailForm.Whole)
    };

    public bool Draws(Airflow air) => SquareMetres > 0 && air.Speed > 0 && polar.At(air.FromBow) != null;

    public void Slack(Airflow air)
    {
        Pressure = Depth = 0;
        Turn = Turned(air.Leeward, Math.Clamp(air.FromBow, leastTrim, Math.PI / 2));
    }

    public override Push Push(Airflow air, double halfDensity)
    {
        if (polar.At(air.FromBow) is not var (drive, side)) return default;

        var leeward = air.Leeward;
        var dynamic = halfDensity * air.Speed * air.Speed;
        Pressure = dynamic * Math.Sqrt(drive * drive + side * side);
        Turn = Turned(leeward, Math.Clamp(Math.PI / 2 - Math.Atan2(side, drive), leastTrim, Math.PI / 2));
        var (normalAlong, normalAcross) = form.Normal(Turn);
        Depth = Math.Sign(normalAlong * air.Along + normalAcross * air.Across) * Pressure / (Pressure + clothNewtonsPerSquareMetre);
        var force = dynamic * SquareMetres;
        var centre = form.Centre(Set, Turn, Depth);
        return new Push(-force * drive, leeward * force * side, centre.Along, centre.Across);
    }

    private double Turned(int leeward, double chord) => leeward * (chord - form.RestChord);

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
