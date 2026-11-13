using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed class Rig
{
    private readonly RigEnd[] ends;

    public Rig(RigEnd[] ends) => this.ends = ends;

    public static Rig? Of(JsonObject rig, double camber, double clothKilograms)
    {
        if (!rig.Exists) return null;

        var widths = new RigWidths(rig["sparMetres"].AsDouble(), rig["poleMetres"].AsDouble(), rig["shoreMetres"].AsDouble(), rig["lineMetres"].AsDouble(),
            rig["rollMetres"].AsDouble());
        return new Rig(rig["ends"].AsArray().Select((end, n) => new RigEnd(RigPose.Of(end["hoisted"]), RigPose.Of(end["lowered"]), n == 0 ? -1 : 1, camber,
            (rig["yardKilograms"].AsDouble(), rig["boomKilograms"].AsDouble(), clothKilograms), widths)).ToArray());
    }

    public int Tacked { get; set; }

    public RigEnd End => ends[Tacked];

    public RigEnd EndAt(int tacked) => ends[tacked];

    public int Ends => ends.Length;
}

public sealed record RigWidths(double Spar, double Pole, double Shore, double Line, double Roll);

public sealed record RigLoad(Push[] Points, double SheetNewtons, double HalyardNewtons, (string Line, double Newtons)[] Stays);

public sealed record RigState(double Yard, double Boom, HullPoint Peak, HullPoint Clew, HullPoint Bend, HullPoint Bridle, SailForm Form, double SheetNewtons)
{
    public HullPoint Centre((double Reach, double Span) set, double depth) => Form.Centre(set, depth);
}

public sealed class RigEnd
{
    private const int Steps = 720;
    private const int Samples = 64;
    private const int Swings = 16;
    private const int Halvings = 10;
    private const double Holding = 1e-3;
    private static readonly HullPoint Down = new(0, -1, 0);

    private readonly HullPoint axis;
    private readonly (double Yard, double Boom, double Cloth) kilograms;
    private readonly SailForm rest;
    private readonly (HullPoint From, HullPoint To, double Width)[] standing;

    public RigWidths Widths { get; }

    public RigEnd(RigPose hoisted, RigPose lowered, int ahead, double camber, (double Yard, double Boom, double Cloth) kilograms, RigWidths widths)
    {
        (Hoisted, Lowered, Ahead, this.kilograms, Widths) = (hoisted, lowered, ahead, kilograms, widths);
        standing = [(hoisted.Heel, hoisted.Masthead, widths.Pole), (hoisted.Masthead, hoisted.SternPin, widths.Line),
            (hoisted.Masthead, hoisted.ShroudFoot, widths.Line), (hoisted.ShoreFoot, hoisted.ShoreTop, widths.Shore)];
        axis = (hoisted.Masthead - hoisted.Tack).Unit;
        rest = new SailForm(hoisted.Tack, hoisted.Tack, hoisted.Peak, hoisted.Clew, camber);
        Leeward = -Math.Sign(hoisted.ShroudFoot.Across);
        var lee = Enumerable.Range(0, Steps).Select(k => 2 * Math.PI * k / Steps - Math.PI).MaxBy(turn => Leeward * Turned(hoisted.Clew, turn).Across);
        var (from, span) = FreeTurns(lee, Blocked().ToArray());
        var step = 2 * Math.PI / Steps;
        while (span > 0 && !Clear(Pose(from, 0))) (from, span) = (from + step, span - step);
        while (span > 0 && !Clear(Pose(from + span, 0))) span -= step;
        Free = (from, span);
    }

    public int Leeward { get; }

    public RigPose Hoisted { get; }

    public RigPose Lowered { get; }

    public int Ahead { get; }

    public HullPoint Tack => Hoisted.Tack;

    public HullPoint Axis => axis;

    public (double From, double Span) Free { get; }

    public HullPoint Turned(HullPoint point, double turn) => point.Turned(Hoisted.Tack, axis, turn);

