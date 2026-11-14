using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed record ClawState(double Yard, double Boom, HullPoint Peak, HullPoint Clew, HullPoint Bend, HullPoint Bridle, SailForm Form, double SheetNewtons,
    ClawPose Rig, ClawLines Lines)
    : RigState(Yard, Boom, Form, SheetNewtons);

public sealed record ClawLines((HullPoint Pin, double Metres)[] Stays, double Shroud, double Halyard, double Sheet);

/// <summary>
/// A shunting canoe's crab claw at one of her ends: the yard turns on its halyard about the line from the tack to the
/// masthead, the boom about the yard on its lashing; the sail stands only to the side away from her float.
/// </summary>
public sealed class CrabClaw : Rigging
{
    public const string Kind = "crabclaw";

    private const int Samples = 64;
    private const int Swings = 16;
    private const int Halvings = 10;
    private const double ShortestLeverMetres = 1e-3;
    private static readonly HullPoint Gravity = WaveTheory.Gravity * new HullPoint(0, -1, 0);

    private readonly HullPoint axis;
    private readonly (double Yard, double Boom, double Cloth) kilograms;
    private readonly SailForm rest;
    private readonly (HullPoint From, HullPoint To, double Width)[] standing;
    private ClawState? furled;

    public CrabClaw(ClawPose hoisted, ClawPose lowered, ClawPose model, int ahead, double camber, (double Yard, double Boom, double Cloth) kilograms, RigWidths widths)
    {
        (Hoisted, Lowered, Model, Ahead, this.kilograms, Widths) = (hoisted, lowered, model, ahead, kilograms, widths);
        standing = [(hoisted.Heel, hoisted.Masthead, widths.Pole), (hoisted.Masthead, hoisted.SternPin, widths.Line),
            (hoisted.Masthead, hoisted.ShroudFoot, widths.Line), (hoisted.ShoreFoot, hoisted.ShoreTop, widths.Shore)];
        axis = (hoisted.Masthead - hoisted.Tack).Unit;
        rest = new SailForm(hoisted.Tack, hoisted.Tack, hoisted.Peak, hoisted.Clew, camber);
        Lee = -Math.Sign(hoisted.ShroudFoot.Across);
        var lee = Enumerable.Range(0, Steps).Select(k => 2 * Math.PI * k / Steps - Math.PI).MaxBy(turn => Lee * Turned(hoisted.Clew, turn).Across);
        var (from, span) = FreeTurns(lee, Blocked().ToArray());
        var step = 2 * Math.PI / Steps;
        while (span > 0 && !Clear(Pose(from, 0))) (from, span) = (from + step, span - step);
        while (span > 0 && !Clear(Pose(from + span, 0))) span -= step;
        Free = (from, span);
        Resting = Pose(from, 0).Centre(SailForm.Whole, 0).Height <= Pose(from + span, 0).Centre(SailForm.Whole, 0).Height ? from : from + span;
    }

    public static Rigging[] Of(JsonObject rig, double camber, double clothKilograms, RigWidths widths) =>
        (rig["ends"].AsArray() ?? []).Select((end, n) => (Rigging)new CrabClaw(ClawPose.Of(end["hoisted"]), ClawPose.Of(end["lowered"]),
            ClawPose.Of(rig["ends"].AsArray()![0]["lowered"]), n == 0 ? -1 : 1, camber, (rig["yardKilograms"].AsDouble(), rig["boomKilograms"].AsDouble(), clothKilograms),
            widths)).ToArray();

    public int Lee { get; }

    public ClawPose Hoisted { get; }

    public ClawPose Lowered { get; }

    public ClawPose Model { get; }

    public override int Ahead { get; }

    public override HullPoint Origin => Hoisted.Tack;

    public override HullPoint Axis => axis;

    public override RigWidths Widths { get; }

    public override double Resting { get; }

    public override HullPoint Belly(RigState state)
    {
        var normal = state.Form.Normal;
        return (Hoisted.Heel - Hoisted.Tack).Dot(normal) < 0 ? normal : -1 * normal;
    }

