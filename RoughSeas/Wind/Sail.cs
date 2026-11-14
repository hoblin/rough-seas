using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;

namespace RoughSeas.Wind;

public sealed class Sail(SailPolar polar, IncidencePolar aback, Rig rig, double fullSquareMetres, double halfSquareMetres, (double Reach, double Span) half,
    double clothNewtonsPerSquareMetre, CrewConfig crew)
    : WindSurface
{
    private const int Trims = 36;
    private const int Refinements = 3;
    private const int TiedWithinDecimals = 3;
    private const double ShortestArmMetres = 0.1;

    // The crew trims anew only when the apparent wind moves this much: a bound on the work, not physics.
    private const double RetrimRadians = Math.PI / 180;
    private const double RetrimShare = 0.05;

    private RigState? state;
    private (double Yard, double Boom, bool Draws)? goal;
    private (double Heading, double Pressure, (double Reach, double Span) Set, int Tacked)? trimmed;
    private Shunt? shunt;
    private double hoisted;
    private RigState? hoistFrom;
    private (double Reach, double Span) shown = (1, 0);
    private double slack;

    private sealed class Shunt(Rigging from, Rigging to, int toward, RigState start, RigState end, (double Haul, double Carry) metres)
    {
        public Rigging From => from;
        public Rigging To => to;
        public int Toward => toward;
        public RigState Start => start;
        public RigState End => end;
        public (double Haul, double Carry) Metres => metres;
        public double Seconds { get; set; }
    }

    public Rig Rig => rig;

    public RigState State => state ??= rig.End.Furled;

    public double SquareMetres { get; private set; }

    public (double Reach, double Span) Set { get; private set; }

    public (double Reach, double Span) Shown => shown;

    public bool Shunting => shunt != null;

    public bool Rolled => rig.End.HoistMetres > 0 ? hoisted <= 0 : shown.Span <= 0;

    public double Fill { get; private set; }

    public double Pressure { get; private set; }

    public bool Aback { get; private set; }

    public RigLoad? Load { get; private set; }

    public override double HeightMetres => State.Centre(Rolled ? SailForm.Whole : shown, 0).Height;

    public void Hoist(int sailPosition, int tacked)
    {
        (SquareMetres, Set) = sailPosition switch
        {
            0 => (0, default),
            1 => (halfSquareMetres, half),
            _ => (fullSquareMetres, SailForm.Whole)
        };
        if (tacked == rig.Tacked || shunt != null) return;

        var (from, to) = (rig.End, rig.EndAt(tacked));
        shunt = new Shunt(from, to, tacked, State, Rolled ? to.Furled : to.Pose(to.Resting, 0), to.ShuntMetres(from));
        (goal, trimmed) = (null, null);
    }

    public bool Draws(Airflow air) => air.Speed > 0 && (SquareMetres > 0 || !Rolled || shunt != null);

    public void Slack(double seconds)
    {
        Advance(seconds);
        (Pressure, Fill, Aback, Load) = (0, 0, false, null);
    }

    public override Push[] Pushes(Airflow air, double halfDensity, double seconds)
    {
        if (Advance(seconds))
        {
            var (at, luffing) = Luffing(air, halfDensity);
            Load = new RigLoad([new Push(at, luffing)], 0, 0, []);
            return Load.Points;
        }

        var end = rig.End;
        var fromBow = Math.Acos(Math.Clamp(-end.Ahead * air.Along / air.Speed, -1, 1));
        var luffs = polar.Luffs(fromBow);
        var wind = luffs ? posed => (posed.Centre(shown, 0), default) : (Func<RigState, (HullPoint, HullPoint)>)(posed => Pressing(posed, air, halfDensity, fromBow));
        var trim = (Math.Atan2(air.Across, air.Along), halfDensity * air.Speed * air.Speed, shown, rig.Tacked);
        if (Retrim(trim))
        {
            var best = luffs
                ? Settled(wind, posed => (LeeOf(posed) > 0, true, 0, -Math.Abs(Incidence(posed, air))))
                : Settled(wind, posed => Score(posed, air, halfDensity, fromBow));
            (goal, trimmed) = ((best.Yard, best.Boom, !end.Aback(best, air)), trim);
        }

        var (point, force) = goal is { Draws: true } worked ? Trimmed(air, halfDensity, seconds, fromBow, luffs, worked, wind) : TakenAback(air, halfDensity, seconds);
        Load = end.Loads(State, point, force);
        return Load.Points;
    }

    private bool Advance(double seconds)
    {
        var light = crew.HaulMetresPerSecond;
        slack = Math.Max(0, slack - light * seconds);
        if (shunt is { } carrying)
        {
            carrying.Seconds += seconds;
            var hauled = Math.Min(carrying.Metres.Haul, light * carrying.Seconds);
            var walked = (carrying.Seconds - carrying.Metres.Haul / light) * crew.WalkMetresPerSecond;
            var carried = carrying.Metres.Carry <= 0 ? 1 : Math.Clamp(walked / carrying.Metres.Carry, 0, 1);
            state = carrying.To.Shunted(carrying.From, carrying.Start, carrying.End, hauled, carried);
            if (carried < 1) return true;

            rig.Tacked = carrying.Toward;
            slack = Math.Max(0, carrying.To.SheetMetres(State) - carrying.To.SheetMetres(carrying.End));
            state = carrying.End;
            (shunt, goal, trimmed) = (null, null, null);
        }

        var end = rig.End;
        var spanned = light * seconds / end.SpanMetres;
        if (end.HoistMetres <= 0) shown = (SquareMetres > 0 ? Set.Reach : shown.Reach, Toward(shown.Span, SquareMetres > 0 ? Set.Span : 0, spanned));
        else if (SquareMetres > 0) shown = (Set.Reach, hoisted <= 0 ? Set.Span : Toward(shown.Span, Set.Span, spanned));
        rig.Set = shown;

        var up = SquareMetres > 0 ? 1.0 : 0.0;
        if (end.HoistMetres <= 0 || hoisted == up) return false;

        hoistFrom ??= up > 0 ? end.Pose(end.Resting, 0) : State;
        hoisted = Toward(hoisted, up, light * seconds / end.HoistMetres);
        state = hoisted <= 0 ? end.Furled : end.Between(end.Furled, hoistFrom, hoisted);
        if (hoisted != up) return true;

        (hoistFrom, goal, trimmed) = (null, null, null);
        return up <= 0;
    }

    private (HullPoint At, HullPoint Force) Trimmed(Airflow air, double halfDensity, double seconds, double fromBow, bool luffs,
        (double Yard, double Boom, bool Draws) worked, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        Trim((worked.Yard, worked.Boom), wind, seconds);
        if (rig.End.Aback(State, air)) return Backed(air, halfDensity);

        Aback = false;
        if (luffs) return Luffing(air, halfDensity);

        var (at, force) = Force(State, air, halfDensity, fromBow);
        Pressure = force.Length / Area();
        Filled(air);
        return (State.Centre(shown, Fill), force);
    }

    private (HullPoint At, HullPoint Force) TakenAback(Airflow air, double halfDensity, double seconds)
    {
        var end = rig.End;
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var (at, pressing) = Plate(end.Pose(Math.Clamp(State.Yard, first, last), State.Boom), air, halfDensity);
        var stop = (at - end.Origin).Cross(pressing).Dot(end.Axis) < 0 ? first : last;
        var yard = Toward(Math.Clamp(State.Yard, first, last), stop, crew.HaulMetresPerSecond * seconds / Math.Max(ShortestArmMetres, end.Arm(State)));
        Kept(end.Track(yard, State.Boom, posed => Plate(posed, air, halfDensity)));
        return Backed(air, halfDensity);
    }

    private (HullPoint At, HullPoint Force) Backed(Airflow air, double halfDensity)
    {
        Aback = true;
        Pressure = halfDensity * air.Speed * air.Speed * aback.Pressing(Math.Asin(Math.Clamp(Flow(State.Form.Normal, air) / air.Speed, -1, 1)));
        Filled(air);
        return Plate(State, air, halfDensity);
    }

    private void Trim((double Yard, double Boom) toward, Func<RigState, (HullPoint At, HullPoint Force)> wind, double seconds)
    {
        var end = rig.End;
        var now = State;
        var hauled = crew.HaulMetresPerSecond * seconds;
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var yard = Toward(Math.Clamp(now.Yard, first, last), toward.Yard, hauled / Math.Max(ShortestArmMetres, end.Arm(now)));
        var boom = Toward(now.Boom, toward.Boom, hauled / Math.Max(ShortestArmMetres, end.BoomArm(now)));
        Kept(end.Held(yard, boom, wind));
    }

    private void Kept(RigState moved) =>
        state = rig.End.Slackened(moved with { SheetNewtons = moved.SheetNewtons > 0 ? moved.SheetNewtons : 0 }, slack);

    private static double Toward(double from, double to, double step) => from < to ? Math.Min(to, from + step) : Math.Max(to, from - step);

    private double Area()
    {
        var (span, halfSpan) = (shown.Span, half.Span);
        var area = halfSpan >= 1 ? fullSquareMetres * span
            : span <= halfSpan ? halfSquareMetres * span / halfSpan
            : halfSquareMetres + (fullSquareMetres - halfSquareMetres) * (span - halfSpan) / (1 - halfSpan);
        return rig.End.HoistMetres > 0 ? area * hoisted : area;
    }

    private (bool Lee, bool Drawing, double Drive, double Side, double Facing) Score(RigState posed, Airflow air, double halfDensity, double fromBow)
    {
        var force = Force(posed, air, halfDensity, fromBow).Force;
        var newtons = halfDensity * air.Speed * air.Speed * Area();
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
        return (posed.Centre(shown, 0), halfDensity * air.Speed * air.Speed * Area() * new HullPoint(end.Ahead * drive, 0, end.Leeward(air) * side));
    }

    private (HullPoint At, HullPoint Force) Luffing(Airflow air, double halfDensity)
    {
        (Aback, Pressure, Fill) = (false, 0, 0);
        return (State.Centre(Rolled ? SailForm.Whole : shown, 0), halfDensity * air.Speed * aback.Friction * Area() * new HullPoint(air.Along, 0, air.Across));
    }

    private bool Retrim((double Heading, double Pressure, (double Reach, double Span) Set, int Tacked) trim) =>
        trimmed is not { } last || last.Set != trim.Set || last.Tacked != trim.Tacked
        || Math.Abs(Math.IEEERemainder(trim.Heading - last.Heading, 2 * Math.PI)) > RetrimRadians
        || Math.Abs(trim.Pressure - last.Pressure) > RetrimShare * Math.Max(trim.Pressure, last.Pressure);

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
        return Enumerable.Range(-Refinements, 2 * Refinements + 1).Select(k => best.Yard + step * k / (Refinements + 1))
            .Where(yard => yard >= first && yard <= last).Select(yard => end.Track(yard, best.Boom, wind)).Where(end.Clear).Append(best).MaxBy(score)!;
    }

    private (HullPoint At, HullPoint Force) Plate(RigState posed, Airflow air, double halfDensity)
    {
        var normal = posed.Form.Normal;
        var flow = Flow(normal, air);
        var pressing = aback.Pressing(Math.Asin(Math.Clamp(flow / air.Speed, -1, 1)));
        return (posed.Centre(shown, 0), halfDensity * air.Speed * air.Speed * pressing * Math.Sign(flow) * Area() * normal);
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

    private double LeeOf(RigState posed) => rig.End.LeeOf(posed, shown);

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
