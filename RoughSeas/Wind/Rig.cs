using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public sealed class Rig
{
    private readonly RigEnd[] ends;

    public Rig(RigEnd[] ends) => this.ends = ends;

    public static Rig? Of(JsonObject rig, double camber) =>
        rig.Exists ? new Rig(rig["ends"].AsArray().Select((end, n) => new RigEnd(RigPose.Of(end["hoisted"]), RigPose.Of(end["lowered"]), n == 0 ? -1 : 1, camber)).ToArray()) : null;

    public int Tacked { get; set; }

    public RigEnd End => ends[Tacked];

    public int Ends => ends.Length;
}

public sealed record RigLoad(Push[] Points, double SheetNewtons, double HalyardNewtons, (string Line, double Newtons)[] Stays);

public sealed class RigEnd
{
    private const int Steps = 720;
    private const int Samples = 64;

    public RigEnd(RigPose hoisted, RigPose lowered, int ahead, double camber)
    {
        (Hoisted, Lowered, Ahead) = (hoisted, lowered, ahead);
        Form = new SailForm(hoisted.Tack, hoisted.Tack, hoisted.Peak, hoisted.Clew, hoisted.Tack, hoisted.Masthead - hoisted.Tack, camber);
        Leeward = -Math.Sign(hoisted.ShroudFoot.Across);
        var lee = Enumerable.Range(0, Steps).Select(k => 2 * Math.PI * k / Steps - Math.PI).MaxBy(turn => Leeward * Turned(hoisted.Clew, turn).Across);
        Free = FreeTurns(lee, Blocked().ToArray());
    }

    public int Leeward { get; }

    public RigPose Hoisted { get; }

    public RigPose Lowered { get; }

    public SailForm Form { get; }

    public int Ahead { get; }

    public (double From, double Span) Free { get; }

    public HullPoint Turned(HullPoint point, double turn) => Form.Turned(point, turn);

    public RigLoad Loads(HullPoint at, HullPoint force, double turn)
    {
        var (tack, axis, pose) = (Form.Origin, Form.Axis, Hoisted);
        var (bend, bridle) = (Turned(pose.HalyardBend, turn), Turned(pose.Bridle, turn));
        var sheet = (pose.Lead - bridle).Unit;
        var wind = (at - tack).Cross(force);
        var sheeted = Math.Max(0, -wind.Dot(axis) / (bridle - tack).Cross(sheet).Dot(axis));
        var halyard = (pose.Masthead - bend).Unit;
        var lifted = (bend - tack).Cross(halyard);
        var unheld = wind + (bridle - tack).Cross(sheeted * sheet);
        var hoisted = Math.Max(0, -unheld.Dot(lifted) / lifted.Dot(lifted));
        var stepped = -1 * (force + sheeted * sheet + hoisted * halyard);
        var head = hoisted * ((pose.Heel - pose.Masthead).Unit - halyard);
        var masthead = Stayed(pose, head);
        return new RigLoad(
            [
                new(tack, -1 * stepped, unheld + hoisted * lifted),
                new(pose.Lead, -sheeted * sheet, default),
                new(pose.Heel, hoisted * (pose.Masthead - pose.Heel).Unit, default),
                ..masthead.Loads
            ],
            sheeted, hoisted, masthead.Tensions);
    }

    private static (Push[] Loads, (string Line, double Newtons)[] Tensions) Stayed(RigPose pose, HullPoint head)
    {
        var mast = (pose.Masthead - pose.Heel).Unit;
        var (shroud, backstay, forestay) = ((pose.ShroudFoot - pose.Masthead).Unit, (pose.SternPin - pose.Masthead).Unit, (pose.Tack - pose.Masthead).Unit);
        foreach (var (name, stay, foot) in new[] { ("backstay", backstay, pose.SternPin), ("forestay", forestay, pose.Tack) })
        {
            if (Solve(mast, shroud, stay, -1 * head) is not var (strut, held, pulled) || held < 0 || pulled < 0) continue;
            return ([new(pose.Heel, -strut * mast, default), new(pose.ShroudFoot, -held * shroud, default), new(foot, -pulled * stay, default)],
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

    public IEnumerable<double> Turns()
    {
        for (var k = 0; k <= Steps; k++) yield return Free.From + Free.Span * k / Steps;
    }

    private (double Along, double Out, double Turn) AboutAxis(HullPoint point)
    {
        var (tack, axis) = (Form.Origin, Form.Axis);
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