    public override bool Aback(RigState state, Airflow air) => air.Across * Lee < 0;

    public override int Leeward(Airflow air) => Lee;

    public override double LeeOf(RigState state, (double Reach, double Span) set)
    {
        var offset = state.Centre(set, 0) - Origin;
        return (offset - offset.Dot(axis) * axis).Across * Lee;
    }

    public HullPoint Turned(HullPoint point, double turn) => point.Turned(Hoisted.Tack, axis, turn);

    public override RigState Pose(double yard, double boom) => Claw(yard, boom);

    private ClawState Claw(double yard, double boom)
    {
        var tack = Hoisted.Tack;
        var peak = Turned(Hoisted.Peak, yard);
        var along = (peak - tack).Unit;
        var clew = Turned(Hoisted.Clew, yard).Turned(tack, along, boom);
        var bridle = Turned(Hoisted.Bridle, yard).Turned(tack, along, boom);
        var form = rest.Posed(tack, tack, peak, clew);
        if (Set.Span < 1)
        {
            var foot = (form.At(1, 0, 0, Set) - tack).Unit;
            (clew, bridle) = (tack + (clew - tack).Length * foot, tack + (bridle - tack).Length * foot);
        }
        var bend = Turned(Hoisted.HalyardBend, yard);
        return new ClawState(yard, boom, peak, clew, bend, bridle, form, 0, Hoisted, Taut(Hoisted, (Hoisted.Masthead - bend).Length, (Hoisted.Lead - bridle).Length));
    }

    public override RigState Furled => furled ??= new ClawState(0, 0, Lowered.Peak, Lowered.Clew, Lowered.HalyardBend, Lowered.Bridle,
        rest.Posed(Lowered.Tack, Lowered.Tack, Lowered.Peak, Lowered.Clew), 0, Lowered,
        Taut(Lowered, (Lowered.Masthead - Lowered.HalyardBend).Length, (Lowered.Lead - Lowered.Bridle).Length));

    public override double HoistMetres => (Hoisted.HalyardBend - Lowered.HalyardBend).Length;

    public override double SpanMetres => (Hoisted.Peak - Hoisted.Clew).Length;

    public override double Arm(RigState state)
    {
        var offset = ((ClawState)state).Bridle - Hoisted.Tack;
        return (offset - offset.Dot(axis) * axis).Length;
    }

    public override double BoomArm(RigState state)
    {
        var claw = (ClawState)state;
        var (offset, yard) = (claw.Bridle - Hoisted.Tack, (claw.Peak - Hoisted.Tack).Unit);
        return (offset - offset.Dot(yard) * yard).Length;
    }

    public override RigState Between(RigState from, RigState to, double share)
    {
        var (a, b) = ((ClawState)from, (ClawState)to);
        var lines = new ClawLines(b.Lines.Stays, b.Lines.Shroud, a.Lines.Halyard + share * (b.Lines.Halyard - a.Lines.Halyard), Math.Max(a.Lines.Sheet, b.Lines.Sheet));
        return Blend(a, b, share, share, lines);
    }

    public override (double Haul, double Carry) ShuntMetres(Rigging from)
    {
        var (p, q) = (((CrabClaw)from).Hoisted, Hoisted);
        return ((p.Masthead - q.Tack).Length - (q.Masthead - q.Tack).Length, (q.Tack - p.Tack).Length);
    }