    public IEnumerable<double> Turns(int steps = Steps)
    {
        for (var k = 0; k <= steps; k++) yield return Free.From + Free.Span * k / steps;
    }

    public RigState Pose(double yard, double boom, double sheetNewtons = 0)
    {
        var tack = Hoisted.Tack;
        var peak = Turned(Hoisted.Peak, yard);
        var along = (peak - tack).Unit;
        var clew = Turned(Hoisted.Clew, yard).Turned(tack, along, boom);
        var bridle = Turned(Hoisted.Bridle, yard).Turned(tack, along, boom);
        return new RigState(yard, boom, peak, clew, Turned(Hoisted.HalyardBend, yard), bridle, rest.Posed(tack, tack, peak, clew), sheetNewtons);
    }

    public RigState Track(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        double Unbalanced(double boom) => Swung(Pose(yard, boom), wind).Moment;
        var step = 2 * Math.PI / Swings;
        var (low, high) = (near, near);
        var (atLow, atHigh) = (Unbalanced(near), Unbalanced(near));
        for (var k = 0; k < Swings && atLow <= 0 == atHigh <= 0; k++)
        {
            if (atLow <= 0) (low, atLow) = (low - step, Unbalanced(low - step));
            if (atHigh > 0) (high, atHigh) = (high + step, Unbalanced(high + step));
        }
        if (!(atLow > 0 && atHigh <= 0)) return Settle(yard, near, wind);

        for (var n = 0; n < Halvings; n++)
        {
            var middle = (low + high) / 2;
            if (Unbalanced(middle) > 0) low = middle;
            else high = middle;
        }
        var posed = Pose(yard, (low + high) / 2);
        return posed with { SheetNewtons = Swung(posed, wind).Sheet };
    }

    public RigState Settle(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        double Unbalanced(double boom) => Swung(Pose(yard, boom), wind).Moment;
        var swings = Enumerable.Range(0, Swings + 1).Select(k => -Math.PI + 2 * Math.PI * k / Swings).ToArray();
        var moments = swings.Select(Unbalanced).ToArray();
        var stable = Enumerable.Range(0, Swings).Where(k => moments[k] > 0 && moments[k + 1] <= 0).Select(k =>
        {
            var (low, high) = (swings[k], swings[k + 1]);
            for (var n = 0; n < Halvings; n++)
            {
                var middle = (low + high) / 2;
                if (Unbalanced(middle) > 0) low = middle;
                else high = middle;
            }
            return Pose(yard, (low + high) / 2);
        }).ToArray();
        double Away(double boom) => Math.Abs(Math.IEEERemainder(boom - near, 2 * Math.PI));
        var posed = stable.Length > 0 ? stable.MinBy(state => Away(state.Boom))! : Pose(yard, swings[Array.IndexOf(moments, moments.MinBy(Math.Abs))]);
        return posed with { SheetNewtons = Swung(posed, wind).Sheet };
    }

    private (double Moment, double Sheet) Swung(RigState state, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        var tack = Hoisted.Tack;
        var (at, force) = wind(state);
        var cloth = at - tack;
        var weighed = cloth.Cross(force + kilograms.Cloth * Gravity) + (0.5 * (state.Peak - tack)).Cross(kilograms.Yard * Gravity)
            + (0.5 * (state.Clew - tack)).Cross(kilograms.Boom * Gravity);
        var sheet = (Hoisted.Lead - state.Bridle).Unit;
        var lever = (state.Bridle - tack).Cross(sheet);
        var reach = lever.Dot(axis);
        var pulled = Math.Abs(reach) > Holding ? -weighed.Dot(axis) / reach : double.NaN;
        return ((weighed + (pulled > 0 ? pulled : 0) * lever).Dot((state.Peak - tack).Unit), pulled);
    }

    private static HullPoint Gravity => WaveTheory.Gravity * Down;

