using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;

namespace RoughSeas.Wind;

public sealed class Sail(SailPolar polar, IncidencePolar aback, Rig rig, double fullSquareMetres, double halfSquareMetres, (double Reach, double Span) half,
    double clothNewtonsPerSquareMetre)
    : WindSurface
{
    private const int Trims = 36;
    private const int Refinements = 3;
    private const int TiedWithinDecimals = 3;

    // The crew trims anew only when the apparent wind moves this much: a bound on the work, not physics.
    private const double RetrimRadians = Math.PI / 180;
    private const double RetrimShare = 0.05;

    private RigState? state;
    private (double Heading, double Pressure, (double Reach, double Span) Set, int Tacked)? trimmed;

    public Rig Rig => rig;

    public RigState State => state ??= rig.End.Pose(0, 0);

    public double SquareMetres { get; private set; }

    public (double Reach, double Span) Set { get; private set; }

    public double Fill { get; private set; }

    public double Pressure { get; private set; }

    public bool Aback { get; private set; }

    public RigLoad? Load { get; private set; }

    public override double HeightMetres => State.Centre(SquareMetres > 0 ? Set : SailForm.Whole, 0).Height;

    public void Hoist(int sailPosition) => (SquareMetres, Set) = sailPosition switch
    {
        0 => (0, default),
        1 => (halfSquareMetres, half),
        _ => (fullSquareMetres, SailForm.Whole)
    };

    public bool Draws(Airflow air) => SquareMetres > 0 && air.Speed > 0;

    public void Slack() => (Pressure, Fill, Aback, Load) = (0, 0, false, null);

    public override Push[] Pushes(Airflow air, double halfDensity)
    {
        var fromBow = Math.Acos(Math.Clamp(-rig.End.Ahead * air.Along / air.Speed, -1, 1));
        var luffs = polar.Luffs(fromBow);
        var trim = (Math.Atan2(air.Across, air.Along), halfDensity * air.Speed * air.Speed, Set, rig.Tacked);
        if (Retrim(trim))
        {
            state = luffs
                ? Settled(posed => (posed.Centre(Set, 0), default), posed => (LeeOf(posed) > 0, true, 0, -Math.Abs(Incidence(posed, air))))
                : Settled(posed => Pressing(posed, air, halfDensity, fromBow), posed => Score(posed, air, halfDensity, fromBow));
            trimmed = trim;
        }

        var (at, force) = rig.End.Aback(State, air) ? TakenAback(air, halfDensity)
            : luffs ? Luffing(air, halfDensity)
            : Drawing(Force(State, air, halfDensity, fromBow), air);
        Load = rig.End.Loads(State, at, force);
        return Load.Points;
    }

    private (bool Lee, bool Drawing, double Drive, double Side, double Facing) Score(RigState posed, Airflow air, double halfDensity, double fromBow)
    {
        var force = Force(posed, air, halfDensity, fromBow).Force;
        var newtons = halfDensity * air.Speed * air.Speed * SquareMetres;
        return (LeeOf(posed) > 0, !rig.End.Aback(posed, air), Math.Round(rig.End.Ahead * force.Along / newtons, TiedWithinDecimals),
            -Math.Round(Math.Abs(force.Across) / newtons, TiedWithinDecimals), Flow(rig.End.Belly(posed), force));
    }

    private (HullPoint At, HullPoint Force) Pressing(RigState posed, Airflow air, double halfDensity, double fromBow)
    {
        var (at, force) = Force(posed, air, halfDensity, fromBow);
        return rig.End.Aback(posed, air) ? (at, force) : (at, force.Length * rig.End.Belly(posed));
    }

    private (HullPoint At, HullPoint Force) Force(RigState posed, Airflow air, double halfDensity, double fromBow)
    {
        var end = rig.End;
        if (end.Aback(posed, air)) return Plate(posed, air, halfDensity);

        var (drive, side) = polar.At(fromBow, Incidence(posed, air));
        return (posed.Centre(Set, 0), halfDensity * air.Speed * air.Speed * SquareMetres * new HullPoint(end.Ahead * drive, 0, end.Leeward(air) * side));
    }

    private (HullPoint At, HullPoint Force) Drawing((HullPoint At, HullPoint Force) pushed, Airflow air)
    {
        Aback = false;
        Pressure = pushed.Force.Length / SquareMetres;
        Filled(air);
        return (State.Centre(Set, Fill), pushed.Force);
    }

    private (HullPoint At, HullPoint Force) Luffing(Airflow air, double halfDensity)
    {
        (Aback, Pressure, Fill) = (false, 0, 0);
        return (State.Centre(Set, 0), halfDensity * air.Speed * aback.Friction * SquareMetres * new HullPoint(air.Along, 0, air.Across));
    }

    private bool Retrim((double Heading, double Pressure, (double Reach, double Span) Set, int Tacked) trim) =>
        trimmed is not { } last || last.Set != trim.Set || last.Tacked != trim.Tacked
        || Math.Abs(Math.IEEERemainder(trim.Heading - last.Heading, 2 * Math.PI)) > RetrimRadians
        || Math.Abs(trim.Pressure - last.Pressure) > RetrimShare * Math.Max(trim.Pressure, last.Pressure);

    private (HullPoint At, HullPoint Force) TakenAback(Airflow air, double halfDensity)
    {
        Aback = true;
        var end = rig.End;
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var (at, pressing) = Plate(end.Pose(Math.Clamp(State.Yard, first, last), State.Boom), air, halfDensity);
        var yard = (at - end.Origin).Cross(pressing).Dot(end.Axis) < 0 ? first : last;
        state = end.Settle(yard, State.Boom, posed => Plate(posed, air, halfDensity));
        Pressure = halfDensity * air.Speed * air.Speed * aback.Pressing(Math.Asin(Math.Clamp(Flow(State.Form.Normal, air) / air.Speed, -1, 1)));
        Filled(air);
        return Plate(State, air, halfDensity);
    }

    private RigState Settled<TScore>(Func<RigState, (HullPoint At, HullPoint Force)> wind, Func<RigState, TScore> score)
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
        var pressing = aback.Pressing(Math.Asin(Math.Clamp(flow / air.Speed, -1, 1)));
        return (posed.Centre(Set, 0), halfDensity * air.Speed * air.Speed * pressing * Math.Sign(flow) * SquareMetres * normal);
    }

    private double Incidence(RigState posed, Airflow air) => Math.Asin(Math.Clamp(Flow(rig.End.Belly(posed), air) / air.Speed, -1, 1));

    private static double Flow(HullPoint normal, Airflow air) => normal.Along * air.Along + normal.Across * air.Across;

    private static double Flow(HullPoint normal, HullPoint force)
    {
        var lengths = Math.Sqrt((normal.Along * normal.Along + normal.Across * normal.Across) * (force.Along * force.Along + force.Across * force.Across));
        return lengths > 0 ? (normal.Along * force.Along + normal.Across * force.Across) / lengths : 0;
    }

    private void Filled(Airflow air) =>
        Fill = Math.Sign(Flow(State.Form.Normal, air)) * Pressure / (Pressure + clothNewtonsPerSquareMetre);

    private double LeeOf(RigState posed) => rig.End.LeeOf(posed, Set);

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