    public override RigState Shunted(Rigging from, RigState start, RigState end, double hauled, double carried)
    {
        var (a, b) = ((ClawState)start, (ClawState)end);
        var (p, q) = (((CrabClaw)from).Hoisted, Hoisted);
        var haulTo = (q.Masthead - q.Tack).Length;
        var length = Math.Max(haulTo, (p.Masthead - q.Tack).Length - hauled);
        var (low, high) = (0.0, 1.0);
        for (var n = 0; n < Halvings * 2; n++)
        {
            var middle = (low + high) / 2;
            if ((p.Heel + Swung(p.Masthead - p.Heel, q.Masthead - q.Heel, middle) - q.Tack).Length > length) low = middle;
            else high = middle;
        }
        var raked = (low + high) / 2;
        var masthead = p.Heel + Swung(p.Masthead - p.Heel, q.Masthead - q.Heel, raked);
        var stays = Ordered(p.Tack, q.Tack).Select(pin => (pin, pin == q.Tack ? (masthead - pin).Length : (q.Masthead - pin).Length)).ToArray();
        var lines = new ClawLines(stays, (p.Masthead - p.ShroudFoot).Length, a.Lines.Halyard, Math.Max(a.Lines.Sheet, b.Lines.Sheet));
        return Blend(a, b, raked, carried, lines);
    }

    private ClawState Blend(ClawState a, ClawState b, double mast, double carry, ClawLines lines)
    {
        var (p, q) = (a.Rig, b.Rig);
        HullPoint Carried(HullPoint x, HullPoint y) => x + carry * (y - x);
        var (heel, tack) = (p.Heel + mast * (q.Heel - p.Heel), Carried(p.Tack, q.Tack));
        HullPoint OnMast(HullPoint x, HullPoint y) => heel + Swung(x - p.Heel, y - q.Heel, mast);
        HullPoint OnSpars(HullPoint x, HullPoint y) => tack + Swung(x - p.Tack, y - q.Tack, carry);
        var (masthead, bend) = (OnMast(p.Masthead, q.Masthead), OnSpars(a.Bend, b.Bend));
        var (peak, clew) = (OnSpars(a.Peak, b.Peak), OnSpars(a.Clew, b.Clew));
        var pose = new ClawPose(tack, masthead, heel, bend, peak, clew, OnSpars(a.Bridle, b.Bridle),
            Carried(p.Lead, q.Lead), p.SternPin, p.ShroudFoot, OnMast(p.ShoreTop, q.ShoreTop), p.ShoreFoot);
        return new ClawState(a.Yard + carry * (b.Yard - a.Yard), a.Boom + carry * (b.Boom - a.Boom), peak, clew, pose.HalyardBend, pose.Bridle,
            rest.Posed(tack, tack, peak, clew), 0, pose,
            lines with { Halyard = Math.Max(lines.Halyard, (masthead - bend).Length), Sheet = Math.Max(lines.Sheet, (pose.Lead - pose.Bridle).Length) });
    }

    public override RigState Track(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        double Unbalanced(double boom) => Swung(Claw(yard, boom), wind).Moment;
        var step = 2 * Math.PI / Swings;
        var (low, high) = (near, near);
        var (atLow, atHigh) = (Unbalanced(near), Unbalanced(near));
        for (var k = 0; k < Swings && atLow <= 0 == atHigh <= 0; k++)
        {
            if (atLow <= 0) (low, atLow) = (low - step, Unbalanced(low - step));
            if (atHigh > 0) (high, atHigh) = (high + step, Unbalanced(high + step));
        }
        if (!(atLow > 0 && atHigh <= 0)) return Settle(yard, near, wind);

        return Held(yard, Halved(low, high, Unbalanced), wind);
    }

    public override RigState Settle(double yard, double near, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        double Unbalanced(double boom) => Swung(Claw(yard, boom), wind).Moment;
        var swings = Enumerable.Range(0, Swings + 1).Select(k => -Math.PI + 2 * Math.PI * k / Swings).ToArray();
        var moments = swings.Select(Unbalanced).ToArray();
        var stable = Enumerable.Range(0, Swings).Where(k => moments[k] > 0 && moments[k + 1] <= 0)
            .Select(k => Halved(swings[k], swings[k + 1], Unbalanced)).ToArray();
        double Away(double boom) => Math.Abs(Math.IEEERemainder(boom - near, 2 * Math.PI));
        return Held(yard, stable.Length > 0 ? stable.MinBy(Away) : swings[Array.IndexOf(moments, moments.MinBy(Math.Abs))], wind);
    }

