using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;

namespace RoughSeas.Wind;

public abstract class WindSurface
{
    public abstract double HeightMetres { get; }

    public abstract Push[] Pushes(Airflow air, double halfDensity);
}

public sealed class BluffSurface(double sideSquareMetres, double frontSquareMetres, double drag, double centreAlong, double centreAcross, double heightMetres)
    : WindSurface
{
    public override double HeightMetres => heightMetres;

    public override Push[] Pushes(Airflow air, double halfDensity) =>
    [
        new(new HullPoint(centreAlong, heightMetres, centreAcross), new HullPoint(
            halfDensity * drag * frontSquareMetres * air.Along * Math.Abs(air.Along), 0,
            halfDensity * drag * sideSquareMetres * air.Across * Math.Abs(air.Across)))
    ];
}

public sealed class Sail(SailPolar polar, Rig rig, double fullSquareMetres, double halfSquareMetres, (double Reach, double Span) half, double bluffDrag,
    double clothNewtonsPerSquareMetre)
    : WindSurface
{
    private const int Trims = 36;
    private const int Refinements = 3;

    // The crew trims anew only when the force the polar asks for moves this much: a bound on the work, not physics.
    private const double RetrimRadians = Math.PI / 180;
    private const double RetrimShare = 0.05;

    private RigState? state;
    private (double Heading, double Newtons, (double Reach, double Span) Set, int Tacked)? trimmed;

    public Rig Rig => rig;

    public RigState State => state ??= rig.End.Pose(0, 0);

    public double SquareMetres { get; private set; }

    public (double Reach, double Span) Set { get; private set; }

    public double Depth { get; private set; }

    public double Pressure { get; private set; }

    public bool Aback { get; private set; }

    public override double HeightMetres => State.Centre(SailForm.Whole, 0).Height;

    public void Hoist(int sailPosition) => (SquareMetres, Set) = sailPosition switch
    {
        0 => (0, default),
        1 => (halfSquareMetres, half),
        _ => (fullSquareMetres, SailForm.Whole)
    };

    public bool Draws(Airflow air) => SquareMetres > 0 && air.Speed > 0 && (Backs(air) || polar.At(FromBow(air)) != null);

    private bool Backs(Airflow air) => rig.End.Backs(air);

    private double FromBow(Airflow air) => Math.Acos(Math.Clamp(-rig.End.Ahead * air.Along / air.Speed, -1, 1));

    public void Slack(Airflow air)
    {
        (Pressure, Depth, Aback, trimmed) = (0, 0, false, null);
        var still = (Func<RigState, (HullPoint, HullPoint)>)(posed => (posed.Centre(Set, 0), default));
        state = Settled(still, posed => -Math.Abs(Flow(posed.Form.Normal, air)));
    }

    public RigLoad? Load { get; private set; }

    public override Push[] Pushes(Airflow air, double halfDensity)
    {
        var (centre, force) = Backs(air) ? TakenAback(air, halfDensity) : Drawing(air, halfDensity);
        Load = rig.End.Loads(State, centre, force);
        return Load.Points;
    }

    private (HullPoint At, HullPoint Force) Drawing(Airflow air, double halfDensity)
    {
        var end = rig.End;
        if (polar.At(FromBow(air)) is not var (drive, side)) return default;

        Aback = false;
        var dynamic = halfDensity * air.Speed * air.Speed;
        var force = dynamic * SquareMetres * new HullPoint(end.Ahead * drive, 0, end.Leeward(air) * side);
        var (plan, pressing) = (force.Unit, force.Length);
        var trim = (Math.Atan2(plan.Across, plan.Along), pressing, Set, rig.Tacked);
        if (Retrim(trim))
        {
            state = Settled(posed => (posed.Centre(Set, 0), Math.Sign(Flow(posed.Form.Normal, air)) * pressing * posed.Form.Normal),
                posed => LeeOf(posed) > 0 ? Math.Sign(Flow(posed.Form.Normal, air)) * Flow(posed.Form.Normal, plan) : LeeOf(posed) - 1);
            trimmed = trim;
        }
        Pressure = dynamic * Math.Sqrt(drive * drive + side * side);
        Fill(air);
        return (State.Centre(Set, Depth), force);
    }

    private bool Retrim((double Heading, double Newtons, (double Reach, double Span) Set, int Tacked) trim) =>
        trimmed is not { } last || last.Set != trim.Set || last.Tacked != trim.Tacked
        || Math.Abs(Math.IEEERemainder(trim.Heading - last.Heading, 2 * Math.PI)) > RetrimRadians
        || Math.Abs(trim.Newtons - last.Newtons) > RetrimShare * Math.Max(trim.Newtons, last.Newtons);

    private (HullPoint At, HullPoint Force) TakenAback(Airflow air, double halfDensity)
    {
        Aback = true;
        trimmed = null;
        var end = rig.End;
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var (at, pressing) = Plate(end.Pose(Math.Clamp(State.Yard, first, last), State.Boom), air, halfDensity);
        var yard = (at - end.Origin).Cross(pressing).Dot(end.Axis) < 0 ? first : last;
        state = end.Settle(yard, State.Boom, posed => Plate(posed, air, halfDensity));
        Pressure = halfDensity * bluffDrag * Math.Pow(Flow(State.Form.Normal, air), 2);
        Fill(air);
        return Plate(State, air, halfDensity);
    }

    private RigState Settled(Func<RigState, (HullPoint At, HullPoint Force)> wind, Func<RigState, double> score)
    {
        var end = rig.End;
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var resting = end.Resting;
        var toward = resting == first ? 1 : -1;
        var step = end.Free.Span / Trims;
        var swept = new List<RigState> { end.Settle(resting, 0, wind) };
        for (var k = 1; k <= Trims; k++) swept.Add(end.Track(resting + toward * step * k, swept[^1].Boom, wind));
        var best = swept.Where(end.Clear).DefaultIfEmpty(swept[0]).MaxBy(score)!;
        var finer = Enumerable.Range(-Refinements, 2 * Refinements + 1).Select(k => best.Yard + step * k / (Refinements + 1))
            .Where(yard => yard >= first && yard <= last).Select(yard => end.Track(yard, best.Boom, wind)).Where(end.Clear).Append(best).MaxBy(score)!;
        return finer with { SheetNewtons = finer.SheetNewtons > 0 ? finer.SheetNewtons : 0 };
    }

    private (HullPoint At, HullPoint Force) Plate(RigState posed, Airflow air, double halfDensity)
    {
        var normal = posed.Form.Normal;
        var flow = Flow(normal, air);
        return (posed.Centre(Set, 0), halfDensity * bluffDrag * flow * Math.Abs(flow) * SquareMetres * normal);
    }

    private static double Flow(HullPoint normal, Airflow air) => normal.Along * air.Along + normal.Across * air.Across;

    private static double Flow(HullPoint normal, HullPoint plan)
    {
        var length = Math.Sqrt(normal.Along * normal.Along + normal.Across * normal.Across);
        return length > 0 ? (normal.Along * plan.Along + normal.Across * plan.Across) / length : 0;
    }

    private void Fill(Airflow air) =>
        Depth = Math.Sign(Flow(State.Form.Normal, air)) * Pressure / (Pressure + clothNewtonsPerSquareMetre);

    private double LeeOf(RigState posed)
    {
        return rig.End.LeeOf(posed, Set);
    }

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
