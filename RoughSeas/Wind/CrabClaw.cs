using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Water;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed record ClawState(double Yard, double Boom, HullPoint Peak, HullPoint Clew, HullPoint Bend, HullPoint Bridle, SailForm Form, double SheetNewtons)
    : RigState(Yard, Boom, Form, SheetNewtons);

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

    public CrabClaw(ClawPose hoisted, ClawPose lowered, int ahead, double camber, (double Yard, double Boom, double Cloth) kilograms, RigWidths widths)
    {
        (Hoisted, Lowered, Ahead, this.kilograms, Widths) = (hoisted, lowered, ahead, kilograms, widths);
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
        (rig["ends"].AsArray() ?? []).Select((end, n) => (Rigging)new CrabClaw(ClawPose.Of(end["hoisted"]), ClawPose.Of(end["lowered"]), n == 0 ? -1 : 1,
            camber, (rig["yardKilograms"].AsDouble(), rig["boomKilograms"].AsDouble(), clothKilograms), widths)).ToArray();

    public int Lee { get; }

    public ClawPose Hoisted { get; }

    public ClawPose Lowered { get; }

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
        return new ClawState(yard, boom, peak, clew, Turned(Hoisted.HalyardBend, yard), bridle, rest.Posed(tack, tack, peak, clew), 0);
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

    private ClawState Held(double yard, double boom, Func<RigState, (HullPoint At, HullPoint Force)> wind)
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
        var (claw, pose) = ((ClawState)state, Hoisted);
        string Spar(HullPoint end)
        {
            var run = end - pose.Tack;
            var level = Math.Sqrt(run.Along * run.Along + run.Across * run.Across);
            return $"{Degrees(Math.Atan2(run.Height, level))} up, {Degrees(Math.Atan2(Math.Abs(run.Across), Ahead * run.Along))} off her bow to {(run.Across >= 0 ? "starboard" : "port")}";
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "yard turned {0} about the tack-masthead line, boom {1} about the yard; yard {2}, boom {3}; halyard {4:F2} m, sheet {5:F2} m, forestay {6:F2} m, backstay {7:F2} m, shroud {8:F2} m",
            Degrees(claw.Yard), Degrees(claw.Boom), Spar(claw.Peak), Spar(claw.Clew), (pose.Masthead - claw.Bend).Length,
            (pose.Lead - claw.Bridle).Length, (pose.Masthead - pose.Tack).Length, (pose.Masthead - pose.SternPin).Length, (pose.Masthead - pose.ShroudFoot).Length);
    }

    public override void Draw(IRigCanvas canvas, RigState state, double fill, (double Reach, double Span) set)
    {
        var (claw, pose) = ((ClawState)Pose(state.Yard, state.Boom), Hoisted);
        canvas.Cloth(claw.Form, fill, set);
        var tack = pose.Tack;
        var foot = (claw.Form.At(1, 0, 0, set) - tack).Unit;
        var clew = tack + (pose.Clew - tack).Length * foot;
        var bridle = tack + (pose.Bridle - tack).Length * foot;
        Standing(canvas, pose);
        canvas.Spar(tack, claw.Peak, Widths.Spar);
        canvas.Spar(tack, clew, Widths.Spar);
        if (set.Span < 1) canvas.Roll(tack, clew, Widths.Roll * Math.Sqrt(1 - set.Span));
        canvas.Line(pose.Masthead, claw.Bend, Widths.Line);
        canvas.Line(bridle, pose.Lead, Widths.Line);
    }

    public override void DrawFurled(IRigCanvas canvas)
    {
        var pose = Lowered;
        Standing(canvas, pose);
        canvas.Spar(pose.Tack, pose.Peak, Widths.Spar);
        canvas.Spar(pose.Tack, pose.Clew, Widths.Spar);
        canvas.Roll(pose.Tack, pose.Clew, Widths.Roll);
        canvas.Line(pose.Masthead, pose.HalyardBend, Widths.Line);
        canvas.Line(pose.Bridle, pose.Lead, Widths.Line);
    }

    private void Standing(IRigCanvas canvas, ClawPose pose)
    {
        canvas.Spar(pose.Heel, pose.Masthead, Widths.Pole);
        canvas.Spar(pose.ShoreFoot, pose.ShoreTop, Widths.Shore);
        canvas.Line(pose.Masthead, pose.Tack, Widths.Line);
        canvas.Line(pose.Masthead, pose.SternPin, Widths.Line);
        canvas.Line(pose.Masthead, pose.ShroudFoot, Widths.Line);
    }

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