    private static double Halved(double low, double high, Func<double, double> unbalanced)
    {
        for (var n = 0; n < Halvings; n++)
        {
            var middle = (low + high) / 2;
            if (unbalanced(middle) > 0) low = middle;
            else high = middle;
        }
        return (low + high) / 2;
    }

    public override RigState Held(double yard, double boom, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        var posed = Claw(yard, boom);
        return posed with { SheetNewtons = Swung(posed, wind).Sheet };
    }

    private (double Moment, double Sheet) Swung(ClawState state, Func<RigState, (HullPoint At, HullPoint Force)> wind)
    {
        var tack = Hoisted.Tack;
        var (at, force) = wind(state);
        var weighed = (at - tack).Cross(force + kilograms.Cloth * Gravity) + (0.5 * (state.Peak - tack)).Cross(kilograms.Yard * Gravity)
            + (0.5 * (state.Clew - tack)).Cross(kilograms.Boom * Gravity);
        var sheet = (Hoisted.Lead - state.Bridle).Unit;
        var lever = (state.Bridle - tack).Cross(sheet);
        var reach = lever.Dot(axis);
        var pulled = Math.Abs(reach) > ShortestLeverMetres ? -weighed.Dot(axis) / reach : double.NaN;
        return ((weighed + (pulled > 0 ? pulled : 0) * lever).Dot((state.Peak - tack).Unit), pulled);
    }

    public override bool Clear(RigState state)
    {
        var claw = (ClawState)state;
        return standing.All(stay => !Crosses(stay.From, stay.To, Hoisted.Tack, claw.Peak, claw.Clew)
            && Apart(Hoisted.Tack, claw.Peak, stay.From, stay.To) > (Widths.Spar + stay.Width) / 2
            && Apart(Hoisted.Tack, claw.Clew, stay.From, stay.To) > (Widths.Spar + stay.Width) / 2);
    }

