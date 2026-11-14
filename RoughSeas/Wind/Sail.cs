using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;

namespace RoughSeas.Wind;

public sealed class Sail(Wing wing, Rig rig, double fullSquareMetres, double halfSquareMetres, (double Reach, double Span) half,
    double clothNewtonsPerSquareMetre, double camber, CrewConfig crew)
    : WindSurface
{
    private const int Trims = 36;
    private const int Narrowings = 12;
    private const double EdgeOn = 1e-3;
    private const double FollowedRadians = 5 * Math.PI / 180;
    private const double StopRadians = Math.PI / 180;
    private static readonly (double Reach, double Span)[] Outline = [(0, 0), (1, 0), (1, 1), (0, 1)];
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
    private double tick;

    private sealed class Shunt(Rigging from, Rigging to, int toward, RigState start, bool rolled)
    {
        public Rigging From => from;
        public Rigging To => to;
        public int Toward => toward;
        public RigState Start => start;
        public bool Rolled => rolled;
        public RigState End { get; } = rolled ? to.Furled : to.Pose(to.Resting, 0);
        public (double Haul, double Carry) Metres { get; } = to.ShuntMetres(from);
        public double Seconds { get; set; }
    }

    public Rig Rig => rig;

    public RigState State => state ??= Stowed();

    private RigState Stowed()
    {
        rig.Set = shown;
        return rig.End.Furled;
    }

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
        if (tacked == rig.Tacked || shunt != null || Hoisting) return;

        shunt = new Shunt(rig.End, rig.EndAt(tacked), tacked, State, Rolled);
        (goal, trimmed) = (null, null);
    }

    private bool Hoisting => hoistFrom != null;

    public RigShape Shape(long entityId) => new()
    {
        EntityId = entityId, Tacked = rig.Tacked, Yard = State.Yard, Boom = State.Boom, Fill = Fill, Hoisted = hoisted, Reach = shown.Reach,
        Span = shown.Span, Slack = slack, Aback = Aback, Toward = shunt?.Toward ?? -1, StartYard = shunt?.Start.Yard ?? 0, StartBoom = shunt?.Start.Boom ?? 0,
        StartRolled = shunt?.Rolled ?? false, ShuntSeconds = shunt?.Seconds ?? 0, FromYard = hoistFrom?.Yard ?? 0, FromBoom = hoistFrom?.Boom ?? 0,
        Hoisting = hoistFrom != null
    };

    public void Show(RigShape shape)
    {
        rig.Tacked = shape.Tacked;
        (shown, hoisted, slack, Fill, Aback) = ((shape.Reach, shape.Span), shape.Hoisted, shape.Slack, shape.Fill, shape.Aback);
        rig.Set = shown;
        var end = rig.End;
        hoistFrom = shape.Hoisting ? end.Pose(shape.FromYard, shape.FromBoom) : null;
        if (shape.Toward >= 0)
        {
            var from = end;
            shunt = new Shunt(from, rig.EndAt(shape.Toward), shape.Toward, shape.StartRolled ? from.Furled : from.Pose(shape.StartYard, shape.StartBoom), shape.StartRolled)
                { Seconds = shape.ShuntSeconds };
            state = Carried(shunt).State;
            return;
        }

        shunt = null;
        state = hoistFrom != null ? end.Between(end.Furled, hoistFrom, hoisted)
            : Rolled && end.HoistMetres > 0 ? end.Furled
            : end.Slackened(end.Pose(shape.Yard, shape.Boom), slack);
    }

    public bool Draws(Airflow air) => air.Speed > 0 && (SquareMetres > 0 || !Rolled || shunt != null);

    public void Slack(double seconds)
    {
        tick = seconds;
        Advance(seconds);
        (Pressure, Aback, Load) = (0, false, null);
        Bellied(0, crew.HaulMetresPerSecond);
    }

    public override Push[] Pushes(Airflow air, double halfDensity, double seconds)
    {
        tick = seconds;
        if (Advance(seconds))
        {
            var (at, luffing) = Luffing(air, halfDensity);
            Load = new RigLoad([new Push(at, luffing)], 0, 0, []);
            return Load.Points;
        }

        var end = rig.End;
        var wind = (Func<RigState, (HullPoint, HullPoint)>)(posed => Force(posed, air, halfDensity));
        var trim = (Math.Atan2(air.Across, air.Along), halfDensity * air.Speed * air.Speed, shown, rig.Tacked);
        if (Retrim(trim))
        {
            var warm = goal is { Draws: true } last && trimmed is { } was && was.Set == shown && was.Tacked == rig.Tacked
                && Math.Abs(Math.IEEERemainder(trim.Item1 - was.Heading, 2 * Math.PI)) < FollowedRadians ? (last.Yard, last.Boom) : ((double, double)?)null;
            var best = Settled(wind, posed => Score(posed, air, halfDensity), warm);
            if (warm != null && (end.Aback(best, air) || !Holds(best))) best = Settled(wind, posed => Score(posed, air, halfDensity), null);
            (goal, trimmed) = ((best.Yard, best.Boom, !end.Aback(best, air) && Holds(best)), trim);
        }

        var (point, force) = goal is { Draws: true } worked && slack <= 0 ? Trimmed(air, halfDensity, seconds, worked, wind) : Swung(air, halfDensity, seconds);
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
            var (shunted, done) = Carried(carrying);
            state = shunted;
            if (!done) return true;

            rig.Tacked = carrying.Toward;
            slack = Math.Max(0, carrying.To.SheetMetres(State) - carrying.To.SheetMetres(carrying.End));
            state = carrying.To.Slackened(carrying.End, slack);
            (shunt, goal, trimmed) = (null, null, null);
        }

        var end = rig.End;
        var spanned = light * seconds / end.SpanMetres;
        if (end.HoistMetres <= 0) shown = (SquareMetres > 0 ? Set.Reach : shown.Reach, Toward(shown.Span, SquareMetres > 0 ? Set.Span : 0, spanned));
        else if (SquareMetres > 0) shown = (Set.Reach, hoisted <= 0 ? Set.Span : Toward(shown.Span, Set.Span, spanned));
        var reefed = shown != end.Set;
        var (yard, boom) = (State.Yard, State.Boom);
        rig.Set = shown;
        if (reefed && hoistFrom != null) hoistFrom = end.Pose(hoistFrom.Yard, hoistFrom.Boom);
        else if (reefed && (end.HoistMetres <= 0 || hoisted >= 1)) state = end.Slackened(end.Pose(yard, boom), slack);

        var up = SquareMetres > 0 ? 1.0 : 0.0;
        if (end.HoistMetres <= 0) return false;
        if (hoisted == up) return up <= 0;

        hoistFrom ??= up > 0 ? end.Pose(end.Resting, 0) : State;
        hoisted = Toward(hoisted, up, light * seconds / end.HoistMetres);
        var rope = end.SheetMetres(State);
        if (hoisted <= 0) slack = Math.Max(slack, rope - end.SheetMetres(end.Furled));
        state = hoisted <= 0 ? end.Slackened(end.Furled, slack) : end.Between(end.Furled, hoistFrom, hoisted);
        if (hoisted != up) return true;

        (hoistFrom, goal, trimmed) = (null, null, null);
        return up <= 0;
    }

    private (RigState State, bool Done) Carried(Shunt carrying)
    {
        var light = crew.HaulMetresPerSecond;
        var hauled = Math.Min(carrying.Metres.Haul, light * carrying.Seconds);
        var walked = (carrying.Seconds - carrying.Metres.Haul / light) * crew.WalkMetresPerSecond;
        var carried = carrying.Metres.Carry <= 0 ? 1 : Math.Clamp(walked / carrying.Metres.Carry, 0, 1);
        return (carrying.To.Shunted(carrying.From, carrying.Start, carrying.End, hauled, carried), carried >= 1);
    }

    private (HullPoint At, HullPoint Force) Trimmed(Airflow air, double halfDensity, double seconds,
        (double Yard, double Boom, bool Draws) worked, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        Trim((worked.Yard, worked.Boom), wind, seconds);
        if (rig.End.Aback(State, air)) return Backed(air, halfDensity);

        return Drawing(air, halfDensity);
    }

    private (HullPoint At, HullPoint Force) Drawing(Airflow air, double halfDensity)
    {
        Aback = false;
        var force = Force(State, air, halfDensity).Force;
        Pressure = Area() > 0 ? Math.Max(0, force.Dot(rig.End.Belly(State))) / Area() : 0;
        Filled(air);
        return (State.Centre(shown, Fill), force);
    }

    private (HullPoint At, HullPoint Force) Swung(Airflow air, double halfDensity, double seconds)
    {
        var end = rig.End;
        var aback = end.Aback(State, air);
        var pressed = (Func<RigState, (HullPoint, HullPoint)>)(posed => aback ? Plate(posed, air, halfDensity) : Force(posed, air, halfDensity));
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var (at, pressing) = pressed(end.Pose(Math.Clamp(State.Yard, first, last), State.Boom));
        var stop = (at - end.Origin).Cross(pressing).Dot(end.Axis) < 0 ? first : last;
        var hauled = crew.HaulMetresPerSecond * seconds;
        var yard = Toward(Math.Clamp(State.Yard, first, last), stop, hauled / Math.Max(ShortestArmMetres, end.Arm(State)));
        var boom = Toward(State.Boom, end.Track(yard, State.Boom, pressed).Boom, hauled / Math.Max(ShortestArmMetres, end.BoomArm(State)));
        var spanned = end.SheetSpan(State);
        var moved = Stopped(end.Held(yard, boom, pressed), pressed);
        slack = Math.Max(0, slack - Math.Max(0, end.SheetSpan(moved) - spanned));
        Kept(moved);
        return aback ? Backed(air, halfDensity) : Drawing(air, halfDensity);
    }

    private (HullPoint At, HullPoint Force) Backed(Airflow air, double halfDensity)
    {
        Aback = true;
        Pressure = halfDensity * air.Speed * air.Speed * Pressing(State, State.Form.Normal, air);
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
        Kept(Stopped(end.Held(yard, boom, wind), wind));
    }

    private RigState Stopped(RigState moved, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        var end = rig.End;
        return end.Clear(moved) || !end.Clear(State) ? moved : end.Held(State.Yard, State.Boom, wind);
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

    private (bool Held, bool Lee, bool Drawing, double Drive, double Side, double Facing) Score(RigState posed, Airflow air, double halfDensity)
    {
        var force = Force(posed, air, halfDensity).Force;
        var newtons = halfDensity * air.Speed * air.Speed * Area();
        return (Holds(posed), LeeOf(posed) > 0, !rig.End.Aback(posed, air), Math.Round(rig.End.Ahead * force.Along / newtons, TiedWithinDecimals),
            -Math.Round(Math.Abs(force.Across) / newtons, TiedWithinDecimals), Flow(rig.End.Belly(posed), force));
    }

    private (HullPoint At, HullPoint Force) Force(RigState posed, Airflow air, double halfDensity)
    {
        var end = rig.End;
        if (end.Aback(posed, air)) return Plate(posed, air, halfDensity);

        var belly = end.Belly(posed);
        var flow = 1 / air.Speed * new HullPoint(air.Along, 0, air.Across);
        var incidence = Math.Clamp(Math.Asin(Math.Clamp(belly.Dot(flow), -1, 1)), 0, Math.PI / 2);
        var along = flow - flow.Dot(belly) * belly;
        var pressure = halfDensity * air.Speed * air.Speed * Area();
        return (posed.Centre(shown, Fill), pressure * ((incidence > 0 ? Pressing(posed, belly, air) : 0) * belly + wing.Friction(air.Speed, Chord()) * along));
    }

    private (HullPoint At, HullPoint Force) Luffing(Airflow air, double halfDensity)
    {
        (Aback, Pressure) = (false, 0);
        Bellied(0, air.Speed);
        return (State.Centre(Rolled ? SailForm.Whole : shown, Fill), halfDensity * air.Speed * wing.Friction(air.Speed, Chord()) * Area() * new HullPoint(air.Along, 0, air.Across));
    }

    private bool Retrim((double Heading, double Pressure, (double Reach, double Span) Set, int Tacked) trim) =>
        trimmed is not { } last || last.Set != trim.Set || last.Tacked != trim.Tacked
        || Math.Abs(Math.IEEERemainder(trim.Heading - last.Heading, 2 * Math.PI)) > RetrimRadians
        || Math.Abs(trim.Pressure - last.Pressure) > RetrimShare * Math.Max(trim.Pressure, last.Pressure);

    private RigState Settled<TScore>(Func<RigState, (HullPoint At, HullPoint Force)> wind, Func<RigState, TScore> score, (double Yard, double Boom)? from)
        where TScore : IComparable<TScore>
    {
        var end = rig.End;
        var (first, last) = (end.Free.From, end.Free.From + end.Free.Span);
        var step = end.Free.Span / Trims;
        var best = from is { } kept ? end.Track(kept.Yard, kept.Boom, wind) : Swept(wind, score, step);

        RigState At(double yard) => end.Track(yard, best.Boom, wind);
        bool Better(RigState a, RigState b) => end.Clear(a) && (!end.Clear(b) || score(a).CompareTo(score(b)) > 0);
        var (low, high) = (Math.Max(first, best.Yard - step), Math.Min(last, best.Yard + step));
        for (var n = 0; n < Narrowings; n++)
        {
            var (a, b) = (low + (high - low) / 3, high - (high - low) / 3);
            if (Better(At(a), At(b))) high = b;
            else low = a;
        }
        var refined = At((low + high) / 2);
        return Better(refined, best) ? refined : best;
    }

    private RigState Swept<TScore>(Func<RigState, (HullPoint At, HullPoint Force)> wind, Func<RigState, TScore> score, double step)
    {
        var end = rig.End;
        var resting = end.Resting;
        var toward = resting == end.Free.From ? 1 : -1;
        var swept = new List<RigState> { end.Settle(resting, 0, wind) };
        for (var k = 1; k <= Trims; k++) swept.Add(end.Track(resting + toward * step * k, swept[^1].Boom, wind));
        return swept.Where(end.Clear).DefaultIfEmpty(swept[0]).MaxBy(score)!;
    }

    private (HullPoint At, HullPoint Force) Plate(RigState posed, Airflow air, double halfDensity)
    {
        var normal = posed.Form.Normal;
        var flow = Flow(normal, air);
        var pressing = Pressing(posed, normal, air);
        return (posed.Centre(shown, Fill), halfDensity * air.Speed * air.Speed * pressing * Math.Sign(flow) * Area() * normal);
    }

    private bool Holds(RigState posed)
    {
        var free = rig.End.Free;
        return posed.SheetNewtons > 0 || Math.Abs(posed.Yard - free.From) < StopRadians || Math.Abs(posed.Yard - free.From - free.Span) < StopRadians;
    }

    private double Chord() => Math.Sqrt(Area());

    private double Pressing(RigState posed, HullPoint normal, Airflow air)
    {
        var flow = 1 / air.Speed * new HullPoint(air.Along, 0, air.Across);
        var along = flow - flow.Dot(normal) * normal;
        along = along.Length > EdgeOn ? along.Unit : (posed.Form.At(1, 0.5, 0, shown) - posed.Form.At(0, 0.5, 0, shown)).Unit;
        var across = normal.Cross(along);
        var outline = new (double X, double Y)[Outline.Length];
        for (var k = 0; k < Outline.Length; k++)
        {
            var point = posed.Form.At(Outline[k].Reach, Outline[k].Span, 0, shown);
            outline[k] = (point.Dot(along), point.Dot(across));
        }
        return wing.Pressing(Lattice.Seen(outline), Math.Asin(Math.Clamp(flow.Dot(normal), -1, 1)));
    }

    private static double Flow(HullPoint normal, Airflow air) => normal.Along * air.Along + normal.Across * air.Across;

    private static double Flow(HullPoint normal, HullPoint force)
    {
        var lengths = Math.Sqrt((normal.Along * normal.Along + normal.Across * normal.Across) * (force.Along * force.Along + force.Across * force.Across));
        return lengths > 0 ? (normal.Along * force.Along + normal.Across * force.Across) / lengths : 0;
    }

    private void Filled(Airflow air) => Bellied(Math.Sign(Flow(State.Form.Normal, air)) * Pressure / (Pressure + clothNewtonsPerSquareMetre), air.Speed);

    private void Bellied(double fill, double metresPerSecond) => Fill = Toward(Fill, fill, metresPerSecond * tick / (camber * rig.End.SpanMetres));

    private double LeeOf(RigState posed) => rig.End.LeeOf(posed, shown);

    public static double ClothNewtons(double kilogramsPerSquareMetre) => kilogramsPerSquareMetre * WaveTheory.Gravity;
}