    public bool Clear(RigState state) =>
        standing.All(stay => !Crosses(stay.From, stay.To, Hoisted.Tack, state.Peak, state.Clew)
            && Apart(Hoisted.Tack, state.Peak, stay.From, stay.To) > (Widths.Spar + stay.Width) / 2
            && Apart(Hoisted.Tack, state.Clew, stay.From, stay.To) > (Widths.Spar + stay.Width) / 2);

    private static double Apart(HullPoint a0, HullPoint a1, HullPoint b0, HullPoint b1)
    {
        var (u, v, w) = (a1 - a0, b1 - b0, a0 - b0);
        var (uu, uv, vv, uw, vw) = (u.Dot(u), u.Dot(v), v.Dot(v), u.Dot(w), v.Dot(w));
        var det = uu * vv - uv * uv;
        var s = det > 1e-12 ? Math.Clamp((uv * vw - vv * uw) / det, 0, 1) : 0;
        var t = Math.Clamp((uv * s + vw) / vv, 0, 1);
        s = Math.Clamp((uv * t - uw) / uu, 0, 1);
        return (w + s * u - t * v).Length;
    }

    public RigLoad Loads(RigState state, HullPoint at, HullPoint force)
    {
        var (tack, pose) = (Hoisted.Tack, Hoisted);
        var sheet = (pose.Lead - state.Bridle).Unit;
        var sheeted = state.SheetNewtons;
        var weights = new (HullPoint At, HullPoint Force)[]
        {
            (tack + 0.5 * (state.Peak - tack), kilograms.Yard * Gravity),
            (tack + 0.5 * (state.Clew - tack), kilograms.Boom * Gravity),
            (at, kilograms.Cloth * Gravity)
        };
        var weight = weights.Aggregate(default(HullPoint), (sum, w) => sum + w.Force);
        var halyard = (pose.Masthead - state.Bend).Unit;
        var lifted = (state.Bend - tack).Cross(halyard);
        var unheld = weights.Aggregate((at - tack).Cross(force), (sum, w) => sum + (w.At - tack).Cross(w.Force)) + (state.Bridle - tack).Cross(sheeted * sheet);
        var hoisted = Math.Max(0, -unheld.Dot(lifted) / lifted.Dot(lifted));
        var stepped = -1 * (force + weight + sheeted * sheet + hoisted * halyard);
        var head = hoisted * ((pose.Heel - pose.Masthead).Unit - halyard);
        var masthead = Stayed(pose, head);
        return new RigLoad(
            [
                new(tack, -1 * stepped, unheld + hoisted * lifted),
                new(pose.Lead, -sheeted * sheet),
                new(pose.Heel, hoisted * (pose.Masthead - pose.Heel).Unit),
                ..masthead.Loads
            ],
            sheeted, hoisted, masthead.Tensions);
    }

    public string Describe(RigState state)
    {
        var pose = Hoisted;
        string Spar(HullPoint end)
        {
            var run = end - pose.Tack;
            var level = Math.Sqrt(run.Along * run.Along + run.Across * run.Across);
            var offBow = Math.Atan2(Math.Abs(run.Across), Ahead * run.Along) * 180 / Math.PI;
            return $"{Math.Atan2(run.Height, level) * 180 / Math.PI:F0}° up, {offBow:F0}° off her bow to {(run.Across >= 0 ? "starboard" : "port")}";
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "yard turned {0:F0}° about the tack-masthead line, boom {1:F0}° about the yard; yard {2}, boom {3}; halyard {4:F2} m, sheet {5:F2} m, forestay {6:F2} m, backstay {7:F2} m, shroud {8:F2} m",
            state.Yard * 180 / Math.PI, state.Boom * 180 / Math.PI, Spar(state.Peak), Spar(state.Clew), (pose.Masthead - state.Bend).Length,
            (pose.Lead - state.Bridle).Length, (pose.Masthead - pose.Tack).Length, (pose.Masthead - pose.SternPin).Length, (pose.Masthead - pose.ShroudFoot).Length);
    }