    public override RigLoad Loads(RigState state, HullPoint at, HullPoint force)
    {
        var claw = (ClawState)state;
        var (tack, pose) = (Hoisted.Tack, Hoisted);
        var sheet = (pose.Lead - claw.Bridle).Unit;
        var sheeted = claw.SheetNewtons > 0 ? claw.SheetNewtons : 0;
        var weights = new (HullPoint At, HullPoint Force)[]
        {
            (tack + 0.5 * (claw.Peak - tack), kilograms.Yard * Gravity),
            (tack + 0.5 * (claw.Clew - tack), kilograms.Boom * Gravity),
            (at, kilograms.Cloth * Gravity)
        };
        var weight = weights.Aggregate(default(HullPoint), (sum, w) => sum + w.Force);
        var halyard = (pose.Masthead - claw.Bend).Unit;
        var lifted = (claw.Bend - tack).Cross(halyard);
        var unheld = weights.Aggregate((at - tack).Cross(force), (sum, w) => sum + (w.At - tack).Cross(w.Force)) + (claw.Bridle - tack).Cross(sheeted * sheet);
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

    public override string Describe(RigState state)
    {
        var claw = (ClawState)state;
        var (pose, lines) = (claw.Rig, claw.Lines);
        string Spar(HullPoint end)
        {
            var run = end - pose.Tack;
            var level = Math.Sqrt(run.Along * run.Along + run.Across * run.Across);
            return $"{Degrees(Math.Atan2(run.Height, level))} up, {Degrees(Math.Atan2(Math.Abs(run.Across), Ahead * run.Along))} off her bow to {(run.Across >= 0 ? "starboard" : "port")}";
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "yard turned {0} about the tack-masthead line, boom {1} about the yard; yard {2}, boom {3}; {4}, {5}, {6}, shroud {7}",
            Degrees(claw.Yard), Degrees(claw.Boom), Spar(claw.Peak), Spar(claw.Clew), Line("halyard", pose.Masthead, claw.Bend, lines.Halyard),
            Line("sheet", claw.Bridle, pose.Lead, lines.Sheet),
            string.Join(", ", lines.Stays.Select(stay => Line(stay.Pin.Along * Ahead > 0 ? "forestay" : "backstay", pose.Masthead, stay.Pin, stay.Metres))),
            Line("", pose.Masthead, pose.ShroudFoot, lines.Shroud).TrimStart());
    }

    private static string Line(string name, HullPoint from, HullPoint to, double metres)
    {
        var span = (to - from).Length;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:F2} m{2}", name, metres,
            span < metres - 0.01 ? $" ({metres - span:F2} slack)" : span > metres + 0.01 ? $" (stretched {span - metres:F2})" : "");
    }

    public override double SheetMetres(RigState state) => ((ClawState)state).Lines.Sheet;

    public override HullPoint[] Marks(RigState state)
    {
        var claw = (ClawState)state;
        return [claw.Peak, claw.Clew, claw.Bend, claw.Bridle, claw.Rig.Masthead, claw.Rig.Tack];
    }

    public override RigState Slackened(RigState state, double metres) =>
        metres <= 0 ? state : (ClawState)state with { Lines = ((ClawState)state).Lines with { Sheet = ((ClawState)state).Lines.Sheet + metres } };

    public override void Draw(IRigCanvas canvas, RigState state, double fill)
    {
        var claw = (ClawState)state;
        canvas.Cloth(claw.Form, fill, Set);
        Spars(canvas, claw, Set.Span < 1 ? Math.Sqrt(1 - Set.Span) : 0);
    }

    private void Spars(IRigCanvas canvas, ClawState claw, double rolled)
    {
        var (pose, lines, tack) = (claw.Rig, claw.Lines, claw.Rig.Tack);
        canvas.Part("mast", (Model.Heel, Model.Masthead), (pose.Heel, pose.Masthead));
        canvas.Part("shore", (Model.ShoreFoot, Model.ShoreTop), (pose.ShoreFoot, pose.ShoreTop));
        canvas.Part("yard", (Model.Tack, Model.Peak), (tack, claw.Peak));
        canvas.Part("boom", (Model.Tack, Model.Clew), (tack, claw.Clew));
        if (rolled > 0) canvas.Part("roll", (Model.Tack, Model.Clew), (tack, claw.Clew), rolled);
        foreach (var (pin, metres) in lines.Stays) canvas.Line(pose.Masthead, pin, Widths.Line, metres);
        canvas.Line(pose.Masthead, pose.ShroudFoot, Widths.Line, lines.Shroud);
        canvas.Line(pose.Masthead, claw.Bend, Widths.Line, lines.Halyard);
        canvas.Line(claw.Bridle, pose.Lead, Widths.Line, lines.Sheet);
    }

    private static HullPoint[] Ordered(HullPoint pin, HullPoint other) => pin.Along <= other.Along ? [pin, other] : [other, pin];

    private static ClawLines Taut(ClawPose pose, double halyard, double sheet) =>
        new(Ordered(pose.Tack, pose.SternPin).Select(pin => (pin, (pose.Masthead - pin).Length)).ToArray(), (pose.Masthead - pose.ShroudFoot).Length, halyard, sheet);

    private static HullPoint Swung(HullPoint from, HullPoint to, double share)
    {
        var (a, b) = (from.Unit, to.Unit);
        var angle = Math.Acos(Math.Clamp(a.Dot(b), -1, 1));
        var turned = Math.Sin(angle) < 1e-6 ? a + share * (b - a) : Math.Sin((1 - share) * angle) / Math.Sin(angle) * a + Math.Sin(share * angle) / Math.Sin(angle) * b;
        return (from.Length + share * (to.Length - from.Length)) * turned.Unit;
    }

    public override void DrawFurled(IRigCanvas canvas, RigState state) => Spars(canvas, (ClawState)state, 1);

    private static (Push[] Loads, (string Line, double Newtons)[] Tensions) Stayed(ClawPose pose, HullPoint head)
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
        foreach (var (from, to, _) in standing)
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
