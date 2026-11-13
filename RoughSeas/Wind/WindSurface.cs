using System;
using System.Linq;
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

public sealed class Sail(SailPolar polar, Rig rig, double fullSquareMetres, double halfSquareMetres, (double Reach, double Span) half, double bluffDrag,
    double clothNewtonsPerSquareMetre)
    : WindSurface
{
    public Rig Rig => rig;

    public SailForm Form => rig.End.Form;

    public double SquareMetres { get; private set; }

    public (double Reach, double Span) Set { get; private set; }

    public double Turn { get; private set; }

    public double Depth { get; private set; }

    public double Pressure { get; private set; }

    public bool Aback { get; private set; }

    public override double HeightMetres => Form.Centre(SailForm.Whole, Turn, 0).Height;

    public void Hoist(int sailPosition) => (SquareMetres, Set) = sailPosition switch
    {
        0 => (0, default),
        1 => (halfSquareMetres, half),
        _ => (fullSquareMetres, SailForm.Whole)
    };

    public bool Draws(Airflow air) => SquareMetres > 0 && air.Speed > 0 && (Backs(air) || polar.At(FromBow(air)) != null);

    private bool Backs(Airflow air) => air.Across * rig.End.Leeward < 0;

    private double FromBow(Airflow air) => Math.Acos(Math.Clamp(-rig.End.Ahead * air.Along / air.Speed, -1, 1));

    public void Slack(Airflow air)
    {
        (Pressure, Depth, Aback) = (0, 0, false);
        Turn = Best(turn => -Math.Abs(Plan(Form.Normal(turn)).Along * air.Along + Plan(Form.Normal(turn)).Across * air.Across));
    }

    public override Push Push(Airflow air, double halfDensity)
    {
        var end = rig.End;
        if (Backs(air)) return TakenAback(air, halfDensity);
        if (polar.At(FromBow(air)) is not var (drive, side)) return default;

        Aback = false;
        var dynamic = halfDensity * air.Speed * air.Speed;
        var (along, across) = (end.Ahead * drive, end.Leeward * side);
        Turn = Best(turn => Math.Abs(Plan(Form.Normal(turn)).Along * along + Plan(Form.Normal(turn)).Across * across));
        Pressure = dynamic * Math.Sqrt(drive * drive + side * side);
        Fill(air);
        var force = dynamic * SquareMetres;
        var centre = Form.Centre(Set, Turn, Depth);
        return new Push(force * along, force * across, centre.Along, centre.Across);
    }

    private Push TakenAback(Airflow air, double halfDensity)
    {
        Aback = true;
        var (first, last) = (rig.End.Free.From, rig.End.Free.From + rig.End.Free.Span);
        var (at, pressing) = Plate(air, halfDensity, Math.Clamp(Turn, first, last));
        Turn = (at - Form.Origin).Cross(pressing).Dot(Form.Axis) < 0 ? first : last;
        var normal = Form.Normal(Turn);
        Pressure = halfDensity * bluffDrag * Math.Pow(normal.Along * air.Along + normal.Across * air.Across, 2);
        Fill(air);
        var (centre, force) = Plate(air, halfDensity, Turn);
        return new Push(force.Along, force.Across, centre.Along, centre.Across);
    }

    private (HullPoint At, HullPoint Force) Plate(Airflow air, double halfDensity, double turn)
    {
        var normal = Form.Normal(turn);
        var flow = normal.Along * air.Along + normal.Across * air.Across;
        return (Form.Centre(Set, turn, 0), halfDensity * bluffDrag * flow * Math.Abs(flow) * SquareMetres * normal);
    }

    private void Fill(Airflow air)
    {
        var normal = Form.Normal(Turn);
        Depth = Math.Sign(normal.Along * air.Along + normal.Across * air.Across) * Pressure / (Pressure + clothNewtonsPerSquareMetre);
    }

    private double Best(Func<double, double> score) => rig.End.Turns().MaxBy(score);

    private static (double Along, double Across) Plan(HullPoint normal)
    {
        var length = Math.Sqrt(normal.Along * normal.Along + normal.Across * normal.Across);
        return length > 0 ? (normal.Along / length, normal.Across / length) : default;
    }

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