    private static (Push[] Loads, (string Line, double Newtons)[] Tensions) Stayed(RigPose pose, HullPoint head)
    {
        var mast = (pose.Masthead - pose.Heel).Unit;
        var (shroud, backstay, forestay) = ((pose.ShroudFoot - pose.Masthead).Unit, (pose.SternPin - pose.Masthead).Unit, (pose.Tack - pose.Masthead).Unit);
        foreach (var (name, stay, foot) in new[] { ("backstay", backstay, pose.SternPin), ("forestay", forestay, pose.Tack) })
        {
            if (Solve(mast, shroud, stay, -1 * head) is not var (strut, held, pulled) || held < 0 || pulled < 0) continue;
            return ([new(pose.Heel, -strut * mast), new(pose.ShroudFoot, -held * shroud), new(foot, -pulled * stay)],
                [("mast", strut), ("shroud", held), (name, pulled)]);
        }
        return ([new(pose.Heel, head, (pose.Masthead - pose.Heel).Cross(head))], [("mast bending", head.Length)]);
    }

    private static (double, double, double)? Solve(HullPoint a, HullPoint b, HullPoint c, HullPoint sum)
    {
        var det = a.Dot(b.Cross(c));
        if (Math.Abs(det) < 1e-9) return null;
        return (sum.Dot(b.Cross(c)) / det, a.Dot(sum.Cross(c)) / det, a.Dot(b.Cross(sum)) / det);
    }

    private static bool Crosses(HullPoint from, HullPoint to, HullPoint a, HullPoint b, HullPoint c)
    {
        var (edge1, edge2, direction) = (b - a, c - a, to - from);
        var p = direction.Cross(edge2);
        var det = edge1.Dot(p);
        if (Math.Abs(det) < 1e-12) return false;

        var s = from - a;
        var u = s.Dot(p) / det;
        var q = s.Cross(edge1);
        var v = direction.Dot(q) / det;
        var t = edge2.Dot(q) / det;
        return u >= 0 && v >= 0 && u + v <= 1 && t > 0 && t < 1;
    }

    private (double Along, double Out, double Turn) AboutAxis(HullPoint point)
    {
        var tack = Hoisted.Tack;
        var offset = point - tack;
        var along = offset.Dot(axis);
        var radial = offset - along * axis;
        var first = (Hoisted.Clew - tack - (Hoisted.Clew - tack).Dot(axis) * axis).Unit;
        return (along, radial.Length, Math.Atan2(radial.Dot(axis.Cross(first)), radial.Dot(first)));
    }

    private IEnumerable<double> Blocked()
    {
        var (peak, clew) = (AboutAxis(Hoisted.Peak), AboutAxis(Hoisted.Clew));
        foreach (var (from, to) in Hoisted.Standing)
        for (var k = 0; k <= Samples; k++)
        {
            var (along, distance, turn) = AboutAxis(from + (double)k / Samples * (to - from));
            if (InClaw(along, distance, (peak.Along, peak.Out), (clew.Along, clew.Out))) yield return turn;
        }
    }

    private static bool InClaw(double along, double distance, (double Along, double Out) peak, (double Along, double Out) clew)
    {
        var det = peak.Along * clew.Out - peak.Out * clew.Along;
        var u = (along * clew.Out - distance * clew.Along) / det;
        var v = (peak.Along * distance - peak.Out * along) / det;
        return u >= 0 && v >= 0 && u + v <= 1;
    }

    private static (double From, double Span) FreeTurns(double lee, double[] blocked)
    {
        if (blocked.Length == 0) return (lee - Math.PI, 2 * Math.PI);

        double Past(double turn) => (turn - lee + 4 * Math.PI) % (2 * Math.PI);
        var after = blocked.Min(Past);
        var before = blocked.Max(Past) - 2 * Math.PI;
        return (lee + before, after - before);
    }
}
